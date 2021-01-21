using System.Collections.Generic;

namespace OneSTools.EventLog.Exporter.Manager
{
    public class ClstFolder
    {
        public string Folder { get; set; } = "";
        public List<TemplateItem> Templates { get; set; } = new();
    }
}