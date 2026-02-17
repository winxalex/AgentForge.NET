using Chat2Report.Agents.Generated.IO.DomainVectorSearch;
using Chat2Report.Models;
using Chat2Report.Extensions;
using Chat2Report.Options;
using Microsoft.Extensions.VectorData;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class DomainVectorSearchStepOptions
    {
        public int TopK { get; set; } = 5;
        public double ScoreThreshold { get; set; } = 0.8;
    }

    /// <summary>
    /// Врши хибридно векторско пребарување во 'IVectorStore' за да пронајде
    /// кандидатски домеини врз основа на даден embedding.
    /// </summary>
    public class DomainVectorSearchStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IVectorStoreRecordCollection<ulong, DomainDefinition> _domainCollection;
        private readonly ILogger<DomainVectorSearchStep> _logger;
        private readonly SchemaProcessingSettings _schemaSettings;
        private DomainVectorSearchStepOptions _options = new();

        public DomainVectorSearchStep(
            IVectorStore vectorStore,
            IOptions<SchemaProcessingSettings> settings,
            ILogger<DomainVectorSearchStep> logger)
        {
            _logger = logger;
            _schemaSettings = settings.Value;
            _domainCollection = vectorStore.GetCollection<ulong, DomainDefinition>(_schemaSettings.DomainCollectionName);
        }

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<DomainVectorSearchStepOptions>();
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (inputs.Embedding.IsEmpty)
            {
                _logger.LogWarning("Input embedding is empty. Skipping vector search.");
                return new Outputs { CandidateDomains = new List<CandidateDomainWithScores>() };
            }

            var searchOptions = new HybridSearchOptionsWithScoreFiltering<DomainDefinition>
            {
                Top = _options.TopK,
                ScoreFilter = score => score < _options.ScoreThreshold
            };

            // Забелешка: Стариот код се обидуваше да десеријализира ReadOnlyMemory<float> од JsonElement.
            // Новата архитектура го решава ова во UniversalAgent.ConvertValue, па овде влезот е веќе точен.
            var searchResults = await ((IKeywordHybridSearch<DomainDefinition>)_domainCollection)
                .HybridSearchAsync(inputs.Embedding, System.Array.Empty<string>(), searchOptions, cancellationToken)
                .ConfigureAwait(false);

            var candidateDomains = await searchResults.Results
                .Select(r => new CandidateDomainWithScores
                {
                    Domain = r.Record,
                    Score = r.Score.GetValueOrDefault(double.MaxValue)
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Found {Count} candidate domains via vector search.", candidateDomains.Count);

            return new Outputs { CandidateDomains = candidateDomains };
        }
    }
}