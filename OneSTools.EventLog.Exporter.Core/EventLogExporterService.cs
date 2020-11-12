using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
        private readonly IEventLogStorage<T> _storage;
        private string _logFolder;
        private int _portion;
        private EventLogReader<T> _eventLogReader;
        private ActionBlock<T[]> _writeBlock;
        private BatchBlock<T> _batchBlock;

        public EventLogExporterService(ILogger<EventLogExporterService<T>> logger, IConfiguration configuration, IEventLogStorage<T> storage)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = configuration.GetValue("Exporter:LogFolder", "");
            if (_logFolder == string.Empty)
            {
                _logger.LogCritical("Event log folder is not specified");
                throw new Exception("Event log folder is not specified");
            }

            _logger.LogInformation($"Log folder: {_logFolder}");

            if (!Directory.Exists(_logFolder))
                throw new Exception($"Event log folder ({_logFolder}) doesn't exist");

            _portion = configuration.GetValue("Exporter:Portion", 10000);

            _logger.LogInformation($"Portion per request: {_portion}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _writeBlock = new ActionBlock<T[]>(c => _storage.WriteEventLogDataAsync(c.ToList(), stoppingToken), new ExecutionDataflowBlockOptions()
            {
                CancellationToken = stoppingToken,
                BoundedCapacity = 2
            });
            _batchBlock = new BatchBlock<T>(_portion, new GroupingDataflowBlockOptions()
            {
                CancellationToken = stoppingToken,
                BoundedCapacity = _portion * 2
            });

            _batchBlock.LinkTo(_writeBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            try
            {
                var (FileName, EndPosition) = await _storage.ReadEventLogPositionAsync(stoppingToken);

                if (FileName == string.Empty)
                    _eventLogReader = new EventLogReader<T>(_logFolder, true, "", 0, 1000);
                else
                    _eventLogReader = new EventLogReader<T>(_logFolder, true, FileName + ".lgp", EndPosition, 1000);

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

                        _logger.LogDebug($"Sending items will be forced");
                    }
                    catch (Exception ex)
                    {
                        _batchBlock.Complete();
                        throw ex;
                    }

                    if (item != null)
                        await SendAsync(_batchBlock, item);

                    if (forceSending)
                        _batchBlock.TriggerBatch();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to execute EventLogExporter");
                throw ex;
            }
        }

        private async Task SendAsync(ITargetBlock<T> nextBlock, T item, CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await nextBlock.SendAsync(item))
                    break;
            }
        }

        public override void Dispose()
        {
            _storage?.Dispose();
            _eventLogReader?.Dispose();

            base.Dispose();
        }
    }
}