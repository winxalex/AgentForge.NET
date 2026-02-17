using Chat2Report.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Implements <see cref="ISqlExecutorService"/> for SQLite.
    /// </summary>
    public class SQLiteExecutorService : ISqlExecutorService
    {
        private readonly string _connectionString;
        private readonly ILogger<SQLiteExecutorService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteExecutorService"/> class.
        /// </summary>
        /// <param name="dataStoreSettings">The data store settings containing the connection string.</param>
        /// <param name="logger">The logger.</param>
        public SQLiteExecutorService(IOptions<DataStoreSettings> dataStoreSettings, ILogger<SQLiteExecutorService> logger)
        {
            _connectionString = dataStoreSettings.Value.ConnectionString;
            _logger = logger;

            if (string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogError("Database connection string is not configured in DataStoreSettings.");
                throw new InvalidOperationException("Database connection string is not configured.");
            }
        }

        /// <inheritdoc/>
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
        {
            var results = new List<Dictionary<string, object>>();
            _logger.LogInformation("Executing SQL query: {SqlQuery}", sqlQuery);

            await using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = new SqliteCommand(sqlQuery, connection))
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    var columnNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columnNames.Add(reader.GetName(i));
                    }

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < columnNames.Count; i++)
                        {
                            var value = reader.GetValue(i);
                            row[columnNames[i]] = value == DBNull.Value ? null : value;
                        }
                        results.Add(row);
                    }
                }
            }

            _logger.LogInformation("Query executed successfully, returning {RowCount} rows.", results.Count);
            return results;
        }
    }
}
