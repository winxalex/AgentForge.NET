﻿using Chat2Report.Models;
using Chat2Report.Providers;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// !!!NOT FINISHED
    /// Configuration options for the StepExecutorStep.
    /// </summary>
    public class StepExecutorOptions
    {
        /// <summary>
        /// The full definition of the step to be executed dynamically.
        /// </summary>
        public WorkflowStepDefinition StepDefinition { get; set; }
    }

    /// <summary>
    /// A workflow step that dynamically executes another workflow step based on its definition.
    /// This allows for dynamic, configurable step execution within a workflow pipeline.
    /// </summary>
    public class StepExecutorStep : IConfigurableStep
    {
        private readonly IWorkflowStepFactory _workflowStepFactory;
        private readonly ILogger<StepExecutorStep> _logger;
        private StepExecutorOptions _options;

        public StepExecutorStep(IWorkflowStepFactory workflowStepFactory, ILogger<StepExecutorStep> logger)
        {
            _workflowStepFactory = workflowStepFactory;
            _logger = logger;
        }

        public void Configure(JsonElement config)
        {
            _options = config.Deserialize<StepExecutorOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                     ?? throw new ArgumentException("Failed to deserialize 'Config' section for StepExecutorStep.");

            if (_options.StepDefinition == null || string.IsNullOrWhiteSpace(_options.StepDefinition.Type))
                throw new ArgumentException("A valid 'StepDefinition' with a 'Type' must be provided in the 'Config' for StepExecutorStep.");
        }

        // public async Task<WorkflowState> ExecuteAsync(WorkflowState state, IStatePersitanceProvider memory, IStepExecutionContext context, CancellationToken cancellationToken)
        // {
        //     _logger.LogInformation("StepExecutor dynamically creating and executing inner step of type '{StepType}'.", _options.StepDefinition.Type);
        //     // 1. Factory CREATES the step
        //     var innerStep = _workflowStepFactory.CreateStep(_options.StepDefinition);
        //     // 2. Executor EXECUTES the step
        //     return await innerStep.ExecuteAsync(state, memory, context, cancellationToken);
        // }
    }
}