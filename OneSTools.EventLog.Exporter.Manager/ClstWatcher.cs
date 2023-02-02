using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OneSTools.BracketsFile;
using System.Threading.Tasks;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstWatcher : IDisposable
    {
        public delegate void InfoBaseAddedHandler(object sender, ClstEventArgs args);

        public delegate void InfoBaseDeletedHandler(object sender, ClstEventArgs args);

        private readonly string _folder;
        private readonly string _path;
        private readonly List<TemplateItem> _templates;
        private FileSystemWatcher _clstWatcher;
        private Dictionary<string, (string, string)> _infoBases;

        public ClstWatcher(string folder, List<TemplateItem> templates)
        {
            _folder = folder;
            _templates = templates;
            _path = Path.Combine(_folder, "1CV8Clst.lst");
            if (!File.Exists(_path))
                throw new Exception("Couldn't find LST \"1CV8Clst.lst\" file");
            
            InitializeWatcher();
        }

        public ReadOnlyDictionary<string, (string Name, string DataBaseName)> InfoBases => new(_infoBases);

        public void Dispose()
        {
            _clstWatcher?.Dispose();
            GC.SuppressFinalize(this);
        }

        public event InfoBaseAddedHandler InfoBasesAdded;
        public event InfoBaseDeletedHandler InfoBasesDeleted;

        private async Task<Dictionary<string, (string Name, string DataBaseName)>> ReadInfoBases()
        {
            var items = new Dictionary<string, (string, string)>();

            String fileData = null;
            int tryCount = 10;            
            while (fileData == null)
            {
                try
                {
                    using StreamReader stream = new StreamReader(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); 
                    {
                        fileData = stream.ReadToEnd();
                    }
                }
                catch (Exception e)
                {
                    tryCount--;
                    if (tryCount == 0)
                        throw new Exception("Ошибка чтения файла списка баз", e);
                    await Task.Delay(1000);
                }                
            }
            
            var parsedData = BracketsParser.ParseBlock(fileData);

            var infoBasesNode = parsedData[2];
            int count = infoBasesNode[0];

            if (count > 0)
                for (var i = 1; i <= count; i++)
                {
                    var infoBaseNode = infoBasesNode[i];

                    var elPath = Path.Combine(_folder, infoBaseNode[0]);
                    string name = infoBaseNode[5];

                    foreach (var template in _templates)
                        if (Regex.IsMatch(name, template.Mask))
                        {
                            var dataBaseName = template.Template.Replace("[IBNAME]", name);
                            items.Add(elPath, (name, dataBaseName));

                            break;
                        }
                }

            return items;
        }

        private async void ReadInfoBasesAndRaiseEvents()
        {
            var newInfoBases = await ReadInfoBases();

            var added = newInfoBases.Except(_infoBases);
            foreach (var (key, (item1, item2)) in added)
                InfoBasesAdded?.Invoke(this, new ClstEventArgs(key, item1, item2));

            var deleted = _infoBases.Except(newInfoBases);
            foreach (var (key, (item1, item2)) in deleted)
                InfoBasesDeleted?.Invoke(this, new ClstEventArgs(key, item1, item2));

            _infoBases = newInfoBases;
        }

        private async void InitializeWatcher()
        {
            _infoBases = await ReadInfoBases();
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
            {
                ReadInfoBasesAndRaiseEvents();
            }
        }
    }
}