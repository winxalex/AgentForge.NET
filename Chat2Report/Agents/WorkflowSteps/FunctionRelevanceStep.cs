using Chat2Report.Agents.Generated.IO.FunctionRelevance;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Services;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Ги вчитува дефинициите на СИТЕ достапни табеларно-вредносни функции од шемата.
    /// </summary>
    public class FunctionRelevanceStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataSchemaService _dataSchemaService;
        private readonly SchemaProcessingSettings _settings;
        private readonly ILogger<FunctionRelevanceStep> _logger;

        public FunctionRelevanceStep(
            IDataSchemaService dataSchemaService,
            IOptions<SchemaProcessingSettings> settings,
            ILogger<FunctionRelevanceStep> logger)
        {
            _dataSchemaService = dataSchemaService;
            _settings = settings.Value;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Loading all available table-valued function definitions...");

            var functionDefinitions = await _dataSchemaService
                .GetTableValueFunctionsAsync(_settings.FunctionDescriptionProperty, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Loaded {Count} function definitions.", functionDefinitions.Count);

            return new Outputs { FunctionDefinitions = functionDefinitions };
        }
    }
}