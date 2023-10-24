using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogStorage : IDisposable
    {
        Task<EventLogPosition> ReadEventLogPositionAsync(CancellationToken cancellationToken = default, string filename = "");
        Task WriteEventLogDataAsync(List<EventLogItem> entities, CancellationToken cancellationToken = default);
    }
}