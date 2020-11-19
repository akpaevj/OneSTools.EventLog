using NodaTime;
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
        private EventLogReaderSetings _settings;
        private LgfReader _lgfReader;
        private LgpReader<T> _lgpReader;
        private FileSystemWatcher _lgpFilesWatcher;
        private bool disposedValue;

        /// <summary>
        /// Current reader's "lgp" file name
        /// </summary>
        public string LgpFileName => _lgpReader.LgpFileName;

        public EventLogReader(EventLogReaderSetings settings)
        {
            _settings = settings;

            _lgfReader = new LgfReader(Path.Combine(_settings.LogFolder, "1Cv8.lgf"));
            _lgfReader.SetPosition(settings.LgpStartPosition);

            if (settings.LgpFileName != string.Empty)
            {
                var file = Path.Combine(_settings.LogFolder, settings.LgpFileName);

                _lgpReader = new LgpReader<T>(file, settings.TimeZone, _lgfReader);
                _lgpReader.SetPosition(settings.StartPosition);
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

            if (_settings.LiveMode && _lgpFilesWatcher == null)
                StartLgpFilesWatcher();

            T item = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    item = _lgpReader.ReadNextEventLogItem(cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    item = null;
                    _lgpReader = null;
                    break;
                }
                catch(Exception ex)
                {
                    throw ex;
                }

                if (item == null)
                {
                    var newReader = SetNextLgpReader();

                    if (_settings.LiveMode)
                    {
                        if (!newReader)
                        {
                            _lgpChangedCreated.Reset();

                            var waitHandle = WaitHandle.WaitAny(new WaitHandle[] { _lgpChangedCreated, cancellationToken.WaitHandle }, _settings.ReadingTimeout);

                            if (_settings.ReadingTimeout != Timeout.Infinite && waitHandle == WaitHandle.WaitTimeout)
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

            var filesDateTime = new List<(string, DateTime)>();

            var files = Directory.GetFiles(_settings.LogFolder, "*.lgp");

            foreach (var file in files)
            {
                if (_lgpReader != null)
                {
                    if (_lgpReader.LgpPath != file)
                        filesDateTime.Add((file, new FileInfo(file).LastWriteTime));
                }
                else
                    filesDateTime.Add((file, new FileInfo(file).LastWriteTime));
            }

            var orderedFiles = filesDateTime.OrderBy(c => c.Item2).ToList();

            var nextFile = orderedFiles.FirstOrDefault(c => c.Item2 > currentReaderLastWriteDateTime);

            if (string.IsNullOrEmpty(nextFile.Item1))
                return false;
            else
            {
                _lgpReader?.Dispose();
                _lgpReader = null;

                _lgpReader = new LgpReader<T>(nextFile.Item1, _settings.TimeZone, _lgfReader);

                return true;
            }
        }

        private void StartLgpFilesWatcher()
        {
            _lgpChangedCreated = new ManualResetEvent(false);

            _lgpFilesWatcher = new FileSystemWatcher(_settings.LogFolder, "*.lgp")
            {
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
            _lgpFilesWatcher.Changed += LgpFilesWatcher_Changed;
            _lgpFilesWatcher.Created += LgpFilesWatcher_Created;
            _lgpFilesWatcher.EnableRaisingEvents = true;
        }

        private void LgpFilesWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
                _lgpChangedCreated.Set();
        }

        private void LgpFilesWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
                _lgpChangedCreated.Set();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                }

                _lgpFilesWatcher?.Dispose();
                _lgpFilesWatcher = null;
                _lgpChangedCreated?.Dispose();
                _lgpChangedCreated = null;
                _lgfReader?.Dispose();
                _lgfReader = null;
                _lgpReader?.Dispose();
                _lgpReader = null;

                disposedValue = true;
            }
        }

        ~EventLogReader()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
