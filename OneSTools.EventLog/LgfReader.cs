using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private bool disposedValue;

        public LgfReader(string lgfPath)
        {
            LgfPath = lgfPath;
        }

        private void ReadToEnd(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(LgfPath))
                throw new Exception("Cannot find \"1Cv8.lgf\"");

            if (fileStream is null)
            {
                fileStream = new FileStream(LgfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                streamReader = new StreamReader(fileStream);

                // skip header
                while (true)
                {
                    streamReader.ReadLine();

                    if (streamReader.Peek() == '{')
                        break;
                }
            }

            while (!streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var itemStrData = ReadNextItemData(cancellationToken).Trim().Trim(',');
                var itemData = BracketsFile.BracketsFileParser.Parse(itemStrData);

                var objectType = (ObjectType)(int)itemData[0];

                // Skip unknown object types
                if (objectType >= ObjectType.Unknown)
                    continue;

                switch (objectType)
                {
                    case ObjectType.Users:
                    case ObjectType.Metadata:
                        var key = (objectType, (int)itemData[3]);
                        var value = ((string)itemData[2], (string)itemData[1]);

                        if (_referencedObjects.ContainsKey(key))
                            _referencedObjects.Remove(key);

                        _referencedObjects.Add(key, value);

                        break;
                    default:
                        var key1 = (objectType, (int)itemData[2]);
                        var value1 = (string)itemData[1];

                        if (_objects.ContainsKey(key1))
                            _objects.Remove(key1);

                        _objects.Add(key1, value1);
                        break;
                }
            }
        }

        private string  ReadNextItemData(CancellationToken cancellationToken = default)
        {
            StringBuilder data = new StringBuilder();

            var quotesQuantity = 0;
            var bracketsQuantity = 0;
            bool start = false;
            bool end = false;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var currentLine = streamReader.ReadLine();

                if (currentLine is null)
                    break;

                if (!start && currentLine.Length > 0 && currentLine[0] == '{')
                    start = true;

                if (start)
                {
                    data.Append(currentLine + '\n');

                    bracketsQuantity += currentLine.Count(c => c == '{');
                    bracketsQuantity -= currentLine.Count(c => c == '}');
                    quotesQuantity += currentLine.Count(c => c == '"');

                    if (bracketsQuantity == 0 || streamReader.EndOfStream)
                    {
                        end = true;

                        if (bracketsQuantity != 0)
                            end = (bracketsQuantity % 2) == 0;
                    }
                }

                if (end)
                {
                    break;
                }
            }

            if (data.Length > 0 && data[data.Length - 1] == ',')
                data.Remove(data.Length - 1, 1);

            return data.ToString();
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _objects = null;
                    _referencedObjects = null;
                }

                streamReader?.Dispose();
                streamReader = null;
                fileStream?.Dispose();
                fileStream = null;

                disposedValue = true;
            }
        }

        ~LgfReader()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
