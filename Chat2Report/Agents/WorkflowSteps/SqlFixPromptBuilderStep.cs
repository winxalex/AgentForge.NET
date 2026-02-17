﻿using Chat2Report.Agents.Generated.IO.SqlFixPromptBuilder;
using Chat2Report.Extensions;
using Chat2Report.Models;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class SqlFixPromptBuilderOptions
    {
        public uint MaxRetries { get; set; } = 3;
    }

    /// <summary>
    /// Управува со логиката за повторни обиди при поправка на невалиден SQL.
    /// Го зголемува бројачот и фрла исклучок ако максималниот број обиди е надминат.
    /// </summary>
    public class SqlFixPromptBuilderStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ILogger<SqlFixPromptBuilderStep> _logger;
        private SqlFixPromptBuilderOptions _options = new();

        public SqlFixPromptBuilderStep(ILogger<SqlFixPromptBuilderStep> logger)
        {
            _logger = logger;
        }

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<SqlFixPromptBuilderOptions>();
        }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            // Земи го бројачот од влезот, ако не постои почни од 0
            int currentAttempts = inputs.AttemptCounterIn ?? 0;

            if (currentAttempts >= _options.MaxRetries)
            {
                var errorMsg = $"SQL query fixing failed after {_options.MaxRetries} attempts. Aborting workflow.";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }

            int nextAttempt = currentAttempts + 1;
            _logger.LogInformation("SQL fix attempt number {AttemptNumber} of {MaxRetries}.", nextAttempt, _options.MaxRetries);

            // Врати го новиот, зголемен бројач
            return Task.FromResult(new Outputs { AttemptCounterOut = nextAttempt });
        }
    }
}