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
using System.Threading;

namespace TaskScheduler
{
    class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            Logger.Info("Starting now");
            var manager = new EntityManager();
            Console.WriteLine("Initializing the monitor. Stand by...");
            //async void () <= { await System.Threading.Tasks.Task.Delay(100); }
            manager.StartMonitoring();
            Console.WriteLine("Monitoring has begun");
            var mre = new ManualResetEvent(false);

            // This is optional - if you want to allow the code to exit from the command line, you could add something like:
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Console.WriteLine("Press (x) to exit");
                bool hasDoneDailyReset = false;
                while (true)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.X)
                    {
                        mre.Set(); // This will let the main thread exit
                        break;
                    }
                    else if (DateTime.Now.Hour == 3)
                    {
                        if (!hasDoneDailyReset)
                        {
                            manager.StopMonitoring();
                            Logger.Info("Restarting now");
                            manager = new EntityManager();
                            manager.StartMonitoring();
                            hasDoneDailyReset = true;
                        }
                    }
                    else
                    {
                        hasDoneDailyReset = false;
                    }
                }
            });

            // The main thread can just wait on the wait handle, which basically puts it into a "sleep" state, and blocks it forever
            mre.WaitOne();
        }
    }
}
