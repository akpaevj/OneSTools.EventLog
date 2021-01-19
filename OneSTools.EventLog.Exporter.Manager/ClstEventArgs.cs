namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstEventArgs
    {
        public string Id { get; }
        public string Name { get; }
        public string DataBaseName { get; }

        internal ClstEventArgs(string id, string name, string databaseName)
        {
            Id = id;
            Name = name;
            DataBaseName = databaseName;
        }
    }
}
