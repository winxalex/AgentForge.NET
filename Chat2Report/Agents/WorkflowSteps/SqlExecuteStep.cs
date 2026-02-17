using Chat2Report.Agents.Generated.IO.SqlExecute;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Data.Common;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Извршува SQL барање (query) врз базата на податоци.
    /// Враќа или табела со резултати, или порака за грешка.
    /// </summary>
    public class SqlExecuteStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ISqlExecutorService _sqlExecutor;
        private readonly ILogger<SqlExecuteStep> _logger;

        public SqlExecuteStep(ISqlExecutorService sqlExecutor, ILogger<SqlExecuteStep> logger)
        {
            _sqlExecutor = sqlExecutor;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputs.SqlQuery))
            {
                throw new ArgumentException("Input 'SqlQuery' cannot be empty.", nameof(inputs));
            }

            _logger.LogInformation("Executing SQL query: {Query}", inputs.SqlQuery);

            try
            {
                var resultData = await _sqlExecutor.ExecuteQueryAsync(inputs.SqlQuery, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("SQL query executed successfully, returning {RowCount} rows.", resultData.Count);

                return new Outputs { ResultTable = resultData };
            }
            catch (DbException ex)
            {
                _logger.LogWarning(ex, "A database error occurred while executing the SQL query.");
                return new Outputs { ExecutionError = $"Database Error: {ex.Message}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during SQL execution.");
                return new Outputs { ExecutionError = $"Unexpected Error: {ex.Message}" };
            }
        }

        
    }
}