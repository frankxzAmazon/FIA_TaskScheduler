using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions;
using System.Threading;

namespace TaskScheduler.Tasks
{
    public class FileTask: ITask
    {
        public FileTask(string destinationFolder)
        {
            DestinationFolder = destinationFolder.CheckFolderPath();
        }
        public bool IsExecuting { get; private set; }
        public string Location { get; set; }
        public string Name { get; set; }
        //public string[] Parameters { get; set; }
        public bool ToWait { get; set; }
        public string DestinationFolder { get; }
        public bool ToIncludeDate { get; set; }
        public string DateFormat { get; set; }
        public bool ToUnzip { get; set; }
        public string LastNewFile { get; private set; }
        public bool ToDelete { get; set; }
        public bool Compare(ITask task)
        {
            var other = task as FileTask;
            return other != null &&
                   ToWait == other.ToWait &&
                   DestinationFolder == other.DestinationFolder &&
                   ToIncludeDate == other.ToIncludeDate &&
                   DateFormat == other.DateFormat &&
                   ToUnzip == other.ToUnzip;
        }


        public async Task RunTask(ILog log)
        {
            if (IsExecuting)
            {
                log.Warn("Stored procedure " + Name + " cannot start until the previous command is complete.");
            }
            else
            {
                try
                {
                    IsExecuting = true;
                    DateTime start = DateTime.UtcNow;
                    log.Info("Trying to copy file " + Name);
                    await Task.Run(() =>
                    {
                        DateTime later = DateTime.UtcNow.AddMinutes(10);
                    while (!IsFileReady() && DateTime.UtcNow < later)
                        {
                            Thread.Sleep(1000);
                        }
                        
                        if (ToUnzip && Path.GetExtension(LastNewFile).Equals(".zip", StringComparison.InvariantCultureIgnoreCase))
                        {
                            System.IO.Compression.ZipFile.ExtractToDirectory(LastNewFile, DestinationFolder);
                        }
                    }).ConfigureAwait(false); 
                }
                catch (Exception ex)
                {
                    log.Fatal("Stored procedure " + Name + " failed.", ex);
                }
                finally
                {
                    IsExecuting = false;
                }
            }
        }
        private bool IsFileReady()
        {
            try
            {
                string fileName;
                if(ToIncludeDate)
                {
                    try
                    {
                        fileName = System.IO.File.GetLastWriteTime(Path.Combine(Location, Name)).ToString(DateFormat) + Name;
                    }
                    catch(FormatException)
                    {
                        fileName = System.IO.File.GetLastWriteTime(Path.Combine(Location, Name)).ToString("yyyyMMdd_HHmmsstt") + Name;
                    }
                }
                else
                {
                    fileName = Name;
                }
                if(string.IsNullOrWhiteSpace(DestinationFolder))
                {
                    throw new DirectoryNotFoundException(DestinationFolder +" not found and could not create");
                }
                var outputFile = Path.Combine(DestinationFolder, fileName);
                while (File.Exists(outputFile))
                {
                    fileName = Path.GetFileNameWithoutExtension(outputFile) + " - Copy" + Path.GetExtension(outputFile);
                    outputFile = Path.Combine(DestinationFolder, fileName);
                }
               
                
                if (ToDelete)
                {
                    File.Move(Path.Combine(Location, Name), DestinationFolder.CheckFolderPath() + fileName);
                }
                else
                {
                    File.Copy(Path.Combine(Location, Name), DestinationFolder.CheckFolderPath() + fileName);
                }
                LastNewFile = DestinationFolder + fileName;
                return true;
            }
            catch(FileNotFoundException ex)
            {
                return true;
            }
            catch (Exception es)
            {
                return false;
            }
        }
    }
}
