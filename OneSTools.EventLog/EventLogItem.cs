using System;
using System.Transactions;

namespace OneSTools.EventLog
{
    public class EventLogItem
    {
        public DateTime DateTime { get; set; }
        public string TransactionStatus { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public int TransactionNumber { get; set; }
        public string UserUuid { get; set; }
        public string User { get; set; }
        public string Computer { get; set; }
        public string Application { get; set; }
        public int Connection { get; set; }
        public string Event { get; set; }
        public string Severity { get; set; }
        public string Comment { get; set; }
        public string MetadataUuid { get; set; }
        public string Metadata { get; set; }
        public string Data { get; set; }
        public string DataUuid { get; set; }
        public string DataPresentation { get; set; }
        public string Server { get; set; }
        public int MainPort { get; set; }
        public int AddPort { get; set; }
        public int Session { get; set; }
    }
}
