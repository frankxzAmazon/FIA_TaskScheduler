using System;
using System.Threading.Tasks;

namespace TaskScheduler
{
    public interface IJob
    {
        bool IsExecuting { get; }
        bool IsOn { get; set; }
        bool ToRunOnStart { get; }
        bool ToRunOnReset { get; }
        string JobName { get; }
        void StartJob(string fileName = null);
        void UpdateStatus();
        void UpdateStatus(DateTime date);
        bool Compare(IJob job);
    }
}