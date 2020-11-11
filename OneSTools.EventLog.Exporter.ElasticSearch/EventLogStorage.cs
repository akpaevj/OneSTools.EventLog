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

namespace OneSTools.EventLog.Exporter.ElasticSearch
{
    public class EventLogStorage<T> : IEventLogStorage<T>, IDisposable where T : class, IEventLogItem, new()
    {
        private readonly string _eventLogitemsIndex;
        private readonly string _separation;
        readonly ElasticClient _client;

        public EventLogStorage(IConfiguration configuration)
        {
            var host = configuration.GetValue("ElasticSearch:Host", "");
            if (host == string.Empty)
                throw new Exception("ElasticSearch host is not specified");

            var port = configuration.GetValue("ElasticSearch:Port", 9200);

            var index = configuration.GetValue("ElasticSearch:Index", "");
            if (index == string.Empty)
                throw new Exception("ElasticSearch index name is not specified");

            _separation = configuration.GetValue("ElasticSearch:Separation", "H");

            var uri = new Uri($"{host}:{port}");
            _eventLogitemsIndex = $"{index}-el";

            var settings = new ConnectionSettings(uri);
            settings.DefaultIndex(_eventLogitemsIndex);

            _client = new ElasticClient(settings);
            var response = _client.Ping();

            if (!response.IsValid)
                throw response.OriginalException;
        }

        public async Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var response = await _client.SearchAsync<T>(sd => sd
                .Sort(ss => 
                    ss.Descending("DateTime"))
                .Size(1)
            );

            if (response.IsValid)
            {
                var item = response.Documents.FirstOrDefault();

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
                        data.Add(($"{_eventLogitemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "D":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMMdd")).OrderBy(c => c.Key);
                    foreach (IGrouping<string, T> item in groups)
                        data.Add(($"{_eventLogitemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "M":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMM")).OrderBy(c => c.Key);
                    foreach (IGrouping<string, T> item in groups)
                        data.Add(($"{_eventLogitemsIndex}-{item.Key}", item.ToList()));
                    break;
                default:
                    data.Add(($"{_eventLogitemsIndex}-all", entities));
                    break;
            }

            foreach((string IndexName, List<T> Entities) item in data)
            {
                var responseItems = await _client.IndexManyAsync(item.Entities, item.IndexName, cancellationToken);

                if (!responseItems.IsValid)
                {
                    throw responseItems.OriginalException;
                }
            }
        }

        public void Dispose()
        {
            
        }
    }
}
