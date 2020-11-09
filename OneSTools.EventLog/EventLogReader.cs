using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog
{
    /// <summary>
    ///  Presents methods for reading 1C event log
    /// </summary>    
    public class EventLogReader<T> : IDisposable where T : class, IEventLogItem, new()
    {
        private ManualResetEvent _lgpChangedCreated;
        private readonly string _logFolder;
        private readonly int _readingTimeout;
        private readonly bool _liveMode;
        private readonly LgfReader _lgfReader;
        private LgpReader<T> _lgpReader;
        private FileSystemWatcher _lgpFilesWatcher;

        /// <summary>
        /// Current reader's "lgp" file name
        /// </summary>
        public string CurrentLgpFileName => _lgpReader.LgpFileName;
        /// <summary>
        /// Current position of the "lgp" file
        /// </summary>
        public long CurrentLgpFilePosition => _lgpReader.GetPosition();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="logFolder">1C event log's folder. Supported only lgf and lgp files (old version)</param>
        /// <param name="liveMode">Flag of "live" reading mode. In this mode it'll be waiting for a new event without returning a null element/param>
        /// <param name="lgpFileName">LGP file name that will be used for reading</param>
        /// <param name="startPosition">LGP file position that will be used for reading</param>
        /// <param name="readingTimout">Timeout of the reading next event. if this is set to -1 the reader will wait forever, 
        /// otherwise it'll throw exception when the timeout occurs</param>
        public EventLogReader(string logFolder, bool liveMode = false, string lgpFileName = "", long startPosition = 0, int readingTimout = -1)
        {
            _logFolder = logFolder;
            _readingTimeout = readingTimout;
            _liveMode = liveMode;
            _lgfReader = new LgfReader(Path.Combine(_logFolder, "1Cv8.lgf"));

            if (lgpFileName != string.Empty)
            {
                var file = Path.Combine(_logFolder, lgpFileName);

                _lgpReader = new LgpReader<T>(file, _lgfReader);
                _lgpReader.SetPosition(startPosition);
            }
        }

        /// <summary>
        /// The behaviour of the method depends on the mode of the reader. In the "live" mode it'll be waiting for an appearing of the new event item, otherwise It'll just return null
        /// </summary>
        /// <param name="cancellationToken">Token for interrupting of the reader</param>
        /// <returns></returns>
        public T ReadNextEventLogItem(CancellationToken cancellationToken = default)
        {
            if (_lgpReader == null)
                SetNextLgpReader();

            if (_liveMode && _lgpFilesWatcher == null)
                StartLgpFilesWatcher();

            T item = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                item = _lgpReader.ReadNextEventLogItem(cancellationToken);

                if (item == null)
                {
                    var newReader = SetNextLgpReader();

                    if (_liveMode)
                    {
                        if (!newReader)
                        {
                            _lgpChangedCreated.Reset();

                            var waitHandle = WaitHandle.WaitAny(new WaitHandle[] { _lgpChangedCreated, cancellationToken.WaitHandle }, _readingTimeout);

                            if (_readingTimeout != Timeout.Infinite && waitHandle == WaitHandle.WaitTimeout)
                                throw new EventLogReaderTimeoutException();

                            _lgpChangedCreated.Reset();
                        }
                    }
                    else
                    {
                        if (!newReader)
                            break;
                    }
                }
                else
                    break;
            }

            return item;
        }

        private bool SetNextLgpReader()
        {
            var currentReaderLastWriteDateTime = DateTime.MinValue;

            if (_lgpReader != null)
                currentReaderLastWriteDateTime = new FileInfo(_lgpReader.LgpPath).LastWriteTime;

            var files = Directory.GetFiles(_logFolder, "*.lgp");

            foreach (var file in files)
            {
                var writeDateTime = new FileInfo(file).LastWriteTime;

                if (writeDateTime > currentReaderLastWriteDateTime)
                {
                    if (_lgpReader != null)
                        _lgpReader.Dispose();

                    _lgpReader = new LgpReader<T>(file, _lgfReader);

                    return true;
                }
            }

            return false;
        }

        private void StartLgpFilesWatcher()
        {
            _lgpChangedCreated = new ManualResetEvent(false);

            _lgpFilesWatcher = new FileSystemWatcher(_logFolder, "*.lgp")
            {
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
            _lgpFilesWatcher.Changed += _lgpFilesWatcher_Changed;
            _lgpFilesWatcher.Created += _lgpFilesWatcher_Created;
            _lgpFilesWatcher.EnableRaisingEvents = true;
        }

        private void _lgpFilesWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
                _lgpChangedCreated.Set();
        }

        private void _lgpFilesWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
                _lgpChangedCreated.Set();
        }

        public void Dispose()
        {
            if (_lgpFilesWatcher != null)
            {
                _lgpFilesWatcher.Dispose();

                _lgpChangedCreated.Dispose();
            }

            if (_lgfReader != null)
                _lgfReader.Dispose();
        }
    }
}
