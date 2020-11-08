using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Transactions;

namespace OneSTools.EventLog
{
    public class EventLogItem
    {
        public long Id { get; internal set; }
        [Required]
        public string FileName { get; internal set; } = "";
        public long EndPosition { get; internal set; } = 0;
        public DateTime DateTime { get; internal set; } = DateTime.MinValue;
        [Required]
        public string TransactionStatus { get; internal set; } = "";
        public DateTime TransactionDateTime { get; internal set; } = DateTime.MinValue;
        public int TransactionNumber { get; internal set; } = 0;
        [Required]
        public string UserUuid { get; internal set; } = "";
        [Required]
        public string User { get; internal set; } = "";
        [Required]
        public string Computer { get; internal set; } = "";
        [Required]
        public string Application { get; internal set; } = "";
        public int Connection { get; internal set; } = 0;
        [Required]
        public string Event { get; internal set; } = "";
        [Required]
        public string Severity { get; internal set; } = "";
        [Required]
        public string Comment { get; internal set; } = "";
        [Required]
        public string MetadataUuid { get; internal set; } = "";
        [Required]
        public string Metadata { get; internal set; } = "";
        [Required]
        public string Data { get; internal set; } = "";
        [Required]
        public string DataPresentation { get; internal set; } = "";
        [Required]
        public string Server { get; internal set; } = "";
        public int MainPort { get; internal set; } = 0;
        public int AddPort { get; internal set; } = 0;
        public int Session { get; internal set; } = 0;
    }
}
