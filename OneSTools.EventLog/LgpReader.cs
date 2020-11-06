using OneSTools.BracketsFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OneSTools.EventLog
{
    sealed class LgpReader : IDisposable
    {
        private LgfReader _lgfReader;
        private FileStream fileStream;
        private StreamReader streamReader;

        public string LgpPath { get; private set; }
        public bool EndOfStream => streamReader.EndOfStream;

        public LgpReader(string lgpPath, LgfReader lgfReader)
        {
            LgpPath = lgpPath;
            _lgfReader = lgfReader;
        }

        public EventLogItem ReadNextEventLogItem(CancellationToken cancellationToken)
        {
            InitializeStreams();

            return ReadEventLogItemData(cancellationToken);
        }

        public void SetPosition(long position)
        {
            InitializeStreams();

            streamReader.SetPosition(position);
        }

        public long GetPosition(long position)
        {
            InitializeStreams();

            return streamReader.GetPosition();
        }

        public void GoToEventLogItem(int position)
        {
            InitializeStreams();

            streamReader.SetPosition(position);
            
            GoToEndOfEvent();
        }

        private void GoToEndOfEvent()
        {
            while (!streamReader.EndOfStream)
            {
                var currentLine = streamReader.ReadLine();

                if (currentLine == null)
                    break;

                if (Regex.IsMatch(currentLine, @"^}\.$"))
                    break;
            }
        }

        private void InitializeStreams()
        {
            if (fileStream is null)
            {
                if (!File.Exists(LgpPath))
                    throw new Exception($"Cannot find lgp file by path {LgpPath}");

                fileStream = new FileStream(LgpPath, FileMode.Open, FileAccess.Read);
                streamReader = new StreamReader(fileStream);

                // skip header (first three lines)
                for (int i = 0; i < 3; i++)
                    streamReader.ReadLine();
            }
        }

        private string ReadNextEventLogItemData(CancellationToken cancellationToken)
        {
            StringBuilder data = new StringBuilder();

            var quotesCount = 0;
            var bracketsCount = 0;
            bool end = false;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var currentLine = streamReader.ReadLine();

                if (currentLine is null)
                    break;

                bracketsCount += currentLine.Count(c => c == '{');
                bracketsCount -= currentLine.Count(c => c == '}');
                quotesCount += currentLine.Count(c => c == '"');

                data.Append(currentLine + '\n');

                if (bracketsCount == 0)
                    end = true;

                if (bracketsCount == 0 && quotesCount != 0)
                    end = (quotesCount % 2) == 0;

                if (end)
                    break;
            }

            if (data.Length > 0 && data[data.Length - 1] == ',')
                data.Remove(data.Length - 1, 1);

            return data.ToString();
        }

        private EventLogItem ReadEventLogItemData(CancellationToken cancellationToken)
        {
            var data = ReadNextEventLogItemData(cancellationToken);

            if (data == string.Empty)
                return null;

            return ParseEventLogItemData(data, cancellationToken);
        }

        private EventLogItem ParseEventLogItemData(string eventLogItemData, CancellationToken cancellationToken)
        {
            var parsedData = BracketsFileParser.Parse(eventLogItemData);

            var eventLogItem = new EventLogItem
            {
                DateTime = DateTime.ParseExact((string)parsedData[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                TransactionStatus = GetTransactionPresentation((string)parsedData[1])
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

            eventLogItem.Data = GetData(parsedData[11]);
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
                case "R":
                    return (string)node[1];
                case "U":
                    return "";
                case "S":
                    return (string)node[1];
                case "P":
                    StringBuilder str = new StringBuilder();

                    var subDataNode = node[1];
                    var subDataType = (int)subDataNode[0];

                    if (subDataType == 1) // This is additional data of "Authentication (Windows auth) in thin or thick client"
                    {
                        var osUserName = GetData(subDataNode[1]);
                    }
                    else if (subDataType == 6) // This is additional data of "Authentication in thin or thick client" event
                    {
                        var userName = GetData(subDataNode[1]);
                    }
                    else if (subDataType == 2) // This is additional data of "Authentication in COM connection" event
                    {
                        var userName = GetData(subDataNode[1]);
                    }
                    else if (subDataType == 11) // This is additional data of "Access denied" event
                    {
                        var right = GetData(subDataNode[1]);
                        var action = GetData(subDataNode[2]);
                        var accessObject = GetData(subDataNode[3]);
                    }
                    else
                    {
                        
                    }

                    return str.ToString();
                default:
                    return "";
            }
        }

        private string GetSeverityPresentation(string str)
        {
            switch (str)
            {
                case "I":
                    return "Information";
                case "E":
                    return "Error";
                case "W":
                    return "Warning";
                case "N":
                    return "Notification";
                default:
                    return "";
            }
        }

        public void Dispose()
        {
            streamReader.Dispose();
        }
    }
}
