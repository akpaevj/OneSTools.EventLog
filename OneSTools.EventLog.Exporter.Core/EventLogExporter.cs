using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogExporter : IDisposable
    {
        private readonly int _collectedFactor;
        private readonly bool _loadArchive;

        // Exporter settings
        private readonly string _logFolder;
        private readonly ILogger<EventLogExporter> _logger;
        private readonly int _portion;
        private readonly int _readingTimeout;
        private readonly IEventLogStorage _storage;
        private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        private readonly int _writingMaxDop;
        private BatchBlock<EventLogItem> _batchBlock;
        private readonly DateTime _skipEventsBeforeDate;

        private string _currentLgpFile;

        private bool _disposedValue;

        // DataFlow blocks
        private EventLogReader _eventLogReader;
        private ActionBlock<EventLogItem[]> _writeBlock;

        public EventLogExporter(EventLogExporterSettings settings, IEventLogStorage storage,
            ILogger<EventLogExporter> logger = null)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = settings.LogFolder;
            _portion = settings.Portion;
            _writingMaxDop = settings.WritingMaxDop;
            _collectedFactor = settings.CollectedFactor;
            _loadArchive = settings.LoadArchive;
            _timeZone = settings.TimeZone;
            _readingTimeout = settings.ReadingTimeout;
            _skipEventsBeforeDate = settings.SkipEventsBeforeDate;

            CheckSettings();
        }

        public EventLogExporter(ILogger<EventLogExporter> logger, IConfiguration configuration,
            IEventLogStorage storage)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = configuration.GetValue("Exporter:LogFolder", "");
            _portion = configuration.GetValue("Exporter:Portion", 10000);
            _writingMaxDop = configuration.GetValue("Exporter:WritingMaxDegreeOfParallelism", 1);
            _collectedFactor = configuration.GetValue("Exporter:CollectedFactor", 2);
            _loadArchive = configuration.GetValue("Exporter:LoadArchive", false);
            _readingTimeout = configuration.GetValue("Exporter:ReadingTimeout", 1);
            _skipEventsBeforeDate = configuration.GetValue("Exporter:SkipEventsBeforeDate", DateTime.MinValue);

            var timeZone = configuration.GetValue("Exporter:TimeZone", "");

            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone);

                _timeZone = zone ?? throw new Exception($"\"{timeZone}\" is unknown time zone");
            }

            CheckSettings();
        }

        private void CheckSettings()
        {
            if (_logFolder == string.Empty)
                throw new Exception("Event log folder is not specified");

            if (!Directory.Exists(_logFolder))
                throw new Exception($"Event log folder ({_logFolder}) doesn't exist");

            if (_writingMaxDop <= 0)
                throw new Exception("WritingMaxDegreeOfParallelism cannot be equal to or less than 0");

            if (_collectedFactor <= 0)
                throw new Exception("CollectedFactor cannot be equal to or less than 0");
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"Log folder: {_logFolder}");

            if (_loadArchive)
                _logger?.LogWarning("\"Load archive\" mode enabled");

            _logger?.LogInformation($"Portion per request: {_portion}");

            InitializeDataflow(cancellationToken);

            try
            {
                var settings = await GetReaderSettingsAsync(cancellationToken);
                _eventLogReader = new EventLogReader(settings);

                while (!cancellationToken.IsCancellationRequested && !_writeBlock.Completion.IsCompleted)
                {
                    var forceSending = false;

                    EventLogItem item = null;

                    try
                    {
                        item = _eventLogReader.ReadNextEventLogItem(cancellationToken);
                    }
                    catch (EventLogReaderTimeoutException)
                    {
                        forceSending = true;
                    }
                    catch (Exception)
                    {
                        _batchBlock.Complete();
                        throw;
                    }

                    if (item != null)
                    {
                        await SendAsync(_batchBlock, item, cancellationToken);

                        if (!string.IsNullOrEmpty(_eventLogReader.LgpFileName) &&
                            _currentLgpFile != _eventLogReader.LgpFileName)
                        {
                            _logger?.LogInformation($"Reader started reading {_eventLogReader.LgpFileName}");

                            _currentLgpFile = _eventLogReader.LgpFileName;
                        }
                    }
                    else if (!settings.LiveMode)
                    {
                        forceSending = true;
                    }

                    if (forceSending)
                        _batchBlock.TriggerBatch();
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void InitializeDataflow(CancellationToken cancellationToken = default)
        {
            var writeBlockSettings = new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = _collectedFactor,
                MaxDegreeOfParallelism = _writingMaxDop
            };

            var batchBlockSettings = new GroupingDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = _portion * _collectedFactor
            };

            _writeBlock = new ActionBlock<EventLogItem[]>(async c =>
            {
                try
                {
                    await _storage.WriteEventLogDataAsync(c.ToList(), cancellationToken);
                }
                catch (Exception)
                {
                    _batchBlock.Complete();
                    _writeBlock.Complete();
                    throw;
                }
            },
            writeBlockSettings);

            _batchBlock = new BatchBlock<EventLogItem>(_portion, batchBlockSettings);
            _batchBlock.LinkTo(_writeBlock, new DataflowLinkOptions { PropagateCompletion = true });
        }

        private async Task<EventLogReaderSettings> GetReaderSettingsAsync(CancellationToken cancellationToken = default)
        {
            var eventLogReaderSettings = new EventLogReaderSettings
            {
                LogFolder = _logFolder,
                LiveMode = true,
                ReadingTimeout = _readingTimeout * 1000,
                TimeZone = _timeZone,
                SkipEventsBeforeDate = _skipEventsBeforeDate
            };

            if (!_loadArchive)
            {
                var position = await _storage.ReadEventLogPositionAsync(cancellationToken);

                if (position != null)
                {
                    var lgpFilePath = Path.Combine(_logFolder, position.FileName);

                    if (!File.Exists(lgpFilePath))
                    {
                        _logger?.LogWarning(
                            $"Lgp file ({lgpFilePath}) doesn't exist. The reading will be started from the first found file");
                    }
                    else
                    {
                        eventLogReaderSettings.LgpFileName = position.FileName;
                        eventLogReaderSettings.LgpStartPosition = position.EndPosition;
                        eventLogReaderSettings.LgfStartPosition = position.LgfEndPosition;
                        eventLogReaderSettings.ItemId = position.Id;

                        _logger?.LogInformation(
                            $"File {position.FileName} will be read from {position.EndPosition} position, LGF file will be read from {position.LgfEndPosition} position");
                    }
                }
                else
                {
                    _logger?.LogInformation(
                        "There're no log items in the database, first found log file will be read from 0 position");
                }
            }
            else
            {
                _logger?.LogWarning("LoadArchive parameter is true. Live mode will not be used");

                eventLogReaderSettings.LiveMode = false;
            }

            return eventLogReaderSettings;
        }

        private static async Task SendAsync(ITargetBlock<EventLogItem> nextBlock, EventLogItem item,
            CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested && !nextBlock.Completion.IsCompleted)
                if (await nextBlock.SendAsync(item, stoppingToken))
                    break;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
                return;

            if (disposing) _storage?.Dispose();

            _eventLogReader?.Dispose();

            _disposedValue = true;
        }

        ~EventLogExporter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}