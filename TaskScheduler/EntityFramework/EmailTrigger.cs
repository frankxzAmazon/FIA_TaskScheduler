//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TaskScheduler.EntityFramework
{
    using System;
    using System.Collections.Generic;
    
    public partial class EmailTrigger
    {
        public string TriggerID { get; set; }
        public string EmailAddress { get; set; }
        public string MonitorFolder { get; set; }
        public string SubjectSubstring { get; set; }
        public bool SubjectIsExactMatch { get; set; }
        public string FileNameSubstring { get; set; }
        public string FileExtension { get; set; }
        public bool FileNameIsExactMatch { get; set; }
        public string MoveFolder { get; set; }
        public short Priority { get; set; }
        public bool ToDownload { get; set; }
        public bool ToAddTimestamp { get; set; }
        public string SenderEmailAddress { get; set; }
        public bool SenderIsExactMatch { get; set; }
    }
}
