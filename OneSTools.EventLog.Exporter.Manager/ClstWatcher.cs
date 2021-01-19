using OneSTools.BracketsFile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstWatcher : IDisposable
    {
        private readonly string _folder;
        private readonly string _path;
        private readonly List<TemplateItem> _templates;
        private Dictionary<string, (string, string)> _infoBases;
        private FileSystemWatcher _clstWatcher;

        public delegate void InfoBaseAddedHandler(object sender, ClstEventArgs args);
        public event InfoBaseAddedHandler InfoBasesAdded;

        public delegate void InfoBaseDeletedHandler(object sender, ClstEventArgs args);
        public event InfoBaseDeletedHandler InfoBasesDeleted;

        public ReadOnlyDictionary<string, (string Name, string DataBaseName)> InfoBases => new(_infoBases);

        public ClstWatcher(string folder, List<TemplateItem> templates)
        {
            _folder = folder;
            _templates = templates;
            _path = Path.Combine(_folder, "1CV8Clst.lst");
            if (!File.Exists(_path))
                throw new Exception("Couldn't find LST \"1CV8Clst.lst\" file");

            _infoBases = ReadInfoBases();
            InitializeWatcher();
        }

        private Dictionary<string, (string Name, string DataBaseName)> ReadInfoBases()
        {
            var items = new Dictionary<string, (string, string)>();

            var fileData = File.ReadAllText(_path);
            var parsedData = BracketsParser.ParseBlock(fileData);

            var infoBasesNode = parsedData[2];
            int count = infoBasesNode[0];

            if (count > 0)
            {
                for (var i = 1; i <= count; i++)
                {
                    var infoBaseNode = infoBasesNode[i];

                    string id = infoBaseNode[0];
                    string name = infoBaseNode[5];

                    foreach (var template in _templates)
                    {
                        if (Regex.IsMatch(name, template.Mask))
                        {
                            var dataBaseName = template.Template.Replace("[IBNAME]", name);
                            items.Add(id, (name, dataBaseName));

                            break;
                        }
                    }
                }
            }

            return items;
        }

        private void ReadInfoBasesAndRaiseEvents()
        {
            var newInfoBases = ReadInfoBases();

            var added = newInfoBases.Except(_infoBases);
            foreach (var (key, value) in added)
                InfoBasesAdded?.Invoke(this, new ClstEventArgs(key, value.Item1, value.Item2));

            var deleted = _infoBases.Except(newInfoBases);
            foreach (var (key, value) in deleted)
                InfoBasesDeleted?.Invoke(this, new ClstEventArgs(key, value.Item1, value.Item2));

            _infoBases = newInfoBases;
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
            GC.SuppressFinalize(this);
        }
    }
}
