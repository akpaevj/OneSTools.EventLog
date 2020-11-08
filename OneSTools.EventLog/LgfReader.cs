using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog
{
    internal class LgfReader : IDisposable
    {
        private FileStream fileStream;
        private StreamReader streamReader;

        public string LgfPath { get; private set; }
        /// <summary>
        /// Key tuple - first value is an object type, second value is an object number in the array
        /// </summary>
        public Dictionary<(ObjectType, int), string> _objects = new Dictionary<(ObjectType, int), string>();
        /// <summary>
        /// Key tuple - first value is an object type, second value is an object number in the array
        /// Value tuple - first value is an object value, second value is a guid of the object value
        /// </summary>
        public Dictionary<(ObjectType, int), (string, string)> _referencedObjects = new Dictionary<(ObjectType, int), (string, string)>();

        public LgfReader(string lgfPath)
        {
            LgfPath = lgfPath;
        }

        private void ReadToEnd(CancellationToken cancellationToken)
        {
            if (!File.Exists(LgfPath))
                throw new Exception("Cannot find \"1Cv8.lgf\"");

            if (fileStream is null)
            {
                fileStream = new FileStream(LgfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                streamReader = new StreamReader(fileStream);

                // skip header (first three lines)
                for (int i = 0; i < 3; i++)
                    streamReader.ReadLine();
            }

            string currentLine;
            while ((currentLine = streamReader.ReadLine()) != null && !cancellationToken.IsCancellationRequested)
            {
                var firstCommaPos = currentLine.IndexOf(',');
                var objectType = (ObjectType)int.Parse(currentLine.Substring(1, firstCommaPos - 1));

                // Skip unknown object types
                if (objectType >= ObjectType.Unknown)
                    continue;

                bool hasGuid;
                switch (objectType)
                {
                    case ObjectType.Users:
                    case ObjectType.Metadata:
                        hasGuid = true;
                        break; 
                    default:
                        hasGuid = false;
                        break;
                }

                if (hasGuid)
                {
                    var currentPos = firstCommaPos + 1;
                    var guid = currentLine.Substring(currentPos, 36);
                    currentPos += 36 + 1;

                    var secondCommaPos = currentLine.IndexOf(',', currentPos);
                    var value = currentLine.Substring(currentPos, secondCommaPos - currentPos).Trim('"');
                    currentPos = secondCommaPos + 1;

                    var lastBracketPos = currentLine.LastIndexOf('}');
                    var number = int.Parse(currentLine.Substring(currentPos, lastBracketPos - currentPos));

                    var keyTuple = (objectType, number);
                    var valueTuple = (value, guid);

                    if (_referencedObjects.ContainsKey(keyTuple))
                        throw new Exception("\"Referenced objects\" already contains the same key");

                    _referencedObjects[keyTuple] = valueTuple;
                }
                else
                {
                    var currentPos = firstCommaPos + 1;

                    var secondCommaPos = currentLine.IndexOf(',', currentPos);
                    var value = currentLine.Substring(currentPos, secondCommaPos - currentPos).Trim('"');
                    currentPos = secondCommaPos + 1;

                    var lastBracketPos = currentLine.LastIndexOf('}');
                    var number = int.Parse(currentLine.Substring(currentPos, lastBracketPos - currentPos));

                    var keyTuple = (objectType, number);

                    if (_objects.ContainsKey(keyTuple))
                        throw new Exception("\"Objects\" already contains the same key");

                    _objects[keyTuple] = value;
                }
            }
        }

        public string GetObjectValue(ObjectType objectType, int number, CancellationToken cancellationToken = default)
        {
            if (number == 0)
                return "";

            if (_objects.TryGetValue((objectType, number), out var value))
                return value;
            else
                ReadToEnd(cancellationToken);

            if (_objects.TryGetValue((objectType, number), out value))
                return value;
            else
                throw new Exception($"Cannot find objectType {objectType} with number {number} in objects collection");
        }

        public (string Value, string Uuid) GetReferencedObjectValue(ObjectType objectType, int number, CancellationToken cancellationToken = default)
        {
            if (number == 0)
                return ("", "");

            if (_referencedObjects.TryGetValue((objectType, number), out var value))
                return value;
            else
                ReadToEnd(cancellationToken);

            if (_referencedObjects.TryGetValue((objectType, number), out value))
                return value;
            else
                throw new Exception($"Cannot find objectType {objectType} with number {number} in referenced objects collection");
        }

        public void Dispose()
        {
            streamReader.Dispose();
        }
    }
}
