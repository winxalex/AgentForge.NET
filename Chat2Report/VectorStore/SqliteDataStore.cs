
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging; // Required NuGet package

namespace Chat2Report.VectorStore
{
    
    public class SqliteDataStore<TRecord> : IDataStore<ulong, TRecord>, IAsyncDisposable where TRecord : class
    {
        private readonly string _connectionString;
        
        private readonly SqliteConnection _connection;
        private readonly JsonSerializerOptions _jsonOptions;
        
        private readonly SemaphoreSlim _connectionLock = new(1, 1); // Protect connection and table state access

        private readonly ILogger<SqliteDataStore<TRecord>> _logger;
        /// <summary>
        /// Initializes a new instance of the SqliteDataStore class, configuring the connection.
        /// The specific table to use must be set later by calling EnsureTableAsync.
        /// </summary>
        /// <param name="databaseFilePath">Path to the SQLite database file.</param>
        public SqliteDataStore(string databaseFilePath,ILogger<SqliteDataStore<TRecord>> logger=null)
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
                throw new ArgumentException("Database file path cannot be empty.", nameof(databaseFilePath));


            if (!Path.IsPathRooted(databaseFilePath))
            {
                databaseFilePath = Path.GetFullPath(databaseFilePath, AppContext.BaseDirectory);
            }


            _logger = logger ?? LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<SqliteDataStore<TRecord>>();

            // Ensure directory exists
            var dir = Path.GetDirectoryName(databaseFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _connectionString = $"Data Source={databaseFilePath}";
            _connection = new SqliteConnection(_connectionString);
            _jsonOptions = new JsonSerializerOptions
            {
                // Configure JSON options as needed
                // PropertyNameCaseInsensitive = true,
                // WriteIndented = false
            };
            // tableName is intentionally left null here
        }

        // Basic table name sanitization
        private string SanitizeTableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Table name cannot be empty.", nameof(name));
            // Allow alphanumeric and underscore, replace others
            return System.Text.RegularExpressions.Regex.Replace(name, @"[^\w]", "_");
        }

        /// <summary>
        /// Ensures the specified table exists in the database and prepares this instance
        /// to operate on that table. Must be called before any data operations.
        /// </summary>
        /// <param name="tableName">Name of the table to store records in.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown if table name is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if EnsureTableAsync was already called with a different table name.</exception>
        public async Task EnsureCreatedAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty.", nameof(tableName));

            string sanitizedTableName = SanitizeTableName(tableName);

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken); // Ensure connection is open  

                                // Create the table if it does not exist  
                var createTableCommandText = $@"  
                   CREATE TABLE IF NOT EXISTS ""{sanitizedTableName}"" (  
                       ""Id"" INTEGER PRIMARY KEY NOT NULL, -- Maps to ulong (SQLite INTEGER is 64-bit signed)  
                       ""Data"" TEXT NOT NULL             -- Stores JSON serialized TRecord  
                   );";

                using var createTableCommand = new SqliteCommand(createTableCommandText, _connection);
                await createTableCommand.ExecuteNonQueryAsync(cancellationToken);

                
            }catch(Exception ex)
            {
                Console.WriteLine($"Error ensuring table '{sanitizedTableName}': {ex.Message}");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }


        private async Task OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            // This method assumes the caller holds the _connectionLock if needed
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
                // Enable WAL mode for better concurrency
                using var walCommand = new SqliteCommand("PRAGMA journal_mode=WAL;", _connection);
                await walCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

       

        // --- IDataStore Implementation ---

        public async Task UpsertAsync(ulong key,string tableName, TRecord record, CancellationToken cancellationToken = default)
        {
           
            string jsonData = JsonSerializer.Serialize(record, _jsonOptions);

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var commandText = $@"
                    INSERT INTO ""{tableName}"" (""Id"", ""Data"") VALUES (@Id, @Data)
                    ON CONFLICT(""Id"") DO UPDATE SET ""Data"" = excluded.""Data"";";

                using var command = new SqliteCommand(commandText, _connection);
                command.Parameters.AddWithValue("@Id", (long)key);
                command.Parameters.AddWithValue("@Data", jsonData);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task UpsertBatchAsync(IEnumerable<KeyValuePair<ulong, TRecord>> records, string tableName, CancellationToken cancellationToken = default)
        {
            
            var recordsList = records.ToList();
            if (!recordsList.Any()) return;

            await _connectionLock.WaitAsync(cancellationToken);
            SqliteTransaction? transaction = null;
            try
            {
                await OpenConnectionAsync(cancellationToken);
                transaction = _connection.BeginTransaction();

                var commandText = $@"
                    INSERT INTO ""{tableName}"" (""Id"", ""Data"") VALUES (@Id, @Data)
                    ON CONFLICT(""Id"") DO UPDATE SET ""Data"" = excluded.""Data"";";

                using var command = new SqliteCommand(commandText, _connection, transaction);
                command.Parameters.Add("@Id", SqliteType.Integer);
                command.Parameters.Add("@Data", SqliteType.Text);

                foreach (var kvp in recordsList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string jsonData = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                    command.Parameters["@Id"].Value = (long)kvp.Key;
                    command.Parameters["@Data"].Value = jsonData;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
                _connectionLock.Release();
            }
        }

        public async Task<TRecord?> GetAsync(ulong key, string tableName, CancellationToken cancellationToken = default)
        {
            

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var commandText = $@"SELECT ""Data"" FROM ""{tableName}"" WHERE ""Id"" = @Id;";
                using var command = new SqliteCommand(commandText, _connection);
                command.Parameters.AddWithValue("@Id", (long)key);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (result is string jsonData)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<TRecord>(jsonData, _jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing record with key {key}: {ex.Message}");
                        return null;
                    }
                }
                return null;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<IEnumerable<KeyValuePair<ulong, TRecord>>> GetBatchAsync(IEnumerable<ulong> keys, string tableName, CancellationToken cancellationToken = default)
        {
            var keyList = keys.ToList();
            if (!keyList.Any()) return Enumerable.Empty<KeyValuePair<ulong, TRecord>>();

            var results = new List<KeyValuePair<ulong, TRecord>>();

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var placeholders = string.Join(",", keyList.Select((_, i) => $"@Id{i}"));
                var commandText = $@"SELECT ""Id"", ""Data"" FROM ""{tableName}"" WHERE ""Id"" IN ({placeholders});";

                using var command = new SqliteCommand(commandText, _connection);
                for (int i = 0; i < keyList.Count; i++)
                {
                    command.Parameters.AddWithValue($"@Id{i}", (long)keyList[i]);
                }

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = (ulong)reader.GetInt64(0);
                    var jsonData = reader.GetString(1);
                    try
                    {
                        var record = JsonSerializer.Deserialize<TRecord>(jsonData, _jsonOptions);
                        if (record != null)
                        {
                            results.Add(new KeyValuePair<ulong, TRecord>(id, record));
                        }
                        else
                        {
                            _logger.LogWarning($"Deserialization resulted in null for key {id} in batch.");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, $"Error deserializing record with key {id} in batch.");
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
            }

            return results;
        }


        public async Task DeleteAsync(ulong key, string tableName, CancellationToken cancellationToken = default)
        {
            

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var commandText = $@"DELETE FROM ""{tableName}"" WHERE ""Id"" = @Id;";
                using var command = new SqliteCommand(commandText, _connection);
                command.Parameters.AddWithValue("@Id", (long)key);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DeleteBatchAsync(IEnumerable<ulong> keys, string tableName, CancellationToken cancellationToken = default)
        {
            
            var keyList = keys.ToList();
            if (!keyList.Any()) return;

            await _connectionLock.WaitAsync(cancellationToken);
            SqliteTransaction? transaction = null;
            try
            {
                await OpenConnectionAsync(cancellationToken);
                transaction = _connection.BeginTransaction();

                var placeholders = string.Join(",", keyList.Select((_, i) => $"@Id{i}"));
                var commandText = $@"DELETE FROM ""{tableName}"" WHERE ""Id"" IN ({placeholders});";

                using var command = new SqliteCommand(commandText, _connection, transaction);
                for (int i = 0; i < keyList.Count; i++)
                {
                    command.Parameters.AddWithValue($"@Id{i}", (long)keyList[i]);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Retrieves all records from the ensured table. WARNING: Can be inefficient for large tables.
        /// </summary>
        public async Task<IEnumerable<KeyValuePair<ulong, TRecord>>> GetAllAsync(string tableName, CancellationToken cancellationToken = default)
        {
           
            var results = new List<KeyValuePair<ulong, TRecord>>();

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var commandText = $@"SELECT ""Id"", ""Data"" FROM ""{tableName}"";";
                using var command = new SqliteCommand(commandText, _connection);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = (ulong)reader.GetInt64(0);
                    var jsonData = reader.GetString(1);
                    try
                    {
                        var record = JsonSerializer.Deserialize<TRecord>(jsonData, _jsonOptions);
                        if (record != null)
                        {
                            results.Add(new KeyValuePair<ulong, TRecord>(id, record));
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Deserialization resulted in null for key {id}. Skipping.");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing record with key {id} in GetAllAsync: {ex.Message}");
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
            }
            return results;
        }

        // --- IAsyncDisposable Implementation ---

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            // Release the lock before disposing connection to avoid potential deadlocks if EnsureTableAsync is somehow called during disposal
            _connectionLock?.Release(); // Ensure it's released if held
            _connectionLock?.Dispose();


            if (_connection != null)
            {
                // Best effort to close gracefully before disposing
                if (_connection.State == System.Data.ConnectionState.Open)
                {
                    try
                    {
                        await _connection.CloseAsync().ConfigureAwait(false);
                    }
                    catch {/* Ignore errors during close */}
                }
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> ExistAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty.", nameof(tableName));

            string sanitizedTableName = SanitizeTableName(tableName);

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                await OpenConnectionAsync(cancellationToken);
                var commandText = $@"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@TableName;";
                using var command = new SqliteCommand(commandText, _connection);
                command.Parameters.AddWithValue("@TableName", sanitizedTableName);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result != null && (long)result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking existence of table '{sanitizedTableName}': {ex.Message}");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }


        // Optional Finalizer: Kept for safety, but note the potential issues with blocking calls
        ~SqliteDataStore()
        {
            // Avoid blocking here if possible. Fire-and-forget or very short timeout.
            // Consider logging if the DisposeAsync wasn't called.
            // DisposeAsyncCore().AsTask().Wait(500); // Reduced timeout, still not ideal
            DisposeAsyncCore().GetAwaiter().GetResult(); // Blocking alternative if absolutely necessary, but risky in finalizer
        }
    }
}
