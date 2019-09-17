using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.SharePoint.Client;
using Extensions;
using log4net;
using TaskScheduler.Monitors;
using TaskScheduler.EntityFramework;
using TaskScheduler.Tasks;
using System.Data.SqlClient;

namespace TaskScheduler
{
    public class EntityManager: IJob
    {
        private EmailMonitor emailMonitor;
        private SharePointOnlineCredentials sharepointCredentials;
        private Dictionary<string, FolderMonitor> _folderMonitors = new Dictionary<string, FolderMonitor>();
        private Dictionary<string, (IJob Job, IMonitor Monitor)> _jobs = new Dictionary<string, (IJob, IMonitor)>();
        private string username;

        private List<SQLMonitor> _dbMonitors = new List<SQLMonitor>();
        public bool IsMonitoring { get; private set; }
        public bool IsOn { get; set; }
        public bool ToRunOnStart => true;
        public bool ToRunOnReset => true;
        public EntityManager()
        {
            username = ConfigurationManager.AppSettings["microsoftUsername"];
            var exchangeCredentials = new WebCredentials(ConfigurationManager.AppSettings["microsoftUsername"],
                ConfigurationManager.AppSettings["microsoftPassword"]);
            sharepointCredentials = new SharePointOnlineCredentials(ConfigurationManager.AppSettings["microsoftUsername"],
                ConfigurationManager.AppSettings["microsoftPassword"].ToSecureString());
            emailMonitor = new EmailMonitor(username, exchangeCredentials);
            RegisterSqlMonitors();
            IsOn = true;
        }
        public bool IsExecuting { get; private set; }
        public string JobName => "Base Monitor";
        public bool Compare(IJob job) => job.JobName == JobName && (job is EntityManager);
        public void DownloadAllJobs()
        {
            using (var dbContext = new TaskSchedulerEntities())
            {
                var jobs = dbContext.Jobs.Select(x=>x).ToList();
                foreach(var job in jobs)
                {
                    if (job.IsActive)
                    {
                        var outputJob = CreateJob(job);
                        var trigger = CreateMonitor(outputJob, job.TriggerId);
                        AddJob(trigger, outputJob);
                    }
                }
            }
        }
        public void UpdateStatus()
        {
            using (var dbcontext = new EntityFramework.TaskSchedulerEntities())
            {
                var status = dbcontext.CurrentStatus.Find(JobName);
                if (status == null)
                {
                    dbcontext.CurrentStatus.Add(new EntityFramework.CurrentStatu()
                    {
                        CurrentStatus = IsExecuting ? "Running" : IsOn ? "Online" : "Offline",
                        JobName = JobName,
                        LastRun = DateTime.Parse("1900-1-1")
                    });
                }
                else
                {
                    status.CurrentStatus = IsExecuting ? "Running" : IsOn ? "Online" : "Offline";
                    //status.LastRun = IsExecuting || !IsOn ? status.LastRun : DateTime.Now;
                }
                dbcontext.SaveChanges();
            }

        }
        public void UpdateStatus(DateTime date)
        {
            using (var dbcontext = new EntityFramework.TaskSchedulerEntities())
            {
                var status = dbcontext.CurrentStatus.Find(JobName);
                if (status == null)
                {
                    dbcontext.CurrentStatus.Add(new EntityFramework.CurrentStatu()
                    {
                        CurrentStatus = IsExecuting ? "Running" : IsOn ? "Online" : "Offline",
                        JobName = JobName,
                        LastRun = date
                    });
                }
                else
                {
                    status.CurrentStatus = IsExecuting ? "Running" : IsOn ? "Online" : "Offline";
                    status.LastRun = date;
                }
                dbcontext.SaveChanges();
            }

        }
        public void Refresh()
        {
            using (var dbContext = new TaskSchedulerEntities())
            {
                var jobs = dbContext.Jobs.Select(x => x).ToList();
                foreach (var job in jobs)
                {
                    var outputJob = CreateJob(job);
                    var trigger = CreateMonitor(outputJob, job.TriggerId);
                    if(_jobs.TryGetValue(job.JobName,out var value))
                    {
                        if(!outputJob.Compare(value.Job) || !trigger.Compare(value.Monitor))
                        {
                            
                            AddJob(trigger, outputJob);
                            value.Monitor.BeginMonitoring();
                        }
                    }
                    else
                    {
                        AddJob(trigger, outputJob);
                        value.Monitor.BeginMonitoring();
                    }
                }
            }
        }
        public void Reset()
        {
            DownloadAllJobs();
            foreach(var job in _jobs.Values)
            {
                var type = job.Monitor.GetType();
                if (type == typeof(FileMonitor) || type == typeof(EmailFolderMonitor))
                {
                    job.Monitor.BeginMonitoring();
                    if (job.Job.ToRunOnReset)
                    {
                        using (var dbcontext = new TaskSchedulerEntities())
                        {
                            DateTime startDate = dbcontext.CurrentStatus.Find(job.Job.JobName).LastRun;
                            DateTime endDate = DateTime.Now;


                            ((IJobRunner)job.Monitor).RunJob(startDate, endDate);
                        }
                    }
                }
                else
                {
                    job.Monitor.BeginMonitoring();
                    if (job.Job.ToRunOnReset)
                        job.Job.StartJob();
                }
            }
            IsMonitoring = true;
        }
        private void AddJob(IMonitor monitor, IJob job)
        {
            if (monitor != null && job != null)
            {
                if (monitor is IJob)
                {
                    _jobs[job.JobName] = ((IJob)monitor, monitor);
                }
                else
                {
                    _jobs[job.JobName] = (job, monitor);
                }
            }
        }
        void IJob.StartJob(string fileName) => Refresh();
        public void StartMonitoring()
        {
            DownloadAllJobs();
            foreach (var job in _jobs.Values.Where(x=> x.Monitor.GetType() !=typeof(EmailFolderMonitor)))
            {
                var jobType = job.Monitor.GetType();
                if (jobType == typeof(FileMonitor) || jobType == typeof(SharepointMonitor))
                {
                    job.Monitor.BeginMonitoring();
                    job.Job.IsOn = true;
                    if (job.Job.ToRunOnStart)
                    {
                        using (var dbcontext = new TaskSchedulerEntities())
                        {
                            DateTime startDate = dbcontext.CurrentStatus.Find(job.Job.JobName).LastRun;
                            DateTime endDate = DateTime.Now;


                            ((IJobRunner)job.Monitor).RunJob(startDate, endDate);
                        }
                    }
                }
                else
                {
                    job.Monitor.BeginMonitoring();
                    job.Job.IsOn = true;
                    if (job.Job.ToRunOnStart)
                        job.Job.StartJob();
                }
            }
            var emailList = _jobs.Values.Where(x => x.Monitor.GetType() == typeof(EmailFolderMonitor)).OrderBy(x => ((EmailFolderMonitor)x.Monitor).Priority);
            foreach (var job in emailList)
            {
                job.Monitor.BeginMonitoring();
                job.Job.IsOn = true;
                if (job.Job.ToRunOnStart)
                {
                    using (var dbcontext = new TaskSchedulerEntities())
                    {
                        DateTime startDate = dbcontext.CurrentStatus.Find(job.Job.JobName).LastRun;
                        DateTime endDate = DateTime.Now;


                        ((IJobRunner)job.Monitor).RunJob(startDate, endDate);
                    }
                }
            }
                IsMonitoring = true;
        }
        public void StopMonitoring()
        {
            foreach (var item in _jobs.Values)
            {
                item.Monitor.EndMonitoring();
            }
            IsOn = false;
            IsMonitoring = false;  
        }
        private Job CreateJob(EntityFramework.Job inputJob)
        {
            Job outputJob = new Job()
            {
                JobName = inputJob.JobName,
                ToRunOnStart = inputJob.ToRunOnStart,
                ToRunOnReset = inputJob.ToRunOnReset
            };
            foreach(var task in inputJob.Tasks.ToList())
            {
                outputJob.AddTask(CreateTask(task), task.Priority);
            }
            return outputJob;
        }
        private IMonitor CreateMonitor(Job job, string triggerId)
        {
            using (var dbContext = new TaskSchedulerEntities())
            {
                var email = dbContext.EmailTriggers.Find(triggerId);
                if(email != null)
                {
                    var ret = new EmailFolderMonitor()
                    {
                        AttachmentFileExtension = email.FileExtension,
                        AttachmentIsExactMatch = email.FileNameIsExactMatch,
                        AttachmentSubstring = email.FileNameSubstring,
                        InboxMonitor = emailMonitor,
                        MonitorName = triggerId,
                        Job = job,
                        SubjectIsExactMatch = email.SubjectIsExactMatch,
                        SubjectSubstring = email.SubjectSubstring,
                        Priority = email.Priority,
                        ToDownload = email.ToDownload,
                        ToAddTimestamp = email.ToAddTimestamp,
                        SenderEmailAddress = email.SenderEmailAddress,
                        SenderIsExactMatch = email.SenderIsExactMatch
                    };
                    ret.SetMonitorFolder(email.MonitorFolder, email.EmailAddress);
                    ret.SetMoveFolder(email.MoveFolder, email.EmailAddress);
                    return ret;
                }
                else
                {
                    var sp = dbContext.SharepointTriggers.Find(triggerId);
                    if(sp != null)
                    {
                        return new SharepointMonitor(sharepointCredentials, sp.SharepointFolder, sp.SharepointSite)
                        {
                            FileExtension = sp.FileExtension,
                            FileNameIsExactMatch = sp.FileNameIsExactMatch,
                            FileNameSubstring = sp.FileNameSubstring,
                            Job = job,
                            MonitorName = triggerId.ToString()
                        };
                        
                    }
                    else
                    {
                        var folder = dbContext.FolderTriggers.Find(triggerId);
                        if (folder != null)
                        {
                            if(!_folderMonitors.TryGetValue(folder.FolderToMonitor, out var monitor))
                            {
                                monitor = new FolderMonitor(folder.FolderToMonitor)
                                {
                                    IncludeSubDirectories = folder.ToIncludeSubDirectories
                                };

                                _folderMonitors[folder.FolderToMonitor] = monitor;
                            }
                            return new FileMonitor()
                            {
                                FileExtension = folder.FileExtension,
                                FileNameIsExactMatch = folder.FileNameIsExactMatch ?? false,
                                FileNameSubstring = folder.FileNameSubstring,
                                FolderToMonitor = folder.FolderToMonitor,
                                FolderMonitor = monitor,
                                Job = job,
                                MonitorName = triggerId.ToString()
                            };
                        }
                        else
                        {
                            var sql = dbContext.SQLTriggers.Find(triggerId);
                            if(sql!=null)
                            {
                                return new SQLMonitor(sql.InstanceName, sql.DBName, sql.SQLCode)
                                {
                                    NotificationType =  (SqlNotificationInfo)Enum.Parse(typeof(SqlNotificationInfo), sql.EventType,true),
                                    MonitorName = triggerId.ToString(),
                                    Job = job
                                };
                            }
                            var time = dbContext.TimeTriggers.Find(triggerId);
                            var timeMonitor = new TimeMonitor()
                            {
                                Frequency = (int)time.ExecutionFrequency,
                                Job = job,
                                MonitorName = triggerId.ToString(),
                                NextTime = time.FirstExecutionTime_EST_.ToUniversalTime(),
                                Unit = (DateTimeUnit)Enum.Parse(typeof(DateTimeUnit), time.ExecutionFrequencyUnits, true)
                            };
                            return timeMonitor;
                        }
                    }
                }
            }
        }
        private ITask CreateTask(EntityFramework.Task task)
        {
            using (var dbContext = new TaskSchedulerEntities())
            {
                var runTask = dbContext.RunTasks.Find(task.TaskId);
                if (runTask != null)
                {
                    return CreateRunTask(task, runTask);
                }
                else
                {
                    return CreateFileTask(task, dbContext.FileTasks.Find(task.TaskId));
                }
            }
        }
        private ITask CreateRunTask(EntityFramework.Task task, EntityFramework.RunTask runTask)
        {
            if(runTask.ProgramRunType.Equals("Command Line",StringComparison.InvariantCultureIgnoreCase))
            {
                return new ExeTask()
                {
                    Location = runTask.ProgramLocation,
                    Name = runTask.ProgramName,
                    ToWait = task.ToWaitForFinish,
                    Parameters = runTask.ProgramCommaDelimParams.Split(',')
                };
            }
            else if (runTask.ProgramRunType.Equals("Stored Procedure", StringComparison.InvariantCultureIgnoreCase))
            {
                return new StoredProcedureTask()
                {
                    Location = runTask.ProgramLocation,
                    Name = runTask.ProgramName,
                    ToWait = task.ToWaitForFinish,
                    Parameters = runTask.ProgramCommaDelimParams.Split(',')
        };
            }
            else
            {
                throw new NotImplementedException("Task type " + runTask.ProgramRunType + " has not been implemented");
            }
        }
        private ITask CreateFileTask(EntityFramework.Task task, EntityFramework.FileTask fileTask)
        {
            return new TaskScheduler.Tasks.FileTask(fileTask.DestinationFolder)
            {
                DateFormat = fileTask.DateFormat,
                ToIncludeDate = fileTask.ToIncludeDate,
                ToUnzip = fileTask.ToUnzip,
                ToWait = task.ToWaitForFinish,
                ToDelete = fileTask.ToDeleteOriginal
            };
        }
        private void RegisterSqlMonitors()
        {
            var conn = ConfigurationManager.ConnectionStrings["TaskDbConnectionString"].ToString();
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Job") { Job = this, MonitorName = "Base Job Insert",NotificationType = SqlNotificationInfo.Insert});
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Job") { Job = this, MonitorName = "Base Job Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Job") { Job = this, MonitorName = "Base Job Delete", NotificationType = SqlNotificationInfo.Delete });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Task") { Job = this, MonitorName = "Base Task Insert", NotificationType = SqlNotificationInfo.Insert });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Task") { Job = this, MonitorName = "Base Task Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.Task") { Job = this, MonitorName = "Base Task Delete", NotificationType = SqlNotificationInfo.Delete });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.EmailTrigger") { Job = this, MonitorName = "Base EmailTrigger Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.FileTask") { Job = this, MonitorName = "Base FileTask Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.FolderTrigger") { Job = this, MonitorName = "Base FolderTrigger Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.RunTask") { Job = this, MonitorName = "Base RunTask Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.SharepointTrigger") { Job = this, MonitorName = "Base SharepointTrigger Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.SQLTrigger") { Job = this, MonitorName = "Base SQLTrigger Update", NotificationType = SqlNotificationInfo.Update });
            _dbMonitors.Add(new SQLMonitor(conn, "dbo.TimeTrigger") { Job = this, MonitorName = "Base TimeTrigger Update", NotificationType = SqlNotificationInfo.Update });
     

            _dbMonitors.ForEach((x) => x.BeginMonitoring());
        }
    }
}
