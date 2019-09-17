using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskScheduler.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace TaskScheduler
{
    public class Job : IJob
    {
        private static readonly ILog Log = LogManager.GetLogger(Environment.MachineName);
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);

        internal SortedDictionary<int,ITask> _tasks { get; set; } = new SortedDictionary<int, ITask> ();
        public bool IsOn { get; set; }
        public string JobName { get; set; }
        public bool ToRunOnStart { get; set; }
        public bool ToRunOnReset { get; set; }
        public bool IsExecuting { get; private set; }
        //subscribe
        //bring in tasks
        //pass log and do task (will need to update location for file tasks)
        public void AddTask(ITask task, int insertSpot)
        {
            _tasks.Add(insertSpot, task);
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
                    //status.LastRun = IsExecuting ? status.LastRun : DateTime.Now;
                }
                dbcontext.SaveChanges();
            }

        }
        public async void StartJob(string fileName = null)
        {
            await semaphore.WaitAsync();
            try
            {
                var awaitedTasks = new List<Task>();
                foreach (var keyvalupair in _tasks)
                {
                    UpdateStatus();
                    var task = keyvalupair.Value;
                    if (task is FileTask)
                    {
                        task.Location = Path.GetDirectoryName(fileName);
                        task.Name = Path.GetFileName(fileName);
                    }
                    awaitedTasks.Add(task.RunTask(Log));
                    if (task.ToWait)
                    {
                        await Task.WhenAll(awaitedTasks.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal("Job " + JobName + " failed.", ex);
            }
            finally
            {
                IsExecuting = false;
                UpdateStatus(DateTime.Now);
                semaphore.Release();
            }
        }

        public bool Compare(IJob job)
        {
            var other = job as Job;
            if(other != null && other.JobName == JobName)
            {
                foreach(var item in _tasks)
                {
                    if(other._tasks.TryGetValue(item.Key, out var task))
                    {
                        if(!task.Compare(item.Value))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }
    }
}
