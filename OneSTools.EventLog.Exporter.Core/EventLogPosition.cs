namespace OneSTools.EventLog.Exporter.Core
{
    public class EventLogPosition
    {
        public string FileName { get; private set; } = "";
        public long EndPosition { get; private set; } = 0;
        public long LgfEndPosition { get; private set; } = 0;
        public long Id { get; private set; } = -1;

        public EventLogPosition(string fileName, long endPosition, long lgfEndPosition, long id)
        {
            FileName = fileName;
            EndPosition = endPosition;
            LgfEndPosition = lgfEndPosition;
            Id = id;
        }
    }
}
