using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Defines a service for executing SQL queries against a database.
    /// </summary>
    public interface ISqlExecutorService
    {
        /// <summary>
        /// Asynchronously executes a SQL query and returns the result set.
        /// </summary>
        /// <param name="sqlQuery">The SQL query string to execute.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of dictionaries, where each dictionary represents a row, with column names as keys and row values as values.</returns>
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery, CancellationToken cancellationToken = default);
    }
}