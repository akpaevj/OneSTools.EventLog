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
        private List<EventLogItem> _entities;

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
                    _entities = new List<EventLogItem>(portion);
                    _liveMode = liveMode;
                }, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.ToString());
                throw ex;
            }

            _logger.LogInformation("EventLogExporter started");
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                var eventLogPosition = await _storage.ReadEventLogPositionAsync(stoppingToken);

                if (eventLogPosition == null)
                    _eventLogReader = new EventLogReader(_logFolder, _liveMode);
                else
                    _eventLogReader = new EventLogReader(_logFolder, _liveMode, eventLogPosition.LgpFileName + ".lgp", eventLogPosition.LgpFilePosition);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var item = _eventLogReader.ReadNextEventLogItem(stoppingToken);

                    if (item != null)
                    {
                        _entities.Add(item);
                    }

                    if (_entities.Count == _entities.Capacity)
                    {
                        var currentEventLogPosition = new EventLogPosition
                        {
                            LgpFileName = _eventLogReader.CurrentLgpFileName,
                            LgpFilePosition = _eventLogReader.CurrentLgpFilePosition
                        };

                        await _storage.WriteEventLogDataAsync(currentEventLogPosition, _entities, stoppingToken);

                        _logger.LogInformation($"{DateTime.Now.ToString("hh:mm:ss.fffff")}: EventLogExporter has written {_entities.Count} items");

                        _entities.Clear();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.ToString());
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
