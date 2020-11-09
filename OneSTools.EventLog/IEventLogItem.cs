using System;

namespace OneSTools.EventLog
{
    public interface IEventLogItem
    {
        int AddPort { get; set; }
        string Application { get; set; }
        string Comment { get; set; }
        string Computer { get; set; }
        int Connection { get; set; }
        string Data { get; set; }
        string DataPresentation { get; set; }
        DateTime DateTime { get; set; }
        long EndPosition { get; set; }
        string Event { get; set; }
        string FileName { get; set; }
        int MainPort { get; set; }
        string Metadata { get; set; }
        string MetadataUuid { get; set; }
        string Server { get; set; }
        int Session { get; set; }
        string Severity { get; set; }
        DateTime TransactionDateTime { get; set; }
        int TransactionNumber { get; set; }
        string TransactionStatus { get; set; }
        string User { get; set; }
        string UserUuid { get; set; }
    }
}