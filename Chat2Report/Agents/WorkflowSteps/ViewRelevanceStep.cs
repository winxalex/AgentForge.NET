using Chat2Report.Agents.Generated.IO.ViewRelevance;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Services;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Ги вчитува целосните дефиниции на погледи (views) за дадена листа
    /// на имиња на погледи.
    /// </summary>
    public class ViewRelevanceStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataSchemaService _dataSchemaService;
        private readonly SchemaProcessingSettings _settings;
        private readonly ILogger<ViewRelevanceStep> _logger;

        public ViewRelevanceStep(
            IDataSchemaService dataSchemaService,
            IOptions<SchemaProcessingSettings> settings,
            ILogger<ViewRelevanceStep> logger)
        {
            _dataSchemaService = dataSchemaService;
            _settings = settings.Value;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (inputs.ViewNames == null || !inputs.ViewNames.Any())
            {
                _logger.LogWarning("No view names provided to ViewRelevanceStep. Returning empty list of definitions.");
                return new Outputs { ViewDefinitions = new List<ViewDefinition>() };
            }

            _logger.LogInformation("Loading full definitions for {Count} views.", inputs.ViewNames.Count);

            var viewDefinitions = await _dataSchemaService
                .GetViewDefinitionsAsync(inputs.ViewNames, _settings.ViewDescriptionProperty, cancellationToken)
                .ConfigureAwait(false);

            return new Outputs { ViewDefinitions = viewDefinitions };
        }
    }
}