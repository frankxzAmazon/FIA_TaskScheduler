using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions;
using log4net;
using System.Threading;

namespace TaskScheduler.Monitors
{
    public class FileMonitor: IMonitor, IJobRunner
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private object _lock = new object();
        private Dictionary<string,bool> _isBeingUsed = new Dictionary<string, bool>();
        public string MonitorName { get; set; }
        public string FolderToMonitor { get; set; }
        public string FileNameSubstring { get; set; }
        public bool FileNameIsExactMatch { get; set; }
        public string FileExtension { get; set; }
        public FolderMonitor FolderMonitor { get; set; }
        public IJob Job { get; set; }
        public void BeginMonitoring()
        {
            FolderMonitor.AddFileMonitor(this);
            Job.IsOn = true;
            Job.UpdateStatus();
        }
            public void EndMonitoring()=>FolderMonitor.RemoveFileMonitor(this);
        public void StartJob(string file)
        {
            DateTime lastWrite = File.GetLastWriteTime(file);
            bool isBeingUsed;
            if (CheckFile(file))
            {
                lock(_lock)
                {

                    if(!_isBeingUsed.TryGetValue(file, out isBeingUsed))
                    {
                        isBeingUsed = false;
                    }
                    _isBeingUsed[file] = true;
   
                }
                if (!isBeingUsed)
                {
                    Logger.Info("Starting job");
                    try
                    {
                        Job.StartJob(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.Fatal(ex.Message);
                    }
                    finally
                    {
                        //We need to put something in here to flush the dictionary
                        Task.Run(async delegate
                        {
                            await Task.Delay(TimeSpan.FromSeconds(60));
                            _isBeingUsed[file] = false;
                        });
                    }
                }
            }
        }
        public void RunJob(DateTime startDate, DateTime endDate)
        {
            
            string[] files = Directory.GetFiles(FolderToMonitor, "*" + FileNameSubstring +"*", FolderMonitor.IncludeSubDirectories ? SearchOption.AllDirectories: SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                DateTime lastWrite = File.GetLastWriteTime(file);
                if (startDate <= lastWrite && lastWrite <= endDate)
                {
                    StartJob(file);
                }
            }
            Job.UpdateStatus(endDate);
        }
        public bool CheckFile(string file)
        {
            return
                ((FileNameIsExactMatch && Path.GetFileName(file).Equals(FileNameSubstring + FileExtension, StringComparison.InvariantCultureIgnoreCase))
                ||    
                (
                    (
                        (!FileNameIsExactMatch && Path.GetFileName(file).CaseInsensitiveContains(FileNameSubstring))
                        ||
                        string.IsNullOrWhiteSpace(FileNameSubstring)
                    )
                    &&
                    (
                        string.IsNullOrWhiteSpace(FileExtension)
                        ||
                        Path.GetExtension(file).CaseInsensitiveContains(FileExtension)
                    )
                 ));
        }
        public bool Compare(IMonitor monitor)
        {
            var other = monitor as FileMonitor;
            return other != null &&
                   MonitorName == other.MonitorName &&
                   FolderToMonitor == other.FolderToMonitor &&
                   FileNameSubstring == other.FileNameSubstring &&
                   FileNameIsExactMatch == other.FileNameIsExactMatch &&
                   FileExtension == other.FileExtension &&
                   FolderMonitor.Compare(other.FolderMonitor) &&
                   Job.Compare(other.Job); 
        }
    }
}
