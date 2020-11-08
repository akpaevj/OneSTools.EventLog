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
    public class EventLogExporterService<T> : BackgroundService where T : class, IEventLogItem
    {
        private string _logFolder;
        private int _portion;
        private readonly ILogger<EventLogExporterService<T>> _logger;
        private readonly IEventLogExporter _eventLogExporter;

        public EventLogExporterService(IConfiguration configuration, ILogger<EventLogExporterService<T>> logger, IEventLogExporter eventLogExporter)
        {
            _logger = logger;
            _eventLogExporter = eventLogExporter;

            var exporterSection = configuration.GetSection("Exporter");

            _logFolder = exporterSection.GetValue("LogFolder", "");

            if (_logFolder == string.Empty)
            {
                var msg = "Log's folder is not set";
                _logger.LogCritical(msg);

                throw new Exception(msg);
            }

            _portion = exporterSection.GetValue("Portion", 10000);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _eventLogExporter.StartAsync(_logFolder, _portion, true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start EventLogExporter");

                await StopAsync(cancellationToken);
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _eventLogExporter.ExecuteAsync<T>(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute EventLogExporter");

                await StopAsync(stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _eventLogExporter.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop EventLogExporter");
            }

            await base.StopAsync(cancellationToken);
        }
    }
}