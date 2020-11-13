using OneSTools.BracketsFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog
{
    internal class LgpReader<T> : IDisposable where T : class, IEventLogItem, new()
    {
        private LgfReader _lgfReader;
        private FileStream fileStream;
        private StreamReader streamReader;
        private long _lastPosition;
        private FileSystemWatcher _lgpFileWatcher;
        private bool disposedValue;

        public string LgpPath { get; private set; }
        public string LgpFileName => Path.GetFileNameWithoutExtension(LgpPath);

        public LgpReader(string lgpPath, LgfReader lgfReader)
        {
            LgpPath = lgpPath;
            _lgfReader = lgfReader;
        }

        public T ReadNextEventLogItem(CancellationToken cancellationToken = default)
        {
            if (disposedValue)
                throw new ObjectDisposedException(typeof(LgpReader<T>).Name);

            InitializeStreams();

            return ReadEventLogItemData(cancellationToken);
        }

        public void SetPosition(long position)
        {
            InitializeStreams();

            streamReader.SetPosition(position);
        }

        public long GetPosition()
        {
            InitializeStreams();

            return streamReader.GetPosition();
        }

        private void InitializeStreams()
        {
            if (fileStream is null)
            {
                if (!File.Exists(LgpPath))
                    throw new Exception($"Cannot find lgp file by path {LgpPath}");

                _lgpFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(LgpPath), "*.lgp")
                {
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Attributes
                };
                _lgpFileWatcher.Deleted += LgpFileWatcher_Deleted;
                _lgpFileWatcher.EnableRaisingEvents = true;

                fileStream = new FileStream(LgpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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

        private void LgpFileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted && LgpPath == e.FullPath)
            {
                Dispose();
            }
        }

        private (string Data, long EndPosition) ReadNextEventLogItemData(CancellationToken cancellationToken = default)
        {
            StringBuilder data = new StringBuilder();

            var quotesQuantity = 0;
            var bracketsQuantity = 0;
            bool start = false;
            bool end = false;
            long endPosition = 0;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_lastPosition != 0)
                    SetPosition(_lastPosition);

                var currentLine = streamReader.ReadLine();

                if (currentLine is null)
                {
                    _lastPosition = GetPosition();
                    break;
                }
                else
                    _lastPosition = 0;

                if (!start && currentLine.Length > 0 && currentLine[0] == '{')
                    start = true;

                if (start)
                {
                    data.Append(currentLine + '\n');

                    bracketsQuantity += currentLine.Count(c => c == '{');
                    bracketsQuantity -= currentLine.Count(c => c == '}');
                    quotesQuantity += currentLine.Count(c => c == '"');

                    if (currentLine == "}," && bracketsQuantity == 0 || streamReader.EndOfStream)
                    {
                        end = true;

                        if (bracketsQuantity != 0)
                            end = (bracketsQuantity % 2) == 0;
                    }
                }

                if (end)
                {
                    endPosition = GetPosition();
                    break;
                }
            }

            if (data.Length > 0 && data[data.Length - 1] == ',')
                data.Remove(data.Length - 1, 1);

            return (data.ToString(), endPosition);
        }

        private T ReadEventLogItemData(CancellationToken cancellationToken = default)
        {
            (string Data, long EndPosition) data = ReadNextEventLogItemData(cancellationToken);

            if (data.Data == string.Empty)
                return null;

            return ParseEventLogItemData(data.Data, data.EndPosition, cancellationToken);
        }

        private T ParseEventLogItemData(string eventLogItemData, long endPosition, CancellationToken cancellationToken = default)
        {
            var parsedData = BracketsFileParser.Parse(eventLogItemData);

            var eventLogItem = new T
            {
                DateTime = DateTime.ParseExact((string)parsedData[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                TransactionStatus = GetTransactionPresentation((string)parsedData[1]),
                FileName = LgpFileName,
                EndPosition = endPosition
            };

            var (Value, Uuid) = _lgfReader.GetReferencedObjectValue(ObjectType.Users, (int)parsedData[3], cancellationToken);
            eventLogItem.UserUuid = Uuid;
            eventLogItem.User = Value;

            eventLogItem.Computer = _lgfReader.GetObjectValue(ObjectType.Computers, (int)parsedData[4], cancellationToken);
            eventLogItem.Application = _lgfReader.GetObjectValue(ObjectType.Applications, (int)parsedData[5], cancellationToken);
            eventLogItem.Connection = (int)parsedData[6];
            eventLogItem.Event = _lgfReader.GetObjectValue(ObjectType.Events, (int)parsedData[7], cancellationToken);
            eventLogItem.Severity = GetSeverityPresentation((string)parsedData[8]);
            eventLogItem.Comment = (string)parsedData[9];

            (Value, Uuid) = _lgfReader.GetReferencedObjectValue(ObjectType.Metadata, (int)parsedData[10], cancellationToken);
            eventLogItem.MetadataUuid = Uuid;
            eventLogItem.Metadata = Value;

            eventLogItem.Data = GetData(parsedData[11]).Trim();
            eventLogItem.DataPresentation = (string)parsedData[12];
            eventLogItem.Server = _lgfReader.GetObjectValue(ObjectType.Servers, (int)parsedData[13], cancellationToken);

            var mainPort = _lgfReader.GetObjectValue(ObjectType.MainPorts, (int)parsedData[14], cancellationToken);
            if (mainPort != "")
                eventLogItem.MainPort = int.Parse(mainPort);

            var addPort = _lgfReader.GetObjectValue(ObjectType.AddPorts, (int)parsedData[15], cancellationToken);
            if (addPort != "")
                eventLogItem.AddPort = int.Parse(addPort);

            eventLogItem.Session = (int)parsedData[16];

            return eventLogItem;
        }

        private string GetTransactionPresentation(string str)
        {
            switch (str)
            {
                case "U":
                    return "Commited";
                case "C":
                    return "RolledBack";
                case "R":
                    return "InProgress";
                case "N":
                    return "NotApplicable";
                default:
                    return "";
            }
        }

        private string GetData(BracketsFileNode node)
        {
            var dataType = (string)node[0];

            switch (dataType)
            {
                case "R": // Reference
                    return (string)node[1];
                case "U": // Undefined
                    return "";
                case "S": // String
                    return (string)node[1];
                case "B": // Boolean
                    return (string)node[1] == "0" ? "false" : "true";
                case "P": // Complex data
                    StringBuilder str = new StringBuilder();

                    var subDataNode = node[1];

                    //var subDataType = (int)subDataNode[0];
                    // What's known (subDataNode):
                    // 1 - additional data of "Authentication (Windows auth) in thin or thick client"
                    // 2 - additional data of "Authentication in COM connection" event
                    // 6 - additional data of "Authentication in thin or thick client" event
                    // 11 - additional data of "Access denied" event

                    // I hope this is temporarily method
                    var subDataCount = subDataNode.Count - 1;

                    if (subDataCount > 0)
                        for (int i = 1; i <= subDataCount; i++)
                        {
                            var value = GetData(subDataNode[i]);

                            if (value != string.Empty)
                                str.AppendLine($"Item {i}: {value}");
                        }

                    return str.ToString();
                default:
                    return "";
            }
        }

        private string GetSeverityPresentation(string str)
        {
            return str switch
            {
                "I" => "Information",
                "E" => "Error",
                "W" => "Warning",
                "N" => "Notification",
                _ => "",
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                }

                streamReader?.Dispose();
                streamReader = null;
                fileStream = null;

                _lgpFileWatcher?.Dispose();
                _lgpFileWatcher = null;
                
                disposedValue = true;
            }
        }

        ~LgpReader()
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
