using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskScheduler.Tasks
{
    public class ExeTask : ITask
    {
        public bool IsExecuting { get; private set; }
        public string Location { get; set; }
        public string Name { get; set; }
        public string[] Parameters { get; set; }
        public bool ToWait { get; set; }

        public bool Compare(ITask task)
        {
            var other = task as ExeTask;
            return other != null &&
                   Location == other.Location &&
                   Name == other.Name &&
                   ToWait == other.ToWait &&
                   Parameters.SequenceEqual(other.Parameters);
        }

        public async Task RunTask(ILog log)
        {
            if(IsExecuting)
            {
                log.Warn("Program " + Name + " cannot start until the previous command is complete.");
            }
            // Enter the executable to run, including the complete path
            else if (File.Exists(Path.Combine(Location, Name)))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    //startInfo.Arguments = Parameters;
                    FileName = Path.Combine(Location, Name),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = string.Join(" ", Parameters)
                };
                try
                {
                    IsExecuting = true;
                    log.Info("Starting program " + Name);

                    using (Process exeProcess = Process.Start(startInfo))
                    {

                            while (!exeProcess.HasExited)
                            {
                                Thread.Sleep(10000);
                            }
                    }
                    log.Info("Finished program " + Name);
                }
                catch (Exception ex)
                {
                    log.Fatal("Error with program " + Name, ex);
                    //TODO: Add ability to read error messages from program
                }
                finally
                {
                    IsExecuting = false;
                }
            }
            else
            {
                log.Warn("Program cannot be found " + Path.Combine(Location, Name));
            }
        }
    }
}
