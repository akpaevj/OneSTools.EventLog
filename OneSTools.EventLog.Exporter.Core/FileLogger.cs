using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OneSTools.EventLog.Exporter.Core
{
    public class FileLogger : ILogger
    {
        private static readonly object Locker = new object();
        private readonly string _categoryName;
        private readonly string _path;

        public FileLogger(string path, string categoryName)
        {
            _path = path;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var levelName = Enum.GetName(typeof(LogLevel), logLevel);

            var message =
                $"{DateTime.Now:yyyy-MM-hh HH:mm:ss.fff} | {levelName} | {_categoryName}[{eventId.Id}]\n\t{formatter(state, exception)}";

            lock (Locker)
            {
                File.AppendAllText(_path, message + Environment.NewLine);
            }
        }
    }
}