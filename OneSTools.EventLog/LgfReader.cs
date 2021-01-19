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
        private FileStream _fileStream;
        private BracketsListReader _bracketsReader;

        public string LgfPath { get; }
        /// <summary>
        /// Key tuple - first value is an object type, second value is an object number in the array
        /// </summary>
        private Dictionary<(ObjectType, int), string> _objects = new Dictionary<(ObjectType, int), string>();
        /// <summary>
        /// Key tuple - first value is an object type, second value is an object number in the array
        /// Value tuple - first value is an object value, second value is a guid of the object value
        /// </summary>
        private Dictionary<(ObjectType, int), (string, string)> _referencedObjects = new Dictionary<(ObjectType, int), (string, string)>();
        private bool _disposedValue;

        public LgfReader(string lgfPath)
        {
            LgfPath = lgfPath;
        }

        private void ReadTill(ObjectType objectType, int number, long position, CancellationToken cancellationToken = default)
        {
            InitializeStreams();

            bool stop = false;

            while (!stop && !_bracketsReader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var itemData = _bracketsReader.NextNode();

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
            if (_fileStream is null)
            {
                if (!File.Exists(LgfPath))
                    throw new Exception("Cannot find \"1Cv8.lgf\"");

                _fileStream = new FileStream(LgfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                _bracketsReader = new BracketsListReader(_fileStream);
            }
        }

        public void SetPosition(long position, CancellationToken cancellationToken = default)
        {
            ReadTill(ObjectType.None, 0, position, cancellationToken);
        }

        public long GetPosition()
        {
            InitializeStreams();

            return _bracketsReader.Position;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _objects = null;
                    _referencedObjects = null;
                }

                _bracketsReader?.Dispose();
                _bracketsReader = null;
                _fileStream?.Dispose();
                _fileStream = null;

                _disposedValue = true;
            }
        }

        ~LgfReader()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
