using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogExporter<T> : IEventLogExporter<T> where T : class, IEventLogItem, new()
    {
        private readonly ILogger<EventLogExporter<T>> _logger;
        private readonly IEventLogStorage<T> _storage;
        private string _logFolder;
        private bool _liveMode;
        private EventLogReader<T> _eventLogReader;
        private List<T> _entities;

        public EventLogExporter(ILogger<EventLogExporter<T>> logger, IEventLogStorage<T> storage)
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
                    _entities = new List<T>(portion);
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

        public async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                var (FileName, EndPosition) = await _storage.ReadEventLogPositionAsync(stoppingToken);

                if (FileName == string.Empty)
                    _eventLogReader = new EventLogReader<T>(_logFolder, _liveMode, "", 0, 1000);
                else
                    _eventLogReader = new EventLogReader<T>(_logFolder, _liveMode, FileName + ".lgp", EndPosition, 1000);

                while (!stoppingToken.IsCancellationRequested)
                {
                    bool forceSending = false;
                    T item = default;

                    try
                    {
                        item = _eventLogReader.ReadNextEventLogItem(stoppingToken);
                    }
                    catch (EventLogReaderTimeoutException)
                    {
                        forceSending = true;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    if (item != null)
                        _entities.Add(item);

                    if ((forceSending && _entities.Count > 0) || _entities.Count == _entities.Capacity)
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
