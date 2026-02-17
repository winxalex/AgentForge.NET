using Chat2Report.Agents.Generated.IO.ResolveAmbiguity;
using Chat2Report.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Го обработува изборот на корисникот од Human-in-the-Loop интеракција.
    /// Преместува корисник од 'двосмислена' во 'решена' состојба.
    /// </summary>
    public class ResolveAmbiguityStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ILogger<ResolveAmbiguityStep> _logger;

        public ResolveAmbiguityStep(ILogger<ResolveAmbiguityStep> logger)
        {
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Resolving ambiguity for '{PartialName}' with user selection '{SelectedName}'.",
                inputs.ParcialName, inputs.SelectedFullName);

            var resolvedMappings = new Dictionary<string, string>(inputs.ResolvedMappingsIn ?? new());
            var ambiguousMappings = new Dictionary<string, List<UserModel>>(inputs.AmbiguousMappingsIn ?? new());

            // Премести од двосмислена во решена состојба
            resolvedMappings[inputs.ParcialName] = inputs.SelectedFullName;

            // Исчисти го записот од двосмислените мапирања
            ambiguousMappings.Remove(inputs.ParcialName);

            // Ажурирај го оригиналниот кориснички упит
            var updatedQuery = inputs.CurrentUserQuery.Replace(
                inputs.ParcialName,
                inputs.SelectedFullName,
                StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("Updated 'user_query' to '{NewQuery}'", updatedQuery);

            return Task.FromResult(new Outputs
            {
                ResolvedMappingsOut = resolvedMappings,
                AmbiguousMappingsOut = ambiguousMappings,
                UpdatedUserQuery = updatedQuery
            });
        }
    }
}