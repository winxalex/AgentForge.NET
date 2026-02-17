using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.VectorStore
{
    /// <summary>
    /// Interface for storing the actual data records associated with vector keys.
    /// USearch only stores keys and vectors.
    /// </summary>
    /// <typeparam name="TKey">The Type of the key used in both USearch and the data store.</typeparam>
    /// <typeparam name="TRecord">The Type of the data record being stored.</typeparam>
    public interface IDataStore<TKey, TRecord> : IAsyncDisposable where TKey : notnull
    {
        /// <summary>
        /// Adds or updates a record in the store.
        /// </summary>
        Task UpsertAsync(TKey key, string tableName, TRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds or updates multiple records in the store.
        /// </summary>
        Task UpsertBatchAsync(IEnumerable<KeyValuePair<TKey, TRecord>> records, string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a record by its key.
        /// </summary>
        Task<TRecord?> GetAsync(TKey key, string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves multiple records by their keys.
        /// </summary>
        Task<IEnumerable<KeyValuePair<ulong,TRecord>>> GetBatchAsync(IEnumerable<TKey> keys, string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a record by its key.
        /// </summary>
        Task DeleteAsync(TKey key, string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple records by their keys.
        /// </summary>
        Task DeleteBatchAsync(IEnumerable<TKey> keys, string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all records (potentially inefficient for large stores).
        /// Used for filtering simulation.
        /// </summary>
        Task<IEnumerable<KeyValuePair<TKey, TRecord>>> GetAllAsync(string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures the specified table exists in the database.
        /// </summary>
        Task EnsureCreatedAsync(string tableName, CancellationToken cancellationToken);


        /// <summary>
        /// Checks if the specified table exists in the database.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>bool</returns>
        Task<bool> ExistAsync(string tableName, CancellationToken cancellationToken = default);
    }
}
