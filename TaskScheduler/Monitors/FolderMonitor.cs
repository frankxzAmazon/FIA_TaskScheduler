using Extensions;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace TaskScheduler.Monitors
{
    public class FolderMonitor: IMonitor
    {
        private bool _isMonitoring;
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private readonly FileSystemWatcher watcher = new FileSystemWatcher();
        private FileSystemEventHandler eventHandler;
        private List<FileMonitor> _fileMonitors = new List<FileMonitor>();
       
        public FolderMonitor(string path)
        {
            PathToMonitor = path.CheckFolderPath();
            eventHandler += new FileSystemEventHandler(OnNew);
        }
        public string PathToMonitor { get; set; }
        public bool IncludeSubDirectories { get; set; }
        public string MonitorName => PathToMonitor;
        public void BeginMonitoring()
        {
            if (!_isMonitoring)
            {
                Logger.Info("Starting monitoring for " + MonitorName);
                watcher.Path = PathToMonitor;
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Filter = "*.*";
                watcher.IncludeSubdirectories = IncludeSubDirectories;
                watcher.Changed += eventHandler;
                watcher.EnableRaisingEvents = true;
                _isMonitoring = true;
            }
        }
        public void EndMonitoring()
        {
            Logger.Info("Ending monitoring for " + MonitorName);
            watcher.Path = PathToMonitor;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = "*.*";
            watcher.Changed -= eventHandler;
            watcher.EnableRaisingEvents = false;
            _isMonitoring = false;
        }
        public void AddFileMonitor(FileMonitor file)
        {
            if (!_isMonitoring) BeginMonitoring();
            _fileMonitors.Add(file);
        }
        public void RemoveFileMonitor(FileMonitor file)
        {
            _fileMonitors.Remove(file);
            if (_fileMonitors.Count == 0) EndMonitoring();
        }
        private void OnNew(object sender, FileSystemEventArgs args)
        {
            foreach (var trigger in _fileMonitors)
            {
                trigger.StartJob(args.FullPath);
            }
        }

        public bool Compare(IMonitor monitor)
        {
            var other = monitor as FolderMonitor;
            return other != null &&
                   PathToMonitor == other.PathToMonitor &&
                   MonitorName == other.MonitorName;
        }
    }
}
