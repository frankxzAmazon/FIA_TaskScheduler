using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint.Client;
using System.IO;
using System.Net;
using System.Security;
using Extensions;
using System.Text.RegularExpressions;
using Microsoft.SharePoint.Client.Utilities;
using log4net;

namespace TaskScheduler.DataSources
{
    public class SharepointSite
    {
        /// <summary>
        /// The credentials needed to access the Sharepoint site
        /// </summary>
        private SharePointOnlineCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharepointSite"/> class.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        public SharepointSite(SharePointOnlineCredentials credentials)
        {
            _credentials = credentials;
        }

        /// <summary>
        /// Gets or sets the Sharepoint URL.
        /// </summary>
        /// <value>
        /// The Sharepoint site's URL.
        /// </value>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the name of the Sharepoint folder.
        /// </summary>
        /// <value>
        /// The name of the Sharepoint folder.
        /// </value>
        public string SharePointFolderName { get; set; }

        /// <summary>
        /// Gets or sets the download path.
        /// </summary>
        /// <value>
        /// The download path, i.e. the location to which we copy the new files.
        /// </value>
        public string DownloadPath { get; set; }

        /// <summary>
        /// A function which checks a Sharepoint file to see if it matches the patterns we expect.
        /// </summary>
        public Func<string, bool> CheckFile { get; set; }

        /// <summary>
        /// Downloads the new files in the Sharepoint site that have been modified between the start and end dates.
        /// </summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <returns>A boolean which represents whether any new files were found.</returns>
        public bool DownloadFiles(DateTime startDate, DateTime endDate, string fileNameSubstring, string fileExtension,
                                    ILog logger = null, string jobName = "")
        {
            //var foo = Microsoft.SharePoint.Client.
            // Temporarily adding 7 hours to each dateetime to adjust for difference between local and server time zone. 
            // Cannot figure out how to pull timezone from sharepoint
            startDate = startDate.ToUniversalTime();
            endDate = endDate.ToUniversalTime();
            logger.Info(String.Format($"Checking for {fileNameSubstring}  {fileExtension} files modified between {startDate} and {endDate}. {jobName}"));
            //Func<(DateTime, DateTime), bool> q = ((a,b)) => { return a == b};

            using (var clientContext = new ClientContext(Url))
            {
                // Set the credentials
                clientContext.Credentials = _credentials;

                // Query for files in the Sharepoint target folder.
                var qry = new CamlQuery();
                qry.ViewXml = "<View Scope='RecursiveAll'>" +
                                    "<Query>" +
                                        "<Where>" +
                                            "<Eq>" +
                                                "<FieldRef Name='FSObjType' />" +
                                                "<Value Type='Integer'>0</Value>" +
                                            "</Eq>" +
                                        "</Where>" +
                                        "<OrderBy>" +
                                            "<FieldRef Name='Modified' Ascending = 'false' />" +
                                        "</OrderBy>" +
                                    "</Query>" +
                              "</View>";
                var list = clientContext.Web.Lists.GetByTitle(SharePointFolderName);
                var items = list.GetItems(qry);
                clientContext.Load(items);
                clientContext.ExecuteQuery();

                // Look for files that match the filename pattern and have been modified within a certain date range.
                // Then copy those files to a destination folder.
                var filesToRet = false;
                Regex regexFileRefPattern = new Regex($@".*{fileNameSubstring}.*{fileExtension}",RegexOptions.IgnoreCase);
                foreach (var item in items)
                {
                    var fileRef = (string)item["FileRef"];
                    var fileName = Path.GetFileName(fileRef);
                    var fooDate = (DateTime)item["Modified"];
                    Match match = regexFileRefPattern.Match(fileRef);

                    if (!match.Success)
                    {
                        continue;
                    }

                    if (CheckFile(fileName))
                    {
                        int x = 1;
                    }

                    if (CheckFile(fileName) && startDate <= (DateTime)item["Modified"] && (DateTime)item["Modified"] <= endDate)
                    {
                        logger.Info(String.Format("I found a new file! Filename is {0}. JobName = {1}.", fileName, jobName));
                        var filePath = Path.Combine(DownloadPath, fileName);
                        var fileInfo = Microsoft.SharePoint.Client.File.OpenBinaryDirect(clientContext, fileRef);
                        using (var fileStream = System.IO.File.Create(filePath))
                        {
                            fileInfo.Stream.CopyTo(fileStream);
                            filesToRet = true;
                        }
                    }
                }
                return filesToRet;
            }
        }

    }
}


//
// Code which could possibly be used in the CAML query to filter by modified date. Derek couldn't get it
// to work, however, so we are excising it and storing it at the end.
//
// "< Lt >"+
//      "< FieldRef Name = 'Modified' />" +
//      "< Value Type = 'DateTime' >= "+ startDate.ToString("yyyy-MM-ddTHH:mm:ssZ") + " </ Value >" +
// "</ Lt >" +
// "< Lt >"+
//      "< FieldRef Name = 'Modified' />" +
//      "< Value Type = 'DateTime' <= "+ endDate.ToString("yyyy-MM-ddTHH:mm:ssZ") + " </ Value >" +
// "</ Lt >" +
//