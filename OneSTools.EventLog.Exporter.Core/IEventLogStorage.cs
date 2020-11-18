using OneSTools.EventLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogStorage<T> : IDisposable where T : class, IEventLogItem, new()
    {
        Task<(string FileName, long EndPosition, long LgfEndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default);
        Task WriteEventLogDataAsync(List<T> entities, CancellationToken cancellationToken = default);
    }
}
