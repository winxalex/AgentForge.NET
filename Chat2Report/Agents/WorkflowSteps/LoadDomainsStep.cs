using Chat2Report.Agents.Generated.IO.LoadDomains;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Providers;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Вчитува листа на достапни домеини од конфигурацијата
    /// и ја форматира како стринг за употреба во промпт темплејти.
    /// </summary>
    public class LoadDomainsStep : IWorkflowStep<object, Outputs>
    {
        private readonly ILogger<LoadDomainsStep> _logger;
        private readonly SchemaProcessingSettings _schemaSettings;

        public LoadDomainsStep(ILogger<LoadDomainsStep> logger, IOptions<SchemaProcessingSettings> schemaSettings)
        {
            _logger = logger;
            _schemaSettings = schemaSettings.Value;
        }

      

        /// <summary>
        /// Го извршува чекорот со типски-безбедни влезови и излези.
        /// </summary>
        public Task<Outputs> ExecuteAsync(object inputs, IStepExecutionContext context, CancellationToken cancellationToken)
        {
            var domainListBuilder = new StringBuilder();
            if (_schemaSettings.Domains != null && _schemaSettings.Domains.Any())
            {
                foreach (var domain in _schemaSettings.Domains)
                {
                    domainListBuilder.AppendLine($"- **{domain.Name}**: {domain.Description}");
                }
            }

            _logger.LogInformation("Loaded {DomainCount} available domains for prompt enrichment.", _schemaSettings.Domains?.Count ?? 0);

            var result = new Outputs
            {
                AvailableDomainsString = domainListBuilder.ToString().TrimEnd()
            };

            return Task.FromResult(result);
        }

       
    }
}