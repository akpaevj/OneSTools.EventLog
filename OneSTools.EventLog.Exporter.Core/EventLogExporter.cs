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
    public class EventLogExporter<T> : IEventLogExporter<T> where T : class, IEventLogItem, new()
    {
        public static int DEFAULT_PORTION = 10000;

        private readonly ILogger<EventLogExporter<T>> _logger;
        private readonly IEventLogStorage<T> _storage;
        private string _logFolder;
        private int _portion;
        private bool _loadArchive;
        private DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        private string _currentLgpFile;
        private EventLogReader<T> _eventLogReader;
        private ActionBlock<T[]> _writeBlock;
        private BatchBlock<T> _batchBlock;
        private bool disposedValue;

        public EventLogExporter(ILogger<EventLogExporter<T>> logger, IEventLogStorage<T> storage, string logFolder, int portion, string timeZone, bool loadArchive = false)
        {
            _logger = logger;
            _storage = storage;

            _logFolder = logFolder;
            if (_logFolder == string.Empty)
                throw new Exception("Event log folder is not specified");

            if (!Directory.Exists(_logFolder))
                throw new Exception($"Event log folder ({_logFolder}) doesn't exist");

            _portion = portion;

            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone);
                if (zone is null)
                    throw new Exception($"\"{timeZone}\" is unknown time zone");

                _timeZone = zone;
            }

            _loadArchive = loadArchive;
        }

        public EventLogExporter(ILogger<EventLogExporter<T>> logger, IConfiguration configuration, IEventLogStorage<T> storage) : this(
            logger,
            storage,
            configuration.GetValue("Exporter:LogFolder", ""),
            configuration.GetValue("Exporter:Portion", DEFAULT_PORTION),
            configuration.GetValue("Exporter:TimeZone", ""),
            configuration.GetValue("Exporter:LoadArchive", false)
            )
        {

        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Log folder: {_logFolder}");

            if (_loadArchive)
                _logger.LogWarning($"\"Load archive\" mode enabled");

            _logger.LogInformation($"Portion per request: {_portion}");

            InitializeDataflow(cancellationToken);

            try
            {
                var settings = await GetReaderSettingsAsync(cancellationToken);
                _eventLogReader = new EventLogReader<T>(settings);

                while (!cancellationToken.IsCancellationRequested)
                {
                    bool forceSending = false;

                    T item = default;

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
                            _logger.LogInformation($"Reader started reading {_eventLogReader.LgpFileName}");

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
                BoundedCapacity = 2
            };

            var batchBlockSettings = new GroupingDataflowBlockOptions()
            {
                CancellationToken = cancellationToken,
                BoundedCapacity = _portion * 2
            };

            _writeBlock = new ActionBlock<T[]>(c => _storage.WriteEventLogDataAsync(c.ToList(), cancellationToken), writeBlockSettings);
            _batchBlock = new BatchBlock<T>(_portion, batchBlockSettings);

            _batchBlock.LinkTo(_writeBlock, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private async Task<EventLogReaderSetings> GetReaderSettingsAsync(CancellationToken cancellationToken = default)
        {
            var eventLogReaderSettings = new EventLogReaderSetings
            {
                LogFolder = _logFolder,
                LiveMode = true,
                ReadingTimeout = 1000,
                TimeZone = _timeZone
            };

            if (!_loadArchive)
            {
                (string FileName, long EndPosition, long LgfEndPosition) = await _storage.ReadEventLogPositionAsync(cancellationToken);

                if (FileName != string.Empty)
                {
                    var lgpFilePath = Path.Combine(_logFolder, FileName);

                    if (!File.Exists(lgpFilePath))
                        _logger.LogWarning($"Lgp file ({lgpFilePath}) doesn't exist. The reading will be started from the first found file");
                    else
                    {
                        eventLogReaderSettings.LgpFileName = FileName;
                        eventLogReaderSettings.StartPosition = EndPosition;
                        eventLogReaderSettings.LgpStartPosition = LgfEndPosition;
                    }
                }
            }
            else
                eventLogReaderSettings.LiveMode = false;

            return eventLogReaderSettings;
        }

        private async Task SendAsync(ITargetBlock<T> nextBlock, T item, CancellationToken stoppingToken = default)
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