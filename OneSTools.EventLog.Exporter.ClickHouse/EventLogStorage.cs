using System;
using System.Collections.Generic;
using System.Text;
using ClickHouse.Client.Copy;
using System.Threading;
using System.Threading.Tasks;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using System.Linq;
using System.Data;
using ClickHouse.Client.ADO.Readers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using OneSTools.EventLog.Exporter.Core;

namespace OneSTools.EventLog.Exporter.ClickHouse
{
    public class EventLogStorage<T> : IEventLogStorage<T>, IDisposable where T : class, IEventLogItem, new()
    {
        private const string TABLE_NAME = "EventLogItems";
        private readonly ILogger<EventLogStorage<T>> _logger;
        private readonly string _connectionString;
        private string _databaseName;
        private ClickHouseConnection _connection;

        public EventLogStorage(ILogger<EventLogStorage<T>> logger, string connectionString)
        {
            _logger = logger;

            _connectionString = connectionString;
            if (_connectionString == string.Empty)
                throw new Exception("Connection string is not specified");

            _databaseName = Regex.Match(_connectionString, "(?<=Database=).*?(?=(;|$))", RegexOptions.IgnoreCase).Value;
            _connectionString = Regex.Replace(_connectionString, "Database=.*?(;|$)", "");

            if (string.IsNullOrWhiteSpace(_databaseName))
                throw new Exception("Database name is not specified");
        }

        public EventLogStorage(ILogger<EventLogStorage<T>> logger, IConfiguration configuration) : this(logger, configuration.GetConnectionString("Default"))
        {

        }

        private async Task CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                _connection = new ClickHouseConnection(_connectionString);
                await _connection.OpenAsync(cancellationToken);

                await CreateEventLogItemsDatabaseAsync(cancellationToken);
            }
        }

        private async Task CreateEventLogItemsDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var commandDbText = $@"CREATE DATABASE IF NOT EXISTS {_databaseName}";

            using var cmdDb = _connection.CreateCommand();
            cmdDb.CommandText = commandDbText;
            await cmdDb.ExecuteNonQueryAsync(cancellationToken);

            await _connection.ChangeDatabaseAsync(_databaseName, cancellationToken);

            var commandText =
                $@"CREATE TABLE IF NOT EXISTS {TABLE_NAME}
                (
                    FileName LowCardinality(String),
                    EndPosition Int64 Codec(DoubleDelta, LZ4),
                    LgfEndPosition Int64 Codec(DoubleDelta, LZ4),
                    DateTime DateTime('UTC') Codec(Delta, LZ4),
                    TransactionStatus LowCardinality(String),
                    TransactionDate DateTime('UTC') Codec(Delta, LZ4),
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
                    MainPort Int32 Codec(DoubleDelta, LZ4),
                    AddPort Int32 Codec(DoubleDelta, LZ4),
                    Session Int64 Codec(DoubleDelta, LZ4)
                )
                engine = MergeTree()
                PARTITION BY (toYYYYMM(DateTime))
                ORDER BY (DateTime, EndPosition)
                SETTINGS index_granularity = 8192;";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<(string FileName, long EndPosition, long LgfEndPosition)> ReadEventLogPositionAsync(CancellationToken cancellationToken = default)
        {
            await CreateConnectionAsync(cancellationToken);

            var commandText = $"SELECT TOP 1 FileName, EndPosition, LgfEndPosition FROM {TABLE_NAME} ORDER BY DateTime DESC, EndPosition DESC";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = commandText;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync())
                return (reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2));
            else
                return ("", 0, 0);
        }

        public async Task WriteEventLogDataAsync(List<T> entities, CancellationToken cancellationToken = default)
        {
            await CreateConnectionAsync();

            using var copy = new ClickHouseBulkCopy(_connection)
            {
                DestinationTableName = TABLE_NAME,
                BatchSize = entities.Count
            };

            var data = entities.Select(item => new object[] {
                item.FileName ?? "",
                item.EndPosition,
                item.LgfEndPosition,
                item.DateTime,
                item.TransactionStatus ?? "",
                item.TransactionDateTime == DateTime.MinValue ? new DateTime(1970, 1, 1) : item.TransactionDateTime,
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
                _logger.LogError(ex, $"Failed to write data to {_databaseName}");
                throw ex;
            }

            _logger.LogDebug($"{DateTime.Now:(hh:mm:ss.fffff)} | {entities.Count} items were being written to {_databaseName}");
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
