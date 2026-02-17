using Chat2Report.Agents.Generated.IO.SqlPreExecuteClean;
using Chat2Report.Models;
using Chat2Report.Services;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Го чисти и подготвува генерираниот SQL за извршување.
    /// Екстрахира од markdown, додава LIMIT/TOP клаузула преку ICleanupSQLService, и проверува за DML/DDL.
    /// </summary>
    public class SqlPreExecuteCleanStep : IWorkflowStep<Inputs, Outputs>
    {
        private readonly ILogger<SqlPreExecuteCleanStep> _logger;
        private readonly ICleanupSQLService _cleanupSqlService;

        public SqlPreExecuteCleanStep(
            ILogger<SqlPreExecuteCleanStep> logger,
            ICleanupSQLService cleanupSqlService)
        {
            _logger = logger;
            _cleanupSqlService = cleanupSqlService;
        }

        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputs.RawSql))
            {
                _logger.LogError("Input 'RawSql' is empty. Cannot clean SQL.");
                throw new ArgumentException("Input 'RawSql' cannot be empty.", nameof(inputs));
            }

            string finalSql = _cleanupSqlService.Clean(inputs.RawSql, inputs.Analysis);

            if (ContainsDisallowedStatements(finalSql))
            {
                _logger.LogError("Generated SQL contains disallowed statements: {SQL}", finalSql);
                throw new InvalidOperationException("Generated SQL contains disallowed DML/DDL statements. Operation aborted for security reasons.");
            }

            _logger.LogInformation("Cleaned and validated SQL: {SQL}", finalSql);

            return Task.FromResult(new Outputs { CleanedSql = finalSql });
        }

        private bool ContainsDisallowedStatements(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return false;
            }

            string pattern = @"\b(INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|CREATE|EXEC|EXECUTE)\b";
            return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase);
        }
    }
}

