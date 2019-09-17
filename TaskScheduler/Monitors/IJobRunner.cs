using System;

namespace TaskScheduler.Monitors
{
    public interface IJobRunner
    {
        void RunJob(DateTime startDate, DateTime endDate);
    }
}