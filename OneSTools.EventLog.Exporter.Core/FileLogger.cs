using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OneSTools.EventLog.Exporter.Core
{
    public class FileLogger : ILogger
    {
        private static object _locker = new object();
        private string _path;
        private string _categoryName;

        public FileLogger(string path, string categotyName)
        {
            _path = path;
            _categoryName = categotyName;
        }

        public IDisposable BeginScope<TState>(TState state)
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var levelName = Enum.GetName(typeof(LogLevel), logLevel);

            var message = $"{DateTime.Now:yyyy-MM-hh HH:mm:ss.fff} | {levelName} | {_categoryName}[{eventId.Id}]\n\t{ formatter(state, exception)}";

            lock (_locker)
                File.AppendAllText(_path, message + Environment.NewLine);
        }
    }
}
