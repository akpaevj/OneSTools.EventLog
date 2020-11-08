using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogExporter : IEventLogExporter
    {
        private readonly ILogger<EventLogExporter> _logger;
        private readonly IEventLogStorage _storage;
        private string _logFolder;
        private bool _liveMode;
        private EventLogReader _eventLogReader;
        private List<IEventLogItem> _entities;

        public EventLogExporter(ILogger<EventLogExporter> logger, IEventLogStorage storage)
        {
            _logger = logger;
            _storage = storage;
        }

        public async Task StartAsync(string logFolder, int portion, bool liveMode = false, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() =>
                {
                    _logFolder = logFolder;
                    _entities = new List<IEventLogItem>(portion);
                    _liveMode = liveMode;
                }, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to start EventLogExporter");
                throw ex;
            }

            _logger.LogInformation("EventLogExporter started");
        }

        public async Task ExecuteAsync<T>(CancellationToken stoppingToken = default) where T : class, IEventLogItem
        {
            try
            {
                var (FileName, EndPosition) = await _storage.ReadEventLogPositionAsync(stoppingToken);

                if (FileName == string.Empty)
                    _eventLogReader = new EventLogReader(_logFolder, _liveMode);
                else
                    _eventLogReader = new EventLogReader(_logFolder, _liveMode, FileName + ".lgp", EndPosition);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var item = _eventLogReader.ReadNextEventLogItem<T>(stoppingToken);

                    if (item != null)
                    {
                        _entities.Add(item);
                    }

                    if (_entities.Count == _entities.Capacity)
                    {
                        await _storage.WriteEventLogDataAsync(_entities, stoppingToken);

                        _logger.LogInformation($"{DateTime.Now:hh:mm:ss.fffff}: EventLogExporter has written {_entities.Count} items");

                        _entities.Clear();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to execute EventLogExporter");
                throw ex;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (_eventLogReader != null)
                        _eventLogReader.Dispose();
                }, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                throw ex;
            }

            _logger.LogInformation($"EventLogExporter stopped");
        }

        public void Dispose()
        {
            if (_storage != null)
                _storage.Dispose();

            if (_eventLogReader != null)
                _eventLogReader.Dispose();
        }
    }
}
