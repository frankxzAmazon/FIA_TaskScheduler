using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions;
using Microsoft.Exchange.WebServices.Data;
using log4net;
using System.IO;

namespace TaskScheduler.Monitors
{
    public class EmailFolderMonitor: IMonitor, IJobRunner
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private string monitorFolderName;
        private string moveFolderName;
        public EmailMonitor InboxMonitor { get; set; }
        public string MonitorName { get; set; }
        public FolderId MonitorFolder { get; private set; }
        public FolderId MoveFolder { get; private set; }
        public IJob Job { get; set; }
        public int Priority { get; set; }
        public string SubjectSubstring { get; set; }
        public bool SubjectIsExactMatch { get; set; }
        public string SenderEmailAddress { get; set; }
        public bool SenderIsExactMatch { get; set; }
        public bool ToDownload { get; set; }
        public bool ToAddTimestamp { get; set; }
        public string AttachmentSubstring { get; set; }
        public bool AttachmentIsExactMatch { get; set; }
        public string AttachmentFileExtension { get; set; }
        public string DownloadFolder => "F:\\actuary\\DACT\\ALM\\FIAHedging\\DBUpload\\" + MonitorName;
        //public string DownloadFolder => "\\sv351018\\NAS6\\actuary\\DACT\\ALM\\FIAHeding\\DBUpload\\" + MonitorName;
        public void SetMonitorFolder(string folderName = null, string mailbox = null)
        {
            monitorFolderName = (string.IsNullOrWhiteSpace(mailbox) ? "" : mailbox + " ") + folderName ?? "Inbox";
            MonitorFolder = InboxMonitor.Mailbox.GetFolderID(folderName, mailbox);
        }
        public void SetMoveFolder(string folderName = null, string mailbox = null)
        {
            moveFolderName = (string.IsNullOrWhiteSpace(mailbox) ? "" : mailbox + " ") + folderName ?? "Inbox";
            MoveFolder = InboxMonitor.Mailbox.GetFolderID(folderName, mailbox);
        }
        public void BeginMonitoring()
        {
            InboxMonitor.AddFolderMonitor(this);
            Job.IsOn = true;
            Job.UpdateStatus();
        }
        public void EndMonitoring()
        {
            InboxMonitor.RemoveFolderMonitor(this);
            Directory.Delete(DownloadFolder.CheckFolderPath(), true);
        }
        public void StartJob(List<Item> items)
        {

            foreach(var item in items)
            {
                StartJob(item as EmailMessage);
            }
        }
        public void StartJob(EmailMessage item)
        {
            if (item == null) return;
            bool ranJob = false;
            if (CheckEmail(item))
            {
                foreach (FileAttachment attachment in item.Attachments.Where(x => x is FileAttachment))
                {
                    try
                    {
                        if (CheckAttachment(attachment))
                        {
                            ranJob = true;
                            if (!string.IsNullOrWhiteSpace(AttachmentSubstring) && ToDownload)
                            {
                                var filePath = DownloadFolder.CheckFolderPath();
                                if(ToAddTimestamp)
                                {
                                    filePath += DateTime.Now.ToFileTime() + "_";
                                }
                                filePath+= attachment.Name;
                                (attachment as FileAttachment).Load(filePath);
                                Logger.Info("Downloaded file to " + filePath);
                                Job.StartJob(filePath);
                            }
                            else
                            {

                                Job.StartJob();
                            }   
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Fatal("Issue with trigger" + MonitorName.ToString(), ex);
                    }

                }
                if (string.IsNullOrWhiteSpace(AttachmentSubstring))
                {
                    Job.StartJob();
                    ranJob = true;
                }
                try
                {
                    if (MoveFolder != null && ranJob)
                    {
                        item.Move(MoveFolder);
                        Logger.Info("Moved message to " + moveFolderName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Fatal("Issue with trigger" + MonitorName.ToString(), ex);
                }

            }
        }
        public void RunJob(DateTime startDate, DateTime endDate)=>InboxMonitor.RunJob(this, startDate, endDate);
        public bool CheckEmail(EmailMessage item)
        {
            return CheckSubject(item) && CheckSender(item);
        }
        private bool CheckSubject(EmailMessage item)
        {
            return
            (
                (SubjectIsExactMatch && item.Subject.Equals(SubjectSubstring, StringComparison.InvariantCultureIgnoreCase))
                || (!SubjectIsExactMatch && item.Subject.CaseInsensitiveContains(SubjectSubstring))
                || string.IsNullOrWhiteSpace(SubjectSubstring)

            );
        }
        private bool CheckSender(EmailMessage item)
        {
            return
            (
                (SenderIsExactMatch && item.Sender.Address.Equals(SenderEmailAddress, StringComparison.InvariantCultureIgnoreCase))
                || (!SenderIsExactMatch && item.Sender.Address.CaseInsensitiveContains(SenderEmailAddress))
                || string.IsNullOrWhiteSpace(SenderEmailAddress)
                || SenderEmailAddress.ToLower()=="null"

            );
        }
        public bool CheckAttachment(Attachment item)
        {
            return
            (
                (AttachmentIsExactMatch && item.Name.Equals(AttachmentSubstring + AttachmentFileExtension, StringComparison.InvariantCultureIgnoreCase))
                ||
                (
                    (
                        (!AttachmentIsExactMatch && item.Name.CaseInsensitiveContains(AttachmentSubstring))
                        ||
                        string.IsNullOrWhiteSpace(AttachmentSubstring)
                    )
                    &&
                    (
                        string.IsNullOrWhiteSpace(AttachmentFileExtension)
                        ||
                        item.Name.CaseInsensitiveContains(AttachmentFileExtension)
                    )
                )
            );
        }

        public bool Compare(IMonitor monitor)
        {
            var other = monitor as EmailFolderMonitor;
            return other != null &&
                   MonitorName == other.MonitorName &&
                   MonitorFolder.Equals(other.MonitorFolder) &&
                   MoveFolder.Equals(other.MoveFolder) &&
                   SubjectSubstring == other.SubjectSubstring &&
                   SubjectIsExactMatch == other.SubjectIsExactMatch &&
                   AttachmentSubstring == other.AttachmentSubstring &&
                   AttachmentIsExactMatch == other.AttachmentIsExactMatch &&
                   AttachmentFileExtension == other.AttachmentFileExtension &&
                   DownloadFolder == other.DownloadFolder &&
                   InboxMonitor.Compare(other.InboxMonitor) &&
                   Job.Compare(other.Job); ;
        }
    }
}
