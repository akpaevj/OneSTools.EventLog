using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OneSTools.BracketsFile;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstWatcher : IDisposable
    {
        private readonly string _folder;
        private readonly string _path;
        private readonly string _pattern;
        private Dictionary<string, string> _infobases = new Dictionary<string, string>();
        private FileSystemWatcher _clstWatcher;

        public delegate void InfoBaseAddedHandler(object sender, ClstEventArgs args);
        public event InfoBaseAddedHandler InfoBasesAdded;

        public delegate void InfoBaseDeletedHandler(object sender, ClstEventArgs args);
        public event InfoBaseDeletedHandler InfoBasesDeleted;

        public ReadOnlyDictionary<string, string> InfoBases => new ReadOnlyDictionary<string, string>(_infobases);

        public ClstWatcher(string folder, string pattern = "")
        {
            _folder = folder;
            _pattern = pattern;
            _path = Path.Combine(_folder, "1CV8Clst.lst");
            if (!File.Exists(_path))
                throw new Exception("Couldn't find LST \"1CV8Clst.lst\" file");

            _infobases = ReadInfoBases();
            InitializeWatcher();
        }

        private Dictionary<string, string> ReadInfoBases()
        {
            var items = new Dictionary<string, string>();

            var fileData = File.ReadAllText(_path);
            var parsedData = BracketsParser.ParseBlock(fileData);

            var infobasesNode = parsedData[2];
            int count = infobasesNode[0];

            if (count > 0)
            {
                for (int i = 1; i <= count; i++)
                {
                    var infobaseNode = infobasesNode[i];

                    string id = infobaseNode[0];
                    string name = infobaseNode[5];

                    if (Regex.IsMatch(name, _pattern))
                        items.Add(id, name);
                }
            }

            return items;
        }

        private void ReadInfoBasesAndRaiseEvents()
        {
            var newInfoBases = ReadInfoBases();

            var added = newInfoBases.Except(_infobases);
            foreach (var addedIb in added)
                InfoBasesAdded?.Invoke(this, new ClstEventArgs(addedIb.Key, addedIb.Value));

            var deleted = _infobases.Except(newInfoBases);
            foreach (var deletedIb in deleted)
                InfoBasesDeleted?.Invoke(this, new ClstEventArgs(deletedIb.Key, deletedIb.Value));

            _infobases = newInfoBases;
        }

        private void InitializeWatcher()
        {
            _clstWatcher = new FileSystemWatcher(_folder, "1CV8Clst.lst")
            {
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };
            _clstWatcher.Changed += ClstWatcher_Changed;
            _clstWatcher.EnableRaisingEvents = true;
        }

        private void ClstWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            lock (InfoBases)
                ReadInfoBasesAndRaiseEvents();
        }

        public void Dispose()
        {
            _clstWatcher?.Dispose();
        }
    }
}
