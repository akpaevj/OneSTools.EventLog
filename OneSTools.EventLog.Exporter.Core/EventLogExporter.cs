using System;
using System.Diagnostics;
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
    public class EventLogExporterSettings
    {
        public string LogFolder { get; set; } = "";
        public int Portion { get; set; } = 10000;
        public DateTimeZone TimeZone { get; set; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        public int WritingMaxDop { get; set; } = 1;
        public int CollectedFactor { get; set; } = 2;
        public int ReadingTimeout { get; set; } = 1;
        public bool LoadArchive { get; set; } = false;
    }

    public class EventLogExporter
    {
        private readonly ILogger<EventLogExporter> _logger;
        private readonly IEventLogStorage _storage;

        // Exporter settings
        private readonly string _logFolder;
        private readonly int _portion;
        private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        private readonly int _writingMaxdop;
        private readonly int _collectedFactor;
        private readonly bool _loadArchive;
        private readonly int _readingTimeout;

        private string _currentLgpFile;

        // Dataflow blocks
        private EventLogReader _eventLogReader;
        private ActionBlock<EventLogItem[]> _writeBlock;
        private BatchBlock<EventLogItem> _batchBlock;

        private bool disposedValue;

        public EventLogExporter(EventLogExporterSettings settings, IEventLogStorage storage, ILogger<EventLogExporter> logger = null)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = settings.LogFolder;
            _portion = settings.Portion;
            _writingMaxdop = settings.WritingMaxDop;
            _collectedFactor = settings.CollectedFactor;
            _loadArchive = settings.LoadArchive;
            _timeZone = settings.TimeZone;
            _readingTimeout = settings.ReadingTimeout;

            CheckSettings();
        }

        public EventLogExporter(ILogger<EventLogExporter> logger, IConfiguration configuration, IEventLogStorage storage)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = configuration.GetValue("Exporter:LogFolder", "");
            _portion = configuration.GetValue("Exporter:Portion", 10000);
            _writingMaxdop = configuration.GetValue("Exporter:WritingMaxDegreeOfParallelism", 1);
            _collectedFactor = configuration.GetValue("Exporter:CollectedFactor", 2);
            _loadArchive = configuration.GetValue("Exporter:LoadArchive", false);
            _readingTimeout = configuration.GetValue("Exporter:ReadingTimeout", 1);

            var timeZone = configuration.GetValue("Exporter:TimeZone", "");

            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone);
                if (zone is null)
                    throw new Exception($"\"{timeZone}\" is unknown time zone");

                _timeZone = zone;
            }

            CheckSettings();
        }

        private void CheckSettings()
        {
            if (_logFolder == string.Empty)
                throw new Exception("Event log folder is not specified");

            if (!Directory.Exists(_logFolder))
                throw new Exception($"Event log folder ({_logFolder}) doesn't exist");

            if (_writingMaxdop <= 0)
                throw new Exception($"WritingMaxDegreeOfParallelism cannot be equal to or less than 0");

            if (_collectedFactor <= 0)
                throw new Exception($"CollectedFactor cannot be equal to or less than 0");
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation($"Log folder: {_logFolder}");

            if (_loadArchive)
                _logger?.LogWarning($"\"Load archive\" mode enabled");

            _logger?.LogInformation($"Portion per request: {_portion}");

            InitializeDataflow(cancellationToken);

            try
            {
                var settings = await GetReaderSettingsAsync(cancellationToken);
                _eventLogReader = new EventLogReader(settings);

                while (!cancellationToken.IsCancellationRequested)
                {
                    bool forceSending = false;

                    EventLogItem item = null;

                    try
                    {
                        item = _eventLogReader.ReadNextEventLogItem(cancellationToken);
                    }
                    catch (EventLogReaderTimeoutException)
                    {
                        forceSending = true;
                    }
                    catch (Exception ex)
                    {
                        _batchBlock.Complete();
                        throw ex;
                    }

                    if (item != null)
                    {
                        await SendAsync(_batchBlock, item);

                        if (!string.IsNullOrEmpty(_eventLogReader.LgpFileName) && _currentLgpFile != _eventLogReader.LgpFileName)
                        {
                            _logger?.LogInformation($"Reader started reading {_eventLogReader.LgpFileName}");

                            _currentLgpFile = _eventLogReader.LgpFileName;
                        }
                    }
                    else if (!settings.LiveMode)
                        forceSending = true;

                    if (forceSending)
                        _batchBlock.TriggerBatch();
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void InitializeDataflow(CancellationToken cancellationToken = default)
        {
            var writeBlockSettings = new ExecutionDataflowBlockOptions()
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = _collectedFactor,
                MaxDegreeOfParallelism = _writingMaxdop
            };

            var batchBlockSettings = new GroupingDataflowBlockOptions()
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = _portion * _collectedFactor
            };

            _writeBlock = new ActionBlock<EventLogItem[]>(c => _storage.WriteEventLogDataAsync(c.ToList(), cancellationToken), writeBlockSettings);
            _batchBlock = new BatchBlock<EventLogItem>(_portion, batchBlockSettings);

            _batchBlock.LinkTo(_writeBlock, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private async Task<EventLogReaderSettings> GetReaderSettingsAsync(CancellationToken cancellationToken = default)
        {
            var eventLogReaderSettings = new EventLogReaderSettings
            {
                LogFolder = _logFolder,
                LiveMode = true,
                ReadingTimeout = _readingTimeout * 1000,
                TimeZone = _timeZone
            };

            if (!_loadArchive)
            {
                var position = await _storage.ReadEventLogPositionAsync(cancellationToken);

                if (position != null)
                {
                    var lgpFilePath = Path.Combine(_logFolder, position.FileName);

                    if (!File.Exists(lgpFilePath))
                        _logger?.LogWarning($"Lgp file ({lgpFilePath}) doesn't exist. The reading will be started from the first found file");
                    else
                    {
                        eventLogReaderSettings.LgpFileName = position.FileName;
                        eventLogReaderSettings.LgpStartPosition = position.EndPosition;
                        eventLogReaderSettings.LgfStartPosition = position.LgfEndPosition;
                        eventLogReaderSettings.ItemId = position.Id;

                        _logger?.LogInformation($"File {position.FileName} will be read from {position.EndPosition} position, LGF file will be read from {position.LgfEndPosition} position");
                    }
                }
                else
                    _logger?.LogInformation($"There're no log items in the database, first found log file will be read from 0 position");
            }
            else
            {
                _logger?.LogWarning($"LoadArchive parameter is true. Live mode will not be used");

                eventLogReaderSettings.LiveMode = false;
            }

            return eventLogReaderSettings;
        }

        private async Task SendAsync(ITargetBlock<EventLogItem> nextBlock, EventLogItem item, CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested && !nextBlock.Completion.IsFaulted)
            {
                if (await nextBlock.SendAsync(item))
                    break;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _storage?.Dispose();
                }

                _eventLogReader?.Dispose();

                disposedValue = true;
            }
        }

        ~EventLogExporter()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}