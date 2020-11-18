using System;
using System.Collections.Generic;

namespace OneSTools.EventLog.Exporter.ElasticSearch
{
    public class ElasticSearchNode
    {
        public string Host { get; set; }
        public AuthenticationType AuthenticationType { get; set; }
        public string Id { get; set; }
        public string ApiKey { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ElasticSearchNode host &&
                   Host == host.Host;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host);
        }

        public static bool operator ==(ElasticSearchNode left, ElasticSearchNode right)
        {
            return EqualityComparer<ElasticSearchNode>.Default.Equals(left, right);
        }

        public static bool operator !=(ElasticSearchNode left, ElasticSearchNode right)
        {
            return !(left == right);
        }
    }
}
