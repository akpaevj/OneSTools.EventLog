using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Transactions;

namespace OneSTools.EventLog
{
    public class EventLogItem : IEventLogItem
    {
        [Required]
        public string FileName { get; set; } = "";
        public long EndPosition { get; set; } = 0;
        public DateTime DateTime { get; set; } = DateTime.MinValue;
        [Required]
        public string TransactionStatus { get; set; } = "";
        public DateTime? TransactionDateTime { get; set; } = DateTime.MinValue;
        public int TransactionNumber { get; set; } = 0;
        [Required]
        public string UserUuid { get; set; } = "";
        [Required]
        public string User { get; set; } = "";
        [Required]
        public string Computer { get; set; } = "";
        [Required]
        public string Application { get; set; } = "";
        public int Connection { get; set; } = 0;
        [Required]
        public string Event { get; set; } = "";
        [Required]
        public string Severity { get; set; } = "";
        [Required]
        public string Comment { get; set; } = "";
        [Required]
        public string MetadataUuid { get; set; } = "";
        [Required]
        public string Metadata { get; set; } = "";
        [Required]
        public string Data { get; set; } = "";
        [Required]
        public string DataPresentation { get; set; } = "";
        [Required]
        public string Server { get; set; } = "";
        public int MainPort { get; set; } = 0;
        public int AddPort { get; set; } = 0;
        public int Session { get; set; } = 0;
    }
}
