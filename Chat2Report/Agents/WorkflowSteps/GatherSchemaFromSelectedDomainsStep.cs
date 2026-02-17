using Chat2Report.Agents.Generated.IO.GatherSchemaFromSelectedDomains;
using Chat2Report.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Ги филтрира кандидатските домени според рангирањето од LLM
    /// и ги извлекува сите релевантни табели и погледи од избраните домени.
    /// </summary>
    public class GatherSchemaFromSelectedDomainsStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ILogger<GatherSchemaFromSelectedDomainsStep> _logger;

        public GatherSchemaFromSelectedDomainsStep(ILogger<GatherSchemaFromSelectedDomainsStep> logger)
        {
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            var candidateDict = inputs.OriginalCandidates?
                .ToDictionary(c => c.Domain.Name, c => c, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, CandidateDomainWithScores>(StringComparer.OrdinalIgnoreCase);

            var reRankedDomains = new List<CandidateDomainWithScores>();

            if (inputs.RankedDomainNames != null)
            {
                foreach (var domainObj in inputs.RankedDomainNames)
                {
                    // LLM обично враќа речник { "name": "...", "reasoning": "..." }
                    if (domainObj is JsonElement jsonElem)
                    {
                        // Справи се со случајот кога UniversalAgent.ConvertValue проследил JsonElement
                        if (jsonElem.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        {
                            var domainName = nameProp.GetString();
                            if (!string.IsNullOrEmpty(domainName) && candidateDict.TryGetValue(domainName, out var candidate))
                                reRankedDomains.Add(candidate);
                        }
                    }
                    else if (domainObj is Dictionary<string, object> domainDict &&
                             domainDict.TryGetValue("name", out var nameObj) && nameObj is string domainName)
                    {
                        if (!string.IsNullOrEmpty(domainName) && candidateDict.TryGetValue(domainName, out var candidate))
                            reRankedDomains.Add(candidate);
                    }
                }
            }

            _logger.LogInformation("Finalized {Count} re-ranked domains.", reRankedDomains.Count);

            var allTablesNamesOfRelevantViews = reRankedDomains
                .SelectMany(d => d.Domain.TableNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var allViewsNamesOfRelevantDomains = reRankedDomains
                .SelectMany(d => d.Domain.ViewNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

           
            // Prepare the output for relevant domains
            var relevantDomains = reRankedDomains.Select(d =>
                new Dictionary<string, object>
                {
                    { "name", d.Domain.Name },
                    { "description", d.Domain.Description }
                }).Cast<object>()
                .ToList();

            // Extract table and view names from the top-ranked domains



            return Task.FromResult(new Outputs
            {
                RelevantDomains = relevantDomains,
                RelevantTablesNames = allTablesNamesOfRelevantViews,
                RelevantViewsNames = allViewsNamesOfRelevantDomains
            });
        }
    }
}