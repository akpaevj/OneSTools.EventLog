using System;
using System.Collections.Generic;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstEventArgs
    {
        public string Id { get; private set; }
        public string Name { get; private set; }

        internal ClstEventArgs(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
