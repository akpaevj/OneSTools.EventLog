using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogPosition
    {
        [Key]
        public string LgpFileName { get; set; }
        public long LgpFilePosition { get; set; }
    }
}
