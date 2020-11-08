using OneSTools.EventLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogStorage : IDisposable
    {
        Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default);
        Task WriteEventLogDataAsync<T>(List<T> entities, CancellationToken cancellationToken = default) where T : class, IEventLogItem;
    }
}
