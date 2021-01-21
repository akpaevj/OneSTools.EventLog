namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstEventArgs
    {
        public string Path { get; }
        public string Name { get; }
        public string DataBaseName { get; }

        internal ClstEventArgs(string path, string name, string databaseName)
        {
            Path = path;
            Name = name;
            DataBaseName = databaseName;
        }
    }
}
