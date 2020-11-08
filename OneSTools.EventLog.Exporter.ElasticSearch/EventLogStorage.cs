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
    public class EventLogStorage : IEventLogStorage, IDisposable
    {
        private string _eventLogitemsIndex;
        ElasticClient _client;

        public EventLogStorage(string host, int port = 9200, string index = "")
        {
            var uri = new Uri($"{host}:{port}");
            _eventLogitemsIndex = $"{index}-eli";

            var settings = new ConnectionSettings(uri);
            settings.DefaultIndex(_eventLogitemsIndex);

            _client = new ElasticClient(settings);
            var response = _client.Ping();

            if (!response.IsValid)
                throw response.OriginalException;
        }

        public async Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var response = await _client.SearchAsync<EventLogItem>(sd => sd
                .Sort(ss => 
                    ss.Descending("Id"))
                .Size(1)
            );

            if (response.IsValid)
            {
                var item = response.Documents.FirstOrDefault();

                return (item.FileName, item.EndPosition);
            }

            return ("", 0);
        }

        public async Task WriteEventLogDataAsync(List<EventLogItem> entities, CancellationToken cancellationToken = default)
        {
            var responseItems = await _client.IndexManyAsync(entities, null, cancellationToken);

            if (!responseItems.IsValid)
            {
                throw responseItems.OriginalException;
            }
        }

        public void Dispose()
        {
            
        }
    }
}
