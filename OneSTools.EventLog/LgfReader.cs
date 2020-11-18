using OneSTools.BracketsFile;
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

        private void ReadTill(ObjectType objectType, int number, long position, CancellationToken cancellationToken = default)
        {
            InitializeStreams();

            bool stop = false;

            while (!stop && !streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var itemStrBuilder = ReadNextItemData(cancellationToken);
                var itemData = BracketsFileParser.ParseBlock(itemStrBuilder);

                var ot = (ObjectType)(int)itemData[0];

                // Skip unknown object types
                if (ot >= ObjectType.Unknown)
                    continue;

                switch (ot)
                {
                    case ObjectType.Users:
                    case ObjectType.Metadata:
                        var key = (ot, (int)itemData[3]);
                        var value = ((string)itemData[2], (string)itemData[1]);

                        if (_referencedObjects.ContainsKey(key))
                            _referencedObjects.Remove(key);

                        _referencedObjects.Add(key, value);

                        if (objectType == ObjectType.None && GetPosition() >= position)
                        {
                            stop = true;
                            break;
                        }

                        if (ot == objectType && key.Item2 == number)
                            stop = true;
                            
                        break;
                    default:
                        var key1 = (ot, (int)itemData[2]);
                        var value1 = (string)itemData[1];

                        if (_objects.ContainsKey(key1))
                            _objects.Remove(key1);

                        _objects.Add(key1, value1);

                        if (objectType == ObjectType.None && GetPosition() >= position)
                        {
                            stop = true;
                            break;
                        }

                        if (ot == objectType && key1.Item2 == number)
                            stop = true;

                        break;
                }
            }
        }

        private StringBuilder ReadNextItemData(CancellationToken cancellationToken = default)
        {
            StringBuilder data = new StringBuilder();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var currentLine = streamReader.ReadLine();

                if (currentLine is null)
                    break;

                data.Append(currentLine);

                var blockEndIndex = BracketsFileParser.GetNodeEndIndex(data, 0);

                if (blockEndIndex != -1)
                {
                    data.Remove(blockEndIndex + 1, data.Length - 1 - blockEndIndex);
                    break;
                }
            }

            return data;
        }

        public string GetObjectValue(ObjectType objectType, int number, CancellationToken cancellationToken = default)
        {
            if (number == 0)
                return "";

            if (_objects.TryGetValue((objectType, number), out var value))
                return value;
            else
                ReadTill(objectType, number, 0, cancellationToken);

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
                ReadTill(objectType, number, 0, cancellationToken);

            if (_referencedObjects.TryGetValue((objectType, number), out value))
                return value;
            else
                throw new Exception($"Cannot find objectType {objectType} with number {number} in referenced objects collection");
        }

        private void InitializeStreams()
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
        }

        public void SetPosition(long position, CancellationToken cancellationToken = default)
        {
            ReadTill(ObjectType.None, 0, position, cancellationToken);
        }

        public long GetPosition()
        {
            InitializeStreams();

            return streamReader.GetPosition();
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
