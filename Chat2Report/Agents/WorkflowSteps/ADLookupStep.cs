﻿using Chat2Report.Agents.Generated.IO.ADLookup;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Итерира низ листа на кориснички имиња, пребарува во Active Directory,
    /// и ги категоризира резултатите како решени, двосмислени или непознати.
    /// Обработува само еден корисник по извршување.
    /// </summary>
    public class ADLookupStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IADService _adService;
        private readonly ILogger<ADLookupStep> _logger;

        public ADLookupStep(IADService adService, ILogger<ADLookupStep> logger)
        {
            _adService = adService;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            var entitiesToResolve = new List<Person>(inputs.EntitiesToResolve ?? Enumerable.Empty<Person>());
            var resolvedMappings = new Dictionary<string, string>(inputs.ResolvedMappingsIn ?? new());
            var ambiguousMappings = new Dictionary<string, List<UserModel>>(inputs.AmbiguousMappingsIn ?? new());
            var updatedQuery = inputs.CurrentUserQuery;

            if (!entitiesToResolve.Any())
            {
                _logger.LogInformation("No user entities to resolve. Skipping AD Lookup.");
                return new Outputs
                {
                    EntitiesLeftToResolve = entitiesToResolve,
                    ResolvedMappingsOut = resolvedMappings,
                    AmbiguousMappingsOut = ambiguousMappings,
                    UpdatedUserQuery = updatedQuery,
                    OriginalAmbiguousName = null // No ambiguity to report
                };
            }

            var person = entitiesToResolve.First();
            entitiesToResolve.RemoveAt(0); // Конзумирај го првиот корисник

            var displayName = $"{person.FirstName} {person.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName))
            {
                throw new InvalidOperationException("Resolved entry for person is empty!");
            }

            _logger.LogInformation("Looking up '{Name}' in Active Directory. ({Remaining} remaining)", displayName, entitiesToResolve.Count);
            var adUsers = await _adService.FindUsersByNameAsync(person.FirstName, person.LastName).ConfigureAwait(false);

            string? originalAmbiguousName = null;
            string? unknownUser = null;
            if (adUsers.Count == 1)
            {
                var user = adUsers.First();
                var fullName = $"{user.FirstName} {user.LastName}";
                resolvedMappings[displayName] = fullName;
                updatedQuery = updatedQuery.Replace(displayName, fullName, StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation("Found unique match for '{Name}': {FullName}", displayName, fullName);
            }
            else if (adUsers.Count > 1)
            {
                ambiguousMappings[displayName] = adUsers;
                originalAmbiguousName = displayName;
                _logger.LogWarning("Found {Count} ambiguous matches for '{Name}'.", adUsers.Count, displayName);
            }
            else
            {
                unknownUser = displayName;
                _logger.LogWarning("No matches found for '{Name}'.", displayName);
            }

            return new Outputs
            {
                EntitiesLeftToResolve = entitiesToResolve,
                ResolvedMappingsOut = resolvedMappings,
                AmbiguousMappingsOut = ambiguousMappings,
                UnknownUser = unknownUser,
                UpdatedUserQuery = updatedQuery,
                OriginalAmbiguousName = originalAmbiguousName
            };
        }
    }
}