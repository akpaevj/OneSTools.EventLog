using System;
using System.Transactions;

namespace OneSTools.EventLog
{
    public class EventLogItem
    {
        public DateTime DateTime { get; internal set; }
        public string TransactionStatus { get; internal set; }
        public DateTime TransactionDateTime { get; internal set; }
        public int TransactionNumber { get; internal set; }
        public string UserUuid { get; internal set; }
        public string User { get; internal set; }
        public string Computer { get; internal set; }
        public string Application { get; internal set; }
        public int Connection { get; internal set; }
        public string Event { get; internal set; }
        public string Severity { get; internal set; }
        public string Comment { get; internal set; }
        public string MetadataUuid { get; internal set; }
        public string Metadata { get; internal set; }
        public string Data { get; internal set; }
        public string DataUuid { get; internal set; }
        public string DataPresentation { get; internal set; }
        public string Server { get; internal set; }
        public int MainPort { get; internal set; }
        public int AddPort { get; internal set; }
        public int Session { get; internal set; }
    }
}
