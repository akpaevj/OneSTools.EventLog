using System;
using System.Collections.Generic;
using System.Text;

namespace OneSTools.EventLog.Exporter.SqlServer
{
    public class EventLogItem : EventLog.EventLogItem
    {
        public long Id { get; set; }
    }
}
