using Chat2Report.Agents.Generated.IO.ExtractUserEntities;
using Chat2Report.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Го обработува суровиот LLM одговор за екстракција на корисници
    /// и го претвора во структурирана листа на 'Person' објекти.
    /// </summary>
    public class ExtractUserEntitiesStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ILogger<ExtractUserEntitiesStep> _logger;

        public ExtractUserEntitiesStep(ILogger<ExtractUserEntitiesStep> logger)
        {
            _logger = logger;
        }

        // Овој чекор нема своја 'Config' секција.
        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (inputs.ExtractionResult?.ExtractedPersons is null)
            {
                _logger.LogWarning("Input 'ExtractionResult' was null or did not contain persons. Assuming no persons were found.");
                return Task.FromResult(new Outputs { PersonsToResolve = new List<Person>() });
            }

            // Филтрирај ги празните записи што LLM-от можеби ги генерирал
            var validPersons = inputs.ExtractionResult.ExtractedPersons
                .Where(p => !string.IsNullOrWhiteSpace(p.FirstName) || !string.IsNullOrWhiteSpace(p.LastName))
                .ToList();

            _logger.LogInformation("Extracted {Count} valid user entities to resolve.", validPersons.Count);

            return Task.FromResult(new Outputs { PersonsToResolve = validPersons });
        }
    }
}