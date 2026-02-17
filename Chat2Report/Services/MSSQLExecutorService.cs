using Chat2Report.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Implements <see cref="ISqlExecutorService"/> for Microsoft SQL Server.
    /// </summary>
    public class MSSQLExecutorService : ISqlExecutorService
    {
        private readonly string _connectionString;
        private readonly ILogger<MSSQLExecutorService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MSSQLExecutorService"/> class.
        /// </summary>
        /// <param name="dataStoreSettings">The data store settings containing the connection string.</param>
        /// <param name="logger">The logger.</param>
        public MSSQLExecutorService(IOptions<DataStoreSettings> dataStoreSettings, ILogger<MSSQLExecutorService> logger)
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

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                using (var command = new SqlCommand(sqlQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
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
            }
            _logger.LogInformation("Query executed successfully, returning {RowCount} rows.", results.Count);
            return results;
        }
    }
}