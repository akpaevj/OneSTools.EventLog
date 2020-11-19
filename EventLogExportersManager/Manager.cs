using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneSTools.EventLog;
using OneSTools.EventLog.Exporter.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneSTools.EventLog.Exporter.ElasticSearch;
using System.IO;

namespace EventLogExportersManager
{
    public class Manager : BackgroundService
    {
        private readonly ILogger<Manager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _regFolder;
        private readonly string _includePattern;
        private readonly Dictionary<string, (EventLogExporter<EventLogItem>, CancellationTokenSource Cts)> _exporters;

        public Manager(ILogger<Manager> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            _regFolder = configuration.GetValue("Manager:RegFolder", "");
            if (string.IsNullOrWhiteSpace(_regFolder))
                _logger.LogError("Reg folder is not specified");

            _includePattern = configuration.GetValue("Manager:IncludePattern", "");

            _exporters = new Dictionary<string, (EventLogExporter<EventLogItem>, CancellationTokenSource Cts)>();

            var infoBases = ClstReader.GetInfoBases(_regFolder, _includePattern);

            foreach (var infobase in infoBases)
            {
                var logFolder = Path.Combine(_regFolder, $"{infobase.Value}\\1Cv8Log");

                // skip infobases without LGP file
                if (!File.Exists(Path.Combine(logFolder, "1Cv8.lgf")))
                    continue;

                var storageLogger = _serviceProvider.GetService<ILogger<EventLogStorage<EventLogItem>>>();
                var nodes = configuration.GetSection("ElasticSearch:Nodes").Get<List<ElasticSearchNode>>();
                var index = $"{infobase.Key}-el";
                var separation = configuration.GetValue("ElasticSearch:Separation", "H");
                var maximumRetries = configuration.GetValue("ElasticSearch:MaximumRetries", EventLogStorage<EventLogItem>.DEFAULT_MAXIMUM_RETRIES);
                var maxRetryTimeout = TimeSpan.FromSeconds(configuration.GetValue("ElasticSearch:MaxRetryTimeout", EventLogStorage<EventLogItem>.DEFAULT_MAX_RETRY_TIMEOUT_SEC));
                var exporterStorage = new EventLogStorage<EventLogItem>(storageLogger, nodes, index, separation, maximumRetries, maxRetryTimeout);

                var exporterLogger = _serviceProvider.GetService<ILogger<EventLogExporter<EventLogItem>>>();
                var portion = configuration.GetValue("Exporter:Portion", EventLogExporter<EventLogItem>.DEFAULT_PORTION);
                var timeZone = configuration.GetValue("Exporter:TimeZone", "");
                var exporter = new EventLogExporter<EventLogItem>(exporterLogger, exporterStorage, logFolder, portion, timeZone);

                var cts = new CancellationTokenSource();

                _exporters.Add(infobase.Key, (exporter, cts));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var registration = stoppingToken.Register(() =>
            {
                foreach (var item in _exporters)
                    item.Value.Cts.Cancel();
            });

            var tasks = new List<Task>();

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var item in _exporters)
                {
                    var task = item.Value.Item1.StartAsync();

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
        }
    }
}
