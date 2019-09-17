using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskScheduler.DataSources;
using Extensions;
using Microsoft.SharePoint.Client;

namespace TaskScheduler.Monitors
{
    /// <summary>
    /// Initialize a SharepointMonitor class. The monitor will check the Sharepoint
    /// site every so often to check for new files.
    /// </summary>
    /// <seealso cref="TaskScheduler.IJob" />
    /// <seealso cref="TaskScheduler.Monitors.IMonitor" />
    /// <seealso cref="TaskScheduler.Monitors.IJobRunner" />
    public class SharepointMonitor : IJob, IMonitor, IJobRunner
    {
        /// <summary>
        /// The logger. We can easily log and store messages with this object. Log messages are
        /// stored in the TaskScheduler.dbo.Log table.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);

        /// <summary>
        /// The last time when Sharepoint was checked for new files.
        /// </summary>
        private DateTime _lastCheck;

        /// <summary>
        /// The time monitor
        /// </summary>
        private TimeMonitor _timeMonitor = new TimeMonitor();

        /// <summary>
        /// Initializes a new instance of the <see cref="SharepointMonitor"/> class.
        /// </summary>
        /// <param name="site">The site.</param>
        public SharepointMonitor(SharepointSite site)
        {
            Sharepoint = site;
            Sharepoint.CheckFile = CheckFile;
            _lastCheck = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharepointMonitor"/> class.
        /// </summary>
        /// <param name="cred">The cred.</param>
        /// <param name="folderName">Name of the folder.</param>
        /// <param name="url">The URL.</param>
        public SharepointMonitor(SharePointOnlineCredentials cred, string folderName, string url)
        {
            Sharepoint = new SharepointSite(cred)
            {
                CheckFile = CheckFile,
                SharePointFolderName = folderName,
                Url = url
            };
            _lastCheck = DateTime.Now;
        }

        /// <summary>
        /// Gets or sets the name of the Sharepoint monitor.
        /// </summary>
        /// <value>
        /// The name of the monitor.
        /// </value>
        public string MonitorName { get; set; }

        /// <summary>
        /// Gets or sets the file name substring. Any candidate file must contain the substring
        /// in order to be downloaded by the monitor.
        /// </summary>
        /// <value>
        /// The file name substring.
        /// </value>
        public string FileNameSubstring { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [file name is exact match].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [file name is exact match]; otherwise, <c>false</c>.
        /// </value>
        public bool FileNameIsExactMatch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is on.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is on; otherwise, <c>false</c>.
        /// </value>
        public bool IsOn { get; set; }

        /// <summary>
        /// Gets or sets the file extension (e.g. .csv, .xls, etc.). Any candidate file must end in
        /// the FileExtension in order to be downloaded.
        /// </summary>
        /// <value>
        /// The file extension.
        /// </value>
        public string FileExtension { get; set; }

        /// <summary>
        /// Gets the sharepoint site.
        /// </summary>
        /// <value>
        /// The sharepoint site.
        /// </value>
        public SharepointSite Sharepoint { get; }

        /// <summary>
        /// Gets or sets the job.
        /// </summary>
        /// <value>
        /// The job.
        /// </value>
        public Job Job { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is executing.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is executing; otherwise, <c>false</c>.
        /// </value>
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// Gets the name of the job.
        /// </summary>
        /// <value>
        /// The name of the job.
        /// </value>
        public string JobName => Job.JobName;

        /// <summary>
        /// Gets the download folder.
        /// </summary>
        /// <value>
        /// The download folder.
        /// </value>
        public string DownloadFolder => Directory.GetCurrentDirectory() + "\\" + MonitorName;

        /// <summary>
        /// Gets a value indicating whether [to run on start].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [to run on start]; otherwise, <c>false</c>.
        /// </value>
        public bool ToRunOnStart => ((IJob)Job).ToRunOnStart;

        /// <summary>
        /// Gets a value indicating whether [to run on reset].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [to run on reset]; otherwise, <c>false</c>.
        /// </value>
        public bool ToRunOnReset => ((IJob)Job).ToRunOnReset;

        /// <summary>
        /// Starts the job.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        public void StartJob(string fileName = null)
        {
            DateTime endDate = DateTime.Now;
            //Logger.Info("this is a foo testing message called from StartJob");
            Logger.Info("Checking sharepoint site for job " + JobName);
            if (Sharepoint.DownloadFiles(_lastCheck, endDate, FileNameSubstring, FileExtension, Logger, JobName))
            {
                _lastCheck = endDate;
                IsExecuting = true;
                DirectoryInfo d = new DirectoryInfo(Sharepoint.DownloadPath);
                foreach (var file in d.GetFiles())
                {
                    Job.StartJob(file.FullName);
                }
            }
            else
            {
                _lastCheck = endDate;
            }
            Job.UpdateStatus(_lastCheck);
        }

        /// <summary>
        /// Begins the monitoring of the Sharepoint site.
        /// </summary>
        public void BeginMonitoring()
        {
            IsOn = true;
            Job.IsOn = true;
            _timeMonitor.NextTime = DateTime.UtcNow.AddMinutes(2 * 60);
            _timeMonitor.Frequency = 2 * 60;  // in minutes
            _timeMonitor.Unit = DateTimeUnit.Minutes;
            _timeMonitor.Job = this;
            _timeMonitor.BeginMonitoring();
            Sharepoint.DownloadPath = DownloadFolder.CheckFolderPath();
            Logger.Info("Started sharepoint monitoring " + MonitorName);
            Job.UpdateStatus();
        }

        /// <summary>
        /// Ends the monitoring.
        /// </summary>
        public void EndMonitoring()
        {
            IsOn = false;
            _timeMonitor.EndMonitoring();
            Logger.Info("Ended sharepoint monitoring " + MonitorName);
            Directory.Delete(DownloadFolder.CheckFolderPath(), true);
        }

        /// <summary>
        /// Checks a candidate file to make sure it includes the substring and end in the desired extension.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public bool CheckFile(string file)
        {
            return
                (FileNameIsExactMatch && file.Equals(FileNameSubstring + FileExtension, StringComparison.InvariantCultureIgnoreCase))
                ||
                (
                    (
                        (!FileNameIsExactMatch && file.CaseInsensitiveContains(FileNameSubstring))
                        ||
                        string.IsNullOrWhiteSpace(FileNameSubstring)
                    )
                    &&
                    (
                        string.IsNullOrWhiteSpace(FileExtension)
                        ||
                        Path.GetFileName(file).CaseInsensitiveContains(FileExtension)
                    )
                 );
        }

        /// <summary>
        /// Compares two Sharepoint monitor jobs.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public bool Compare(object obj)
        {
            var other = obj as SharepointMonitor;
            return other != null &&
                   MonitorName == other.MonitorName &&
                   FileNameSubstring == other.FileNameSubstring &&
                   FileNameIsExactMatch == other.FileNameIsExactMatch &&
                   FileExtension == other.FileExtension &&
                   Sharepoint.SharePointFolderName == other.Sharepoint.SharePointFolderName &&
                   Sharepoint.Url == other.Sharepoint.Url &&
                   DownloadFolder == other.DownloadFolder &&
                   Job.Compare(other.Job);
        }

        /// <summary>
        /// Compares the specified job.
        /// </summary>
        /// <param name="job">The job.</param>
        /// <returns></returns>
        bool IJob.Compare(IJob job) => Compare(job);

        /// <summary>
        /// Compares the specified monitor.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <returns></returns>
        bool IMonitor.Compare(IMonitor monitor) => Compare(monitor);

        /// <summary>
        /// Updates the status of this monitor, e.g. by adding a last run date.
        /// </summary>
        public void UpdateStatus()
        {
            ((IJob)Job).UpdateStatus();
        }

        /// <summary>
        /// Updates the status.
        /// </summary>
        /// <param name="date">The date.</param>
        public void UpdateStatus(DateTime date)
        {
            ((IJob)Job).UpdateStatus(date);
        }

        /// <summary>
        /// Runs the Sharepoint job. 
        /// </summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        public void RunJob(DateTime startDate, DateTime endDate)
        {
            Logger.Info("this is a foo testing message called from RunJob");
            if (Sharepoint.DownloadFiles(startDate, endDate, FileNameSubstring, FileExtension, Logger, JobName))
            {
                if (endDate > _lastCheck) _lastCheck = endDate;
                IsExecuting = true;
                DirectoryInfo d = new DirectoryInfo(Sharepoint.DownloadPath);
                foreach (var file in d.GetFiles())
                {
                    Job.StartJob(file.FullName);
                }
            }
            else
            {
                if (endDate > _lastCheck) _lastCheck = endDate;
            }
            Job.UpdateStatus(_lastCheck);
        }

 
    }
}
