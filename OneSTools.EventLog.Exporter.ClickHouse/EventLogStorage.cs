using System;
using System.Collections.Generic;
using System.Text;
using ClickHouse.Client.Copy;
using System.Threading;
using System.Threading.Tasks;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using OneSTools.EventLog.Exporter.Core;
using System.Linq;
using System.Data;
using ClickHouse.Client.ADO.Readers;

namespace OneSTools.EventLog.Exporter.ClickHouse
{
    public class EventLogStorage : IEventLogStorage, IDisposable
    {
        private readonly ClickHouseConnection _connection;
        public EventLogStorage(string connectionString)
        {
            _connection = new ClickHouseConnection(connectionString);
            _connection.Open();

            CreateEventLogPositionsTable();
            CreateEventLogItemsTable();
        }

        private void CreateEventLogPositionsTable()
        {
            var commandText =
                @"CREATE TABLE IF NOT EXISTS EventLogPositions
                (
                    LgpFileName LowCardinality(String),
                    LgpFilePosition Int64 Codec(DoubleDelta, LZ4)
                )
                engine = MergeTree()
                PRIMARY KEY (LgpFileName)
                ORDER BY (LgpFileName)";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            cmd.ExecuteNonQuery();
        }

        private void CreateEventLogItemsTable()
        {
            var commandText =
                @"CREATE TABLE IF NOT EXISTS EventLogItems
                (
                    Id Int64 Codec(DoubleDelta, LZ4),
                    DateTime DateTime Codec(Delta, LZ4),
                    TransactionStatus LowCardinality(String),
                    TransactionDate DateTime Codec(Delta, LZ4),
                    TransactionNumber Int64 Codec(DoubleDelta, LZ4),
                    UserUuid LowCardinality(String),
                    User LowCardinality(String),
                    Computer LowCardinality(String),
                    Application LowCardinality(String),
                    Connection Int64 Codec(DoubleDelta, LZ4),
                    Event LowCardinality(String),
                    Severity LowCardinality(String),
                    Comment String Codec(ZSTD),
                    MetadataUuid LowCardinality(String),
                    Metadata LowCardinality(String),
                    Data String Codec(ZSTD),
                    DataPresentation String Codec(ZSTD),
                    Server LowCardinality(String),
                    MainPort LowCardinality(String),
                    AddPort LowCardinality(String),
                    Session Int64 Codec(DoubleDelta, LZ4)
                )
                engine = MergeTree()
                PARTITION BY (toYYYYMM(DateTime))
                PRIMARY KEY (DateTime, Id)
                ORDER BY (DateTime, Id)
                SETTINGS index_granularity = 8192;";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            cmd.ExecuteNonQuery();
        }

        public async Task<EventLogPosition> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var commandText = "SELECT TOP 1 LgpFileName, LgpFilePosition FROM EventLogPositions";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync())
            {
                var position = new EventLogPosition
                {
                    LgpFileName = reader.GetString(0),
                    LgpFilePosition = reader.GetInt64(1)
                };

                return position;
            }
            else
                return null;
        }

        public async Task WriteEventLogDataAsync(EventLogPosition eventLogPosition, List<EventLogItem> entities, CancellationToken cancellationToken = default)
        {
            await TruncateEventLogPositionsTableAsync(cancellationToken);
            await WriteEventLogPositionAsync(eventLogPosition, cancellationToken);
            await WriteEventLogItemsAsync(entities, cancellationToken);
        }

        private async Task TruncateEventLogPositionsTableAsync(CancellationToken cancellationToken = default)
        {
            var commandText = "TRUNCATE TABLE EventLogPositions";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task WriteEventLogPositionAsync(EventLogPosition eventLogPosition, CancellationToken cancellationToken = default)
        {
            var commandText = $"INSERT INTO EventLogPositions VALUES ('{eventLogPosition.LgpFileName}',{eventLogPosition.LgpFilePosition})";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task WriteEventLogItemsAsync(List<EventLogItem> entities, CancellationToken cancellationToken = default)
        {
            using var copy = new ClickHouseBulkCopy(_connection)
            {
                DestinationTableName = "EventLogItems",
                BatchSize = entities.Count
            };

            var data = entities.Select(item => new object[] { 
                item.Id,
                item.DateTime,
                item.TransactionStatus ?? "",
                item.TransactionDateTime,
                item.TransactionNumber,
                item.UserUuid ?? "",
                item.User ?? "",
                item.Computer ?? "",
                item.Application ?? "",
                item.Connection,
                item.Event ?? "",
                item.Severity ?? "",
                item.Comment ?? "",
                item.MetadataUuid ?? "",
                item.Metadata ?? "",
                item.Data ?? "",
                item.DataPresentation ?? "",
                item.Server ?? "",
                item.MainPort,
                item.AddPort,
                item.Session
            }).AsEnumerable();

            await copy.WriteToServerAsync(data, cancellationToken);
        }

        public void Dispose()
        {
            if (_connection != null)
                _connection.Dispose();
        }
    }
}
