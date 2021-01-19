using System;
using System.Collections.Generic;

namespace OneSTools.EventLog.Exporter.Core.ElasticSearch
{
    public class ElasticSearchStorageSettings
    {
        public List<ElasticSearchNode> Nodes { get; } = new List<ElasticSearchNode>();
        public string Index { get; set; } = "";
        public string Separation { get; set; } = "";
        public int MaximumRetries { get; set; } = ElasticSearchStorage.DefaultMaximumRetries;
        public TimeSpan MaxRetryTimeout { get; set; } = TimeSpan.FromSeconds(ElasticSearchStorage.DefaultMaxRetryTimeoutSec);
    }
}
