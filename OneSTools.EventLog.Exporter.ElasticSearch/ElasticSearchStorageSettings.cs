using System;
using System.Collections.Generic;

namespace OneSTools.EventLog.Exporter.ElasticSearch
{
    public class ElasticSearchStorageSettings
    {
        public List<ElasticSearchNode> Nodes { get; private set; } = new List<ElasticSearchNode>();
        public string Index { get; set; } = "";
        public string Separation { get; set; } = "";
        public int MaximumRetries { get; set; } = ElasticSearchStorage.DEFAULT_MAXIMUM_RETRIES;
        public TimeSpan MaxRetryTimeout { get; set; } = TimeSpan.FromSeconds(ElasticSearchStorage.DEFAULT_MAX_RETRY_TIMEOUT_SEC);
    }
}
