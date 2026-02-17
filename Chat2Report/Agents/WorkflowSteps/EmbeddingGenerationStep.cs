﻿using Chat2Report.Agents.Factories;
using Chat2Report.Agents.Generated.IO.EmbeddingGeneration; // Генериран namespace
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class EmbeddingGenerationStepOptions
    {
        public List<string> ClientNames { get; set; }
    }

    /// <summary>
    /// Генерира густ вектор (embedding) од даден текст, користејќи конфигуриран embedding модел.
    /// </summary>
    public class EmbeddingGenerationStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IChatClientFactory _clientFactory;
        private readonly AgentsConfiguration _globalConfig;
        private readonly ILogger<EmbeddingGenerationStep> _logger;
        private EmbeddingGenerationStepOptions _options;

        public EmbeddingGenerationStep(
            IChatClientFactory clientFactory,
            IOptions<AgentsConfiguration> agentsConfig,
            ILogger<EmbeddingGenerationStep> logger)
        {
            _clientFactory = clientFactory;
            _globalConfig = agentsConfig.Value;
            _logger = logger;
        }

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<EmbeddingGenerationStepOptions>();

            if (_options.ClientNames == null || !_options.ClientNames.Any())
                throw new JsonException("The 'ClientNames' property is required in the configuration for EmbeddingGenerationStep but was not found.");
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputs.TextToEmbed))
            {
                _logger.LogWarning("Input 'TextToEmbed' is empty. Returning empty vector.");
                return new Outputs { EmbeddingVector = ReadOnlyMemory<float>.Empty };
            }

            if (_options.ClientNames == null || !_options.ClientNames.Any())
            {
                throw new InvalidOperationException("No 'ClientNames' configured for EmbeddingGenerationStep.");
            }

            var clientConfigs = _options.ClientNames
                .Select(name => _globalConfig.Clients.TryGetValue(name, out var config) ? config : null)
                .Where(config => config != null)
                .ToList();

            if (!clientConfigs.Any())
            {
                throw new InvalidOperationException("No valid embedding clients could be configured from the provided 'ClientNames'.");
            }

            // Create a generator. In the future, this could be a resilient one.
            var embeddingGenerator = _clientFactory.CreateEmbeddingGenerator(clientConfigs);

            _logger.LogInformation("Generating embedding with client '{ClientName}'.", clientConfigs.First().Type);
            var embeddingResult = await embeddingGenerator.GenerateAsync(new[] { inputs.TextToEmbed }, null, cancellationToken).ConfigureAwait(false);

            var vector = embeddingResult.FirstOrDefault()?.Vector ?? ReadOnlyMemory<float>.Empty;
            return new Outputs { EmbeddingVector = vector };
        }
    }
}