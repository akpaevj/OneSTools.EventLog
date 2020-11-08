using System;

namespace OneSTools.EventLog
{
    public interface IEventLogItem
    {
        int AddPort { get; }
        string Application { get; }
        string Comment { get; }
        string Computer { get; }
        int Connection { get; }
        string Data { get; }
        string DataPresentation { get; }
        DateTime DateTime { get; }
        long EndPosition { get; }
        string Event { get; }
        string FileName { get; }
        int MainPort { get; }
        string Metadata { get; }
        string MetadataUuid { get; }
        string Server { get; }
        int Session { get; }
        string Severity { get; }
        DateTime TransactionDateTime { get; }
        int TransactionNumber { get; }
        string TransactionStatus { get; }
        string User { get; }
        string UserUuid { get; }
    }
}