using Chat2Report.Agents.Generated.IO.TableRelevance;
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
    /// Ги вчитува целосните дефиниции на табели (колони, описи, итн.)
    /// за дадена листа на имиња на табели.
    /// </summary>
    public class TableRelevanceStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataSchemaService _dataSchemaService;
        private readonly SchemaProcessingSettings _settings;
        private readonly ILogger<TableRelevanceStep> _logger;

        public TableRelevanceStep(
            IDataSchemaService dataSchemaService,
            IOptions<SchemaProcessingSettings> settings,
            ILogger<TableRelevanceStep> logger)
        {
            _dataSchemaService = dataSchemaService;
            _settings = settings.Value;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (inputs.TableNames == null || !inputs.TableNames.Any())
            {
                _logger.LogWarning("No table names provided to TableRelevanceStep. Returning empty list of definitions.");
                return new Outputs { TableDefinitions = new List<TableDefinition>() };
            }

            _logger.LogInformation("Loading full definitions for {Count} tables.", inputs.TableNames.Count);

            var tableDefinitions = await _dataSchemaService
                .GetTableDefinitionsAsync(inputs.TableNames, _settings.TableDescriptionProperty, cancellationToken)
                .ConfigureAwait(false);

            return new Outputs { TableDefinitions = tableDefinitions };
        }
    }
}