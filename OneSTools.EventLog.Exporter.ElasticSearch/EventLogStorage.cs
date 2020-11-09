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

namespace OneSTools.EventLog.Exporter.ElasticSearch
{
    public class EventLogStorage<T> : IEventLogStorage<T>, IDisposable where T : class, IEventLogItem, new()
    {
        private string _eventLogitemsIndex;
        private string _separation;
        ElasticClient _client;

        public EventLogStorage(string host, int port = 9200, string index = "", string separation = "")
        {
            var uri = new Uri($"{host}:{port}");
            _eventLogitemsIndex = $"{index}-el";

            var settings = new ConnectionSettings(uri);
            settings.DefaultIndex(_eventLogitemsIndex);

            _separation = separation;

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
