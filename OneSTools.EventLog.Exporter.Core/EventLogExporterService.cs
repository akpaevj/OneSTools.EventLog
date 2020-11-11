using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog.Exporter.Core;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogExporterService<T> : BackgroundService where T : class, IEventLogItem, new()
    {
        private readonly ILogger<EventLogExporterService<T>> _logger;
        private readonly IEventLogExporter<T> _eventLogExporter;

        public EventLogExporterService(ILogger<EventLogExporterService<T>> logger, IEventLogExporter<T> eventLogExporter)
        {
            _logger = logger;
            _eventLogExporter = eventLogExporter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _eventLogExporter.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute EventLogExporter");

                await StopAsync(stoppingToken);
            }
        }

        public override void Dispose()
        {
            _eventLogExporter?.Dispose();

            base.Dispose();
        }
    }
}