using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using TaskScheduler;
namespace TaskSchedulerService
{
    public partial class TaskSchedulerService : ServiceBase
    {
        private EntityManager _manager = new EntityManager();
        public TaskSchedulerService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _manager.StartMonitoring();
        }
        protected override void OnContinue()
        {
            _manager.Reset();
        }
        protected override void OnStop()
        {
            _manager.StopMonitoring();
        }
        protected override void OnShutdown()
        {
            _manager.StopMonitoring();
        }
    }
}
