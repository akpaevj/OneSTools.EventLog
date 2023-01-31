using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog.Exporter.Core;

namespace OneSTools.EventLog.Exporter
{
    public class EventLogExporterService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EventLogExporterService> _logger;
        private bool _disposedValue;

        public EventLogExporterService(IServiceProvider serviceProvider, ILogger<EventLogExporterService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var exporter = _serviceProvider.GetService<EventLogExporter>();
                    await exporter.StartAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, "Failed to execute EventLogExporter");
                }
                await Task.Delay(5000);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = true;
            }
        }

        ~EventLogExporterService()
        {
            Dispose(false);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}