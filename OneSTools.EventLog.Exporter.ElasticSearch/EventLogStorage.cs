using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OneSTools.EventLog.Exporter.Core;
using System.Linq;
using System.Data;
using Elasticsearch.Net;
using Nest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneSTools.EventLog.Exporter.ElasticSearch
{
    public class EventLogStorage<T> : IEventLogStorage<T>, IDisposable where T : class, IEventLogItem, new()
    {
        private readonly ILogger<EventLogStorage<T>> _logger;
        private readonly string _eventLogItemsIndex;
        private readonly string _separation;
        readonly ElasticClient _client;

        public EventLogStorage(ILogger<EventLogStorage<T>> logger, IConfiguration configuration)
        {
            _logger = logger;

            var host = configuration.GetValue("ElasticSearch:Host", "");
            if (host == string.Empty)
                throw new Exception("ElasticSearch host is not specified");

            var port = configuration.GetValue("ElasticSearch:Port", 9200);

            var index = configuration.GetValue("ElasticSearch:Index", "");
            if (index == string.Empty)
                throw new Exception("ElasticSearch index name is not specified");

            _separation = configuration.GetValue("ElasticSearch:Separation", "H");

            var uri = new Uri($"{host}:{port}");
            _eventLogItemsIndex = index;

            var settings = new ConnectionSettings(uri);
            settings.EnableHttpCompression();

            _client = new ElasticClient(settings);
            var response = _client.Ping();

            if (!response.IsValid)
                throw response.OriginalException;

            CreateIndexTemplate();
        }

        private void CreateIndexTemplate()
        {
            var cmd = 
                "{\n" +
                $"  \"index_patterns\": [\"{_eventLogItemsIndex}-*\"],\n" +
                "   \"template\": {\n" +
                "       \"settings\": {\n" +
                "           \"number_of_shards\": 5,\n" +
                "           \"number_of_replicas\": 0\n," +
                "           \"index.codec\": \"best_compression\"\n" +
                "       }\n" +
                "   }\n" +
                "}";

            var response = _client.LowLevel.DoRequest<StringResponse>(HttpMethod.PUT, $"_index_template/{_eventLogItemsIndex}", PostData.String(cmd));

            if (!response.Success)
                throw response.OriginalException;
        }

        public async Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var response = await _client.SearchAsync<T>(sd => sd
                .Index($"{_eventLogItemsIndex}-*")
                .Sort(ss => 
                    ss.Descending("DateTime"))
                .Size(1)
            );

            if (response.IsValid)
            {
                var item = response.Documents.FirstOrDefault();

                if (item is null)
                    return ("", 0);

                return (item.FileName, item.EndPosition);
            }

            return ("", 0);
        }

        public async Task WriteEventLogDataAsync(List<T> entities, CancellationToken cancellationToken = default)
        {
            var data = new List<(string IndexName, List<T> Entities)>();

            switch (_separation)
            {
                case "H":
                    var groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMMddhh")).OrderBy(c => c.Key);
                    foreach (IGrouping<string, T> item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "D":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMMdd")).OrderBy(c => c.Key);
                    foreach (IGrouping<string, T> item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "M":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMM")).OrderBy(c => c.Key);
                    foreach (IGrouping<string, T> item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                default:
                    data.Add(($"{_eventLogItemsIndex}-all", entities));
                    break;
            }

            foreach((string IndexName, List<T> Entities) item in data)
            {
                var responseItems = await _client.IndexManyAsync(item.Entities, item.IndexName, cancellationToken);

                if (responseItems.Errors)
                {
                    foreach (var itemWithError in responseItems.ItemsWithErrors)
                    {
                        _logger.LogError(responseItems.OriginalException, $"Fialed to index document {itemWithError.Id}: {itemWithError.Error}");
                    }

                    _logger.LogError(responseItems.OriginalException, "Fialed to write items");
                    throw responseItems.OriginalException;
                }
            }

            _logger.LogDebug($"{DateTime.Now:(hh:mm:ss.fffff)} | {entities.Count} items have been written");
        }

        public void Dispose()
        {
            
        }
    }
}
