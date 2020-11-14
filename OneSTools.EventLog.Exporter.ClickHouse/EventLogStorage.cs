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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OneSTools.EventLog.Exporter.ClickHouse
{
    public class EventLogStorage<T> : IEventLogStorage<T>, IDisposable where T : class, IEventLogItem, new()
    {
        private const string TABLE_NAME = "EventLogItems";
        private readonly ILogger<EventLogStorage<T>> _logger;
        private readonly ClickHouseConnection _connection;

        public EventLogStorage(ILogger<EventLogStorage<T>> logger, IConfiguration configuration)
        {
            _logger = logger;

            var connectionString = configuration.GetConnectionString("Default");
            if (connectionString == string.Empty)
                throw new Exception("Connection string is not specified");

            _connection = new ClickHouseConnection(connectionString);
            _connection.Open();

            CreateEventLogItemsTable();
        }

        private void CreateEventLogItemsTable()
        {
            var commandText =
                $@"CREATE TABLE IF NOT EXISTS {TABLE_NAME}
                (
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
                    MetadataUuid String Codec(ZSTD),
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
                ORDER BY (DateTime, EndPosition)
                SETTINGS index_granularity = 8192;";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            cmd.ExecuteNonQuery();
        }

        public async Task<(string FileName, long EndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            var commandText = $"SELECT TOP 1 FileName, EndPosition FROM {TABLE_NAME} ORDER BY DateTime DESC, EndPosition DESC";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync())
                return (reader.GetString(0), reader.GetInt64(1));
            else
                return ("", 0);
        }

        public async Task WriteEventLogDataAsync(List<T> entities, CancellationToken cancellationToken = default)
        {
            using var copy = new ClickHouseBulkCopy(_connection)
            {
                DestinationTableName = TABLE_NAME,
                BatchSize = entities.Count
            };

            var data = entities.Select(item => new object[] {
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

            try
            {
                await copy.WriteToServerAsync(data, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write data");
                throw ex;
            }

            _logger.LogDebug($"{DateTime.Now:(hh:mm:ss.fffff)} | {entities.Count} items have been written");
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
