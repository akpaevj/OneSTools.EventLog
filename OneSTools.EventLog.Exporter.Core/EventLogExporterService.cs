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
    public class EventLogExporterService : BackgroundService
    {
        private readonly ILogger<EventLogExporterService> _logger;
        private readonly EventLogExporter _exporter;
        private bool _disposedValue;

        public EventLogExporterService(ILogger<EventLogExporterService> logger, EventLogExporter exporter)
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
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to execute EventLogExporter");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {

                }

                _exporter?.Dispose();

                _disposedValue = true;
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
