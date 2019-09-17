using TaskScheduler.DataSources;
using Microsoft.Exchange.WebServices.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Extensions;
using log4net;
using System.Threading;
using System.Timers;

namespace TaskScheduler.Monitors
{
    public class EmailMonitor : IMonitor
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private bool issubscribed;
        private StreamingSubscriptionConnection connection = null;
        private StreamingSubscriptionConnection.NotificationEventDelegate _onNewMail = null; 
        private StreamingSubscriptionConnection.SubscriptionErrorDelegate _onDisconnect = null;
        private StreamingSubscriptionConnection.SubscriptionErrorDelegate _onSubscriptionError = null;
        private ElapsedEventHandler _onTimedEvent ;
        private  object _lock = new object();
        private System.Timers.Timer _timer;
        //private List<EmailFolderMonitor> _jobs = new List<EmailFolderMonitor>();
        private Dictionary<string,List<EmailFolderMonitor>> folders = new Dictionary<string, List<EmailFolderMonitor>>();
        private Dictionary<string, StreamingSubscription> subscriptions = new Dictionary<string, StreamingSubscription>();
        public EmailMonitor(string email, WebCredentials credentials)
        {
            Mailbox = new ExchangeMailbox(email, credentials);

            _onTimedEvent = new ElapsedEventHandler(OnTimedEvent);
            _timer = new System.Timers.Timer()
            {
                AutoReset = true,
                Enabled = false,
                Interval = TimeSpan.FromMinutes(15).TotalMilliseconds
            };
            _timer.Elapsed += _onTimedEvent;
        }
        public ExchangeMailbox Mailbox { get; }
        public ExchangeService Service => Mailbox.Service;
        public string MonitorName { get; set; }
       
        public void BeginMonitoring( StreamingSubscription subscription = null, string name = null)
        {   

            if(subscription == null) subscription = Service.SubscribeToStreamingNotifications(new FolderId[] {WellKnownFolderName.Inbox },EventType.NewMail);
            if (!subscriptions.ContainsKey(name))
            {
                subscriptions[name] = subscription;
                CreateStreamingConnection(subscription);
                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
            }
            if (!connection.IsOpen) connection.Open();
        }
        private void CreateStreamingConnection(StreamingSubscription subscription)
        {
            if (connection == null)
            {
                connection = new StreamingSubscriptionConnection(Service, 30);
            }
            CloseConnection();
            connection.AddSubscription(subscription);

            _onNewMail = new StreamingSubscriptionConnection.NotificationEventDelegate(OnNewMail);
            _onDisconnect = new StreamingSubscriptionConnection.SubscriptionErrorDelegate(OnDisconnect);
            _onSubscriptionError = new StreamingSubscriptionConnection.SubscriptionErrorDelegate(OnSubscriptionError);
            connection.OnNotificationEvent += _onNewMail;
            connection.OnDisconnect += _onDisconnect;
            connection.OnSubscriptionError += _onDisconnect;
        }
        private void CloseConnection()
        {
            if (connection.IsOpen)
            {
                connection.Close();
            }
            if (_onDisconnect != null || _onNewMail != null || _onSubscriptionError != null)
            {
                connection.OnNotificationEvent -= _onNewMail;
                connection.OnDisconnect -= _onDisconnect;
                connection.OnSubscriptionError -= _onSubscriptionError;
            }
        }
        void IMonitor.BeginMonitoring() => BeginMonitoring();
        public void EndMonitoring()
        {
            _timer.Stop();
            if (connection == null) return;
            CloseConnection();
            foreach(var subscription in subscriptions.ToList())
            {
                connection.RemoveSubscription(subscription.Value);
                subscriptions.Remove(subscription.Key);
            }
            Logger.Info("Ended all email subscriptions");
        }
        public void AddFolderMonitor(EmailFolderMonitor email)
        {
            if (!folders.ContainsKey(email.MonitorFolder.UniqueId))
            {
                folders[email.MonitorFolder.UniqueId] = new List<EmailFolderMonitor>();
                StreamingSubscription streamingsubscription = null;
                while (streamingsubscription == null)
                {
                    try
                    {
                        streamingsubscription = Service.SubscribeToStreamingNotifications(new FolderId[] { email.MonitorFolder }, EventType.NewMail);
                    }
                    catch(ServiceResponseException)
                    {
                        streamingsubscription = null;
                        Thread.Sleep(60000);
                    }
                    
                }
                    BeginMonitoring(streamingsubscription, email.MonitorFolder.UniqueId);
                    issubscribed = true;
            }
            folders[email.MonitorFolder.UniqueId].Add(email);
        }
        public void RemoveFolderMonitor(EmailFolderMonitor email)
        {
            if (!folders.ContainsKey(email.MonitorFolder.UniqueId))
            {
                folders[email.MonitorFolder.UniqueId].Remove(email);
            }
            if (folders.Count == 0) EndMonitoring();
        }
        public void RunJob(DateTime startDate, DateTime endDate)
        {
            foreach (var folderMonitor in folders.SelectMany(x => x.Value))
            {
                var items = Mailbox.FilterFolder(folderMonitor.MonitorFolder, folderMonitor.SubjectSubstring, startDate, endDate);
                if (items.Count > 0)
                {
                    folderMonitor.StartJob(items);
                }
                folderMonitor.Job.UpdateStatus(endDate);
            }
        }
        public void RunJob(EmailFolderMonitor folderMonitor, DateTime startDate, DateTime endDate)
        {
            var folder = folders.SelectMany(x => x.Value).FirstOrDefault(x => x == folderMonitor);
            if(folder != null)
            {
                var items = Mailbox.FilterFolder(folder.MonitorFolder, folder.SubjectSubstring, startDate, endDate);
                if(items.Count > 0)
                {
                    folderMonitor.StartJob(items);
                }
            }
            folderMonitor.Job.UpdateStatus(endDate);
        }

        private void OnNewMail(object sender, NotificationEventArgs args)
        {
            var startDate = DateTime.Now.AddSeconds(-10);
            try
            {
                var newMails = from e in args.Events.OfType<ItemEvent>()
                               where e.EventType == EventType.NewMail
                               select e.ItemId;
                if (newMails.Count() > 0)
                {
                    var responses = Service.BindToItems(newMails,
                        new PropertySet(BasePropertySet.FirstClassProperties, ItemSchema.Attachments, ItemSchema.ParentFolderId));
                    foreach (var response in responses)
                    {
                        var monitors = folders[response.Item.ParentFolderId.UniqueId];
                        foreach (var monitor in monitors.OrderBy(x => x.Priority))
                        {
                            monitor.StartJob(response.Item as EmailMessage);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Warn("Error on new mail. Restarting the connection to Microsoft Exchange", ex);
                Restart();
                RunJob(startDate, DateTime.Now);
                Logger.Info("Reconnected to Microsoft Exchange");
            }
        }
        private void OnDisconnect(object sender, SubscriptionErrorEventArgs args)
        {
            if (!connection.IsOpen)
            {
                Logger.Info(MonitorName + " disconnected from Microsoft Exchange due to timeout", args.Exception);
                try
                {
                    connection.Open();
                    Logger.Info("Reconnected to Microsoft Exchange");
                }
                catch(Exception ex)
                {
                    if (!connection.IsOpen)
                        Logger.Warn("Error on reconnect attempt. Restarting the connection to Microsoft Exchange", ex);
                    Restart();
                    Logger.Info("Reconnected to Microsoft Exchange");
                }
            }
           
        }
        private void OnSubscriptionError(object sender, SubscriptionErrorEventArgs args)
        {
            Logger.Warn(MonitorName + " disconnected from Microsoft Exchange due to error",args.Exception);
            Restart();
            Logger.Info("Reconnected to Microsoft Exchange");
        }
        private void Restart()
        {
            if (Monitor.TryEnter(_lock))
            {
                try
                {
                    var startDate = DateTime.Now;
                    EndMonitoring();
                    var temp = folders.ToDictionary(x=> x.Key, x=> x.Value);
                    var selection = folders.Values.SelectMany(y => y).ToList();
                    folders = new Dictionary<string, List<EmailFolderMonitor>>();
                    foreach (var folder in selection)
                    {
                        AddFolderMonitor(folder);
                    }
                    if(folders.Count == 0)
                    {
                        folders = temp.ToDictionary(x => x.Key, x => x.Value);
                    }
                    //RunJob(startDate, DateTime.Now);
                }
                catch(Exception ex)
                {
                    Logger.Fatal("Could not reconnect to microsoft exchange", ex);
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }

        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            var checkTime = DateTime.UtcNow.AddMinutes(5);
            Logger.Info("Checking that " + MonitorName + " is still connected to Microsoft Exchange");
            do
            {
                if (connection.IsOpen)
                {
                    return;
                }

            } while (DateTime.UtcNow <= checkTime);
            Logger.Info(MonitorName + " disconnected from Microsoft Exchange for more than five minutes. Restarting connection");
            Restart();
        }
        public bool Compare(IMonitor monitor)
        {
            var other = monitor as EmailMonitor;
            return other != null &&
                   MonitorName == other.MonitorName;
        }
    }
}
