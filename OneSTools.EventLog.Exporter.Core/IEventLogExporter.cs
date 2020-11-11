using System;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogExporter<T> : IDisposable where T : class, IEventLogItem, new()
    {
        Task StartAsync(CancellationToken cancellationToken = default);
    }
}