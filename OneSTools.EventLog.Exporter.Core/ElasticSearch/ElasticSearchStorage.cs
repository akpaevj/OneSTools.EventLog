using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;

namespace OneSTools.EventLog.Exporter.Core.ElasticSearch
{
    public class ElasticSearchStorage : IEventLogStorage
    {
        public static int DefaultMaximumRetries = 2;
        public static int DefaultMaxRetryTimeoutSec = 30;
        private readonly string _eventLogItemsIndex;

        private readonly ILogger<ElasticSearchStorage> _logger;
        private readonly int _maximumRetries;
        private readonly TimeSpan _maxRetryTimeout;
        private readonly List<ElasticSearchNode> _nodes = new List<ElasticSearchNode>();
        private readonly string _separation;
        private ElasticClient _client;
        private ElasticSearchNode _currentNode;

        public ElasticSearchStorage(ElasticSearchStorageSettings settings, ILogger<ElasticSearchStorage> logger = null)
        {
            _logger = logger;

            _nodes.AddRange(settings.Nodes);
            _eventLogItemsIndex = settings.Index;
            _separation = settings.Separation;
            _maximumRetries = settings.MaximumRetries;
            _maxRetryTimeout = settings.MaxRetryTimeout;

            CheckSettings();
        }

        public ElasticSearchStorage(ILogger<ElasticSearchStorage> logger, IConfiguration configuration)
        {
            _logger = logger;

            _nodes = configuration.GetSection("ElasticSearch:Nodes").Get<List<ElasticSearchNode>>();
            _eventLogItemsIndex = configuration.GetValue("ElasticSearch:Index", "");
            _separation = configuration.GetValue("ElasticSearch:Separation", "H");
            _maximumRetries = configuration.GetValue("ElasticSearch:MaximumRetries", DefaultMaximumRetries);
            _maxRetryTimeout =
                TimeSpan.FromSeconds(configuration.GetValue("ElasticSearch:MaxRetryTimeout",
                    DefaultMaxRetryTimeoutSec));

            CheckSettings();
        }

        public async Task<EventLogPosition> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            if (_client is null)
                await ConnectAsync(cancellationToken);

            while (true)
            {
                var response = await _client.SearchAsync<EventLogItem>(sd => sd
                        .Index($"{_eventLogItemsIndex}-*")
                        .Sort(ss =>
                            ss.Descending(c => c.Id))
                        .Size(1)
                    , cancellationToken);

                if (response.IsValid)
                {
                    var item = response.Documents.FirstOrDefault();

                    if (item is null)
                        return null;
                    return new EventLogPosition(item.FileName, item.EndPosition, item.LgfEndPosition, item.Id);
                }

                if (response.OriginalException is TaskCanceledException)
                    throw response.OriginalException;

                _logger?.LogError(
                    $"Failed to get last file's position ({_eventLogItemsIndex}): {response.OriginalException.Message}");

                var currentNodeHost = _currentNode.Host;

                await ConnectAsync(cancellationToken);

                // If it's the same node then wait while MaxRetryTimeout occurs, otherwise it'll be a too often request's loop
                if (_currentNode.Host.Equals(currentNodeHost))
                    await Task.Delay(_maxRetryTimeout, cancellationToken);
            }
        }

        public async Task WriteEventLogDataAsync(List<EventLogItem> entities,
            CancellationToken cancellationToken = default)
        {
            if (_client is null)
                await ConnectAsync(cancellationToken);

            var data = GetGroupedData(entities);

            for (var i = 0; i < data.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var item = data[i];

                var responseItems = await _client.IndexManyAsync(item.Entities, item.IndexName, cancellationToken);

                if (!responseItems.ApiCall.Success)
                {
                    if (responseItems.OriginalException is TaskCanceledException)
                        throw responseItems.OriginalException;

                    if (responseItems.Errors)
                    {
                        foreach (var itemWithError in responseItems.ItemsWithErrors)
                            _logger?.LogError(
                                $"Failed to index document {itemWithError.Id} in {item.IndexName}: {itemWithError.Error}");

                        throw new Exception(
                            $"Failed to write items to {item.IndexName}: {responseItems.OriginalException.Message}");
                    }

                    _logger?.LogError(
                        $"Failed to write items to {item.IndexName}: {responseItems.OriginalException.Message}");

                    await ConnectAsync(cancellationToken);

                    i--;
                }
                else
                {
                    if (responseItems.Errors)
                    {
                        foreach (var itemWithError in responseItems.ItemsWithErrors)
                            _logger?.LogError(
                                $"Failed to index document {itemWithError.Id} in {item.IndexName}: {itemWithError.Error}");

                        throw new Exception(
                            $"Failed to write items to {item.IndexName}: {responseItems.OriginalException.Message}");
                    }

                    _logger?.LogDebug($"{item.Entities.Count} items were being written to {item.IndexName}");
                }
            }
        }

        public void Dispose()
        {
        }

        private void CheckSettings()
        {
            if (_nodes.Count == 0)
                throw new Exception("ElasticSearch hosts is not specified");

            if (_eventLogItemsIndex == string.Empty)
                throw new Exception("ElasticSearch index name is not specified");
        }

        private async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var connected = await SwitchToNextNodeAsync(cancellationToken);

                if (connected)
                {
                    await CreateIndexTemplateAsync(cancellationToken);

                    break;
                }
            }
        }

        private async Task CreateIndexTemplateAsync(CancellationToken cancellationToken = default)
        {
            var indexTemplateName = "oneslogs";

            var getItResponse = await _client.LowLevel.DoRequestAsync<StringResponse>(HttpMethod.GET,
                $"_index_template/{indexTemplateName}", cancellationToken);

            // if it exists then skip creating
            if (!getItResponse.Success)
                throw getItResponse.OriginalException;
            if (getItResponse.HttpStatusCode != 404)
                return;

            var cmd =
                @"{
                    ""index_patterns"": ""*-el-*"",
                    ""template"": {
                        ""settings"": {
                            ""index.codec"": ""best_compression""
                        },
                        ""mappings"": {
                            ""properties"": {
                                ""fileName"": { ""type"": ""keyword"" },
                                ""endPosition"": { ""type"": ""long"" },
                                ""lgfEndPosition"": { ""type"": ""long"" },
                                ""Id"": { ""type"": ""long"" },
                                ""dateTime"": { ""type"": ""date"" }, 
                                ""severity"": { ""type"": ""keyword"" },
                                ""server"": { ""type"": ""keyword"" },
                                ""metadata"": { ""type"": ""keyword"" },
                                ""data"": { ""type"": ""text"" },
                                ""transactionDateTime"": { ""type"": ""date"" },
                                ""transactionStatus"": { ""type"": ""keyword"" },
                                ""session"": { ""type"": ""long"" },
                                ""mainPort"": { ""type"": ""integer"" },
                                ""transactionNumber"": { ""type"": ""long"" },
                                ""addPort"": { ""type"": ""integer"" },
                                ""computer"": { ""type"": ""keyword"" },
                                ""application"": { ""type"": ""keyword"" },
                                ""userUuid"": { ""type"": ""keyword"" },
                                ""comment"": { ""type"": ""text"" },
                                ""connection"": { ""type"": ""long"" },
                                ""event"": { ""type"": ""keyword"" },
                                ""metadataUuid"": { ""type"": ""keyword"" },
                                ""dataPresentation"": { ""type"": ""text"" },
                                ""user"": { ""type"": ""keyword"" }
                            }
                        }
                    }
                }";

            var response = await _client.LowLevel.DoRequestAsync<StringResponse>(HttpMethod.PUT,
                $"_index_template/{indexTemplateName}", cancellationToken, PostData.String(cmd));

            if (!response.Success)
                throw response.OriginalException;
        }

        private async Task<bool> SwitchToNextNodeAsync(CancellationToken cancellationToken = default)
        {
            if (_currentNode == null)
            {
                _currentNode = _nodes[0];
            }
            else
            {
                var currentIndex = _nodes.IndexOf(_currentNode);

                _currentNode = currentIndex == _nodes.Count - 1 ? _nodes[0] : _nodes[currentIndex + 1];
            }

            var uri = new Uri(_currentNode.Host);

            var settings = new ConnectionSettings(uri);
            settings.EnableHttpCompression();
            settings.MaximumRetries(_maximumRetries);
            settings.MaxRetryTimeout(_maxRetryTimeout);

            switch (_currentNode.AuthenticationType)
            {
                case AuthenticationType.Basic:
                    settings.BasicAuthentication(_currentNode.UserName, _currentNode.Password);
                    break;
                case AuthenticationType.ApiKey:
                    settings.ApiKeyAuthentication(_currentNode.Id, _currentNode.ApiKey);
                    break;
            }

            _client = new ElasticClient(settings);

            _logger?.LogInformation($"Trying to connect to {uri} ({_eventLogItemsIndex})");

            var response = await _client.PingAsync(pd => pd, cancellationToken);

            if (!(response.OriginalException is TaskCanceledException))
            {
                if (!response.IsValid)
                    _logger?.LogWarning(
                        $"Failed to connect to {uri} ({_eventLogItemsIndex}): {response.OriginalException.Message}");
                else
                    _logger?.LogInformation($"Successfully connected to {uri} ({_eventLogItemsIndex})");
            }

            return response.IsValid;
        }

        private List<(string IndexName, List<EventLogItem> Entities)> GetGroupedData(List<EventLogItem> entities)
        {
            var data = new List<(string IndexName, List<EventLogItem> Entities)>();

            switch (_separation)
            {
                case "H":
                    var groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMMddhh")).OrderBy(c => c.Key);
                    foreach (var item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "D":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMMdd")).OrderBy(c => c.Key);
                    foreach (var item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                case "M":
                    groups = entities.GroupBy(c => c.DateTime.ToString("yyyyMM")).OrderBy(c => c.Key);
                    foreach (var item in groups)
                        data.Add(($"{_eventLogItemsIndex}-{item.Key}", item.ToList()));
                    break;
                default:
                    data.Add(($"{_eventLogItemsIndex}-all", entities));
                    break;
            }

            return data;
        }
    }
}