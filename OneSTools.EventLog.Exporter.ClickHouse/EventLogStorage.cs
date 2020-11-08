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

            CreateEventLogItemsTable();
        }

        private void CreateEventLogItemsTable()
        {
            var commandText =
                @"CREATE TABLE IF NOT EXISTS EventLogItems
                (
                    Id Int64 Codec(DoubleDelta, LZ4),
                    FileName LowCardinality(String),
                    EndPosition Int64 Codec(DoubleDelta, LZ4),
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

        public async Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var commandText = "SELECT TOP 1 FileName, EndPosition FROM EventLogItems ORDER BY Id DESC";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync())
            {

                return (reader.GetString(0), reader.GetInt64(1));
            }
            else
                return ("", 0);
        }

        public async Task WriteEventLogDataAsync(List<EventLogItem> entities, CancellationToken cancellationToken = default)
        {
            using var copy = new ClickHouseBulkCopy(_connection)
            {
                DestinationTableName = "EventLogItems",
                BatchSize = entities.Count
            };

            var data = entities.Select(item => new object[] {
                item.Id,
                item.FileName ?? "",
                item.EndPosition,
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
