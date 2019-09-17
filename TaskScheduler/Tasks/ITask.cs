using System.Threading.Tasks;
using log4net;

namespace TaskScheduler.Tasks
{
    public interface ITask
    {
        bool IsExecuting { get; }
        string Location { get; set; }
        string Name { get; set; }
        bool ToWait { get; set; }
        bool Compare(ITask task);
        Task RunTask(ILog log);
    }
}