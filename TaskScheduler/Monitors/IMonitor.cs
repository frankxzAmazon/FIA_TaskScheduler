using System;

namespace TaskScheduler.Monitors
{
    public interface IMonitor
    {
        string MonitorName { get;  }
        void BeginMonitoring();
        void EndMonitoring();
        bool Compare(IMonitor monitor);
    }
}