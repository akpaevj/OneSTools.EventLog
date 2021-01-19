namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogPosition
    {
        public string FileName { get; }
        public long EndPosition { get; }
        public long LgfEndPosition { get; }
        public long Id { get; }

        public EventLogPosition(string fileName, long endPosition, long lgfEndPosition, long id)
        {
            FileName = fileName;
            EndPosition = endPosition;
            LgfEndPosition = lgfEndPosition;
            Id = id;
        }
    }
}
