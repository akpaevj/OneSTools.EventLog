using System;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public interface IEventLogExporter : IDisposable
    {
        Task StartAsync(string logFolder, int portion, bool liveMode = false, CancellationToken cancellationToken = default);
        Task ExecuteAsync(CancellationToken stoppingToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}