using NodaTime;
using System.Threading;

namespace OneSTools.EventLog
{
    public class EventLogReaderSetings
    {
        public string LogFolder { get; set; } = "";
        public bool LiveMode { get; set; } = true;
        public string LgpFileName { get; set; } = "";
        public long StartPosition { get; set; } = 0;
        public long LgpStartPosition { get; set; } = 0;
        public int ReadingTimeout { get; set; } = Timeout.Infinite;
        public DateTimeZone TimeZone { get; set; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();
    }
}
