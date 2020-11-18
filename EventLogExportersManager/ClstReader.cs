using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace EventLogExportersManager
{
    public static class ClstReader
    {
        public static Dictionary<string, string> GetInfoBases(string regFolder, string includePattern)
        {
            var result = new Dictionary<string, string>();

            var filePath = Path.Combine(regFolder, "1CV8Clst.lst");

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var streamReader = new StreamReader(fileStream);

            var strData = streamReader.ReadToEnd();
            var data = OneSTools.BracketsFile.BracketsFileParser.ParseBlock(strData);

            var infoBasesNode = data[2];
            var infoBasesCount = (int)infoBasesNode[0];

            if (infoBasesCount > 0)
                for (int i = 1; i <= infoBasesCount; i++)
                {
                    bool include = true;

                    var item = infoBasesNode[i];
                    var guid = (string)item[0];
                    var name = (string)item[1];

                    if (!string.IsNullOrWhiteSpace(includePattern))
                        include = Regex.IsMatch(name, includePattern);

                    if (include)
                        result.Add(name, guid);
                }

            return result;
        }
    }
}
