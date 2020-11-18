using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog;
using OneSTools.EventLog.Exporter.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogExporterService<T> : BackgroundService where T : class, IEventLogItem, new()
    {
        ILogger<EventLogExporterService<T>> _logger;
        private readonly IEventLogExporter<T> _exporter;
        private bool disposedValue;

        public EventLogExporterService(ILogger<EventLogExporterService<T>> logger, IEventLogExporter<T> exporter)
        {
            _logger = logger;
            _exporter = exporter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _exporter.StartAsync(stoppingToken);
            }
            catch (TaskCanceledException ex) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to execute EventLogExporter");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                _exporter?.Dispose();

                disposedValue = true;
            }
        }

        ~EventLogExporterService()
        {
            Dispose(disposing: false);
        }

        public override void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
