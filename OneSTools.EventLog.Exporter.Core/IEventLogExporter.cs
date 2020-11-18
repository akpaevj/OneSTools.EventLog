using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogExporter<T> : IDisposable where T : class, IEventLogItem, new()
    {
        Task StartAsync(CancellationToken cancellationToken = default);
    }
}
