using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Timers = System.Timers;

namespace OneSTools.EventLog
{
    /// <summary>
    ///     Presents methods for reading 1C event log
    /// </summary>
    public class EventLogReader : IDisposable
    {
        private readonly EventLogReaderSettings _settings;
        private bool _disposedValue;
        private LgfReader _lgfReader;
        private ManualResetEvent _lgpChangedCreated;        
        private Timers.Timer _Timer;
        private LgpReader _lgpReader;

        public EventLogReader(EventLogReaderSettings settings)
        {
            _settings = settings;

            _lgfReader = new LgfReader(Path.Combine(_settings.LogFolder, "1Cv8.lgf"));
            _lgfReader.SetPosition(settings.LgfStartPosition);

            if (settings.LgpFileName != string.Empty)
            {
                var file = Path.Combine(_settings.LogFolder, settings.LgpFileName);

                _lgpReader = new LgpReader(file, settings.TimeZone, _lgfReader, settings.SkipEventsBeforeDate);
                _lgpReader.SetPosition(settings.LgpStartPosition);
            }
        }

        /// <summary>
        ///     Current reader's "lgp" file name
        /// </summary>
        public string LgpFileName => _lgpReader.LgpFileName;

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     The behaviour of the method depends on the mode of the reader. In the "live" mode it'll be waiting for an appearing
        ///     of the new event item, otherwise It'll just return null
        /// </summary>
        /// <param name="cancellationToken">Token for interrupting of the reader</param>
        /// <returns></returns>
        public EventLogItem ReadNextEventLogItem(CancellationToken cancellationToken = default)
        {
            if (_lgpReader == null)
                SetNextLgpReader();

            if (_settings.LiveMode && _Timer == null)
                StartTimer();

            EventLogItem item = null;

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

                if (item == null)
                {
                    var newReader = SetNextLgpReader();

                    if (_settings.LiveMode)
                    {
                        if (!newReader)
                        {
                            _lgpChangedCreated.Reset();

                            var waitHandle = WaitHandle.WaitAny(
                                new[] {_lgpChangedCreated, cancellationToken.WaitHandle}, _settings.ReadingTimeout);

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
                {
                    _settings.ItemId++;
                    item.Id = _settings.ItemId;

                    break;
                }
            }

            return item;
        }

        private bool SetNextLgpReader()
        {
            var currentReaderLastWriteDateTime = DateTime.MinValue;

            if (_lgpReader != null)
                currentReaderLastWriteDateTime = new FileInfo(_lgpReader.LgpPath).LastWriteTime;
            else if(_settings.SkipEventsBeforeDate != DateTime.MinValue)
                currentReaderLastWriteDateTime = _settings.SkipEventsBeforeDate.AddSeconds(-1);

            var filesDateTime = new List<(string, DateTime)>();

            var files = Directory.GetFiles(_settings.LogFolder, "*.lgp");

            foreach (var file in files)
                if (_lgpReader != null)
                {
                    if (_lgpReader.LgpPath != file)
                        filesDateTime.Add((file, new FileInfo(file).LastWriteTime));
                }
                else
                {
                    filesDateTime.Add((file, new FileInfo(file).LastWriteTime));
                }

            var orderedFiles = filesDateTime.OrderBy(c => c.Item2).ToList();

            var (item1, _) = orderedFiles.FirstOrDefault(c => c.Item2 > currentReaderLastWriteDateTime);

            if (string.IsNullOrEmpty(item1))
            {
                return false;
            }

            _lgpReader?.Dispose();
            _lgpReader = null;

            _lgpReader = new LgpReader(item1, _settings.TimeZone, _lgfReader, _settings.SkipEventsBeforeDate);

            return true;
        }

        private void StartTimer()
        {
            _lgpChangedCreated = new ManualResetEvent(false);

            _Timer = new Timers.Timer(_settings.ReadingTimeout);
            _Timer.Elapsed += OnTimedEvent;
            _Timer.AutoReset = true;
            _Timer.Enabled = true;
        }

        private void OnTimedEvent(Object source, Timers.ElapsedEventArgs e)
        {
            _lgpChangedCreated.Set();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _Timer?.Stop();
                _Timer?.Dispose();
                _Timer = null;
                _lgpChangedCreated?.Dispose();
                _lgpChangedCreated = null;
                _lgfReader?.Dispose();
                _lgfReader = null;
                _lgpReader?.Dispose();
                _lgpReader = null;

                _disposedValue = true;
            }
        }

        ~EventLogReader()
        {
            Dispose(false);
        }
    }
}