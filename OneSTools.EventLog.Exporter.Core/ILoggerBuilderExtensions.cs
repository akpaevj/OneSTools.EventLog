using Microsoft.Extensions.Logging;

namespace OneSTools.EventLog.Exporter.Core
{
    public static class LoggerBuilderExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder loggingBuilder, string path)
        {
            var provider = new FileLoggerProvider(path);

            return loggingBuilder.AddProvider(provider);
        }
    }
}