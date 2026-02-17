﻿using Chat2Report.Agents.Factories;
using Chat2Report.Agents.Generated.IO.StreamingLlmCall;
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class StreamingLlmCallStepOptions
    {
        public List<string> ClientNames { get; set; }
        public string? SystemPrompt { get; set; }
        public string? UserPrompt { get; set; }
        public ChatOptions? Options { get; set; }
        public ClientTimeoutOptions? TimeoutOptions { get; set; }
    }

    /// <summary>
    /// Врши стриминг повик до LLM и враќа 'StreamableResult'.
    /// </summary>
    public class StreamingLlmCallStep : BaseLlmCallStep, IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private StreamingLlmCallStepOptions _options;
        private readonly AgentsConfiguration _agentsConfiguration;

        public StreamingLlmCallStep(
            IChatClientFactory factory, IPromptProvider promptProvider,
            ITemplateEngine templateEngine,
            IOptions<AgentsConfiguration> agentsConfig, 
            ILogger<StreamingLlmCallStep> logger)
            : base(factory, promptProvider, templateEngine, agentsConfig, logger)
        {
            _agentsConfiguration = agentsConfig.Value;
        }

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<StreamingLlmCallStepOptions>();

            if (_options.ClientNames == null || !_options.ClientNames.Any())
                throw new JsonException("The 'ClientNames' property is required in the configuration for StreamingLlmCallStep but was not found.");
        }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (_options.ClientNames == null || !_options.ClientNames.Any())
                throw new InvalidOperationException("No 'ClientNames' configured for StreamingLlmCallStep.");

            // 1. Соберете ги сите ефективни конфигурации заедно со нивните оригинални имиња (ID)
            var clientConfigs = _options.ClientNames
                .Select(name => (Id: name, Config: CreateEffectiveClientConfig(name, _options.Options)))
                .Where(cc => cc.Config != null)
                .ToList();

            if (!clientConfigs.Any())
                throw new InvalidOperationException("No valid clients could be configured from the provided 'ClientNames'.");

            _logger.LogInformation("Initiating streaming LLM call with {ClientCount} potential clients.", clientConfigs.Count);

            // 2. Одреди кои тајмаути да се користат: специфичните од чекорот, или глобалните.
            var timeoutOptions = _options.TimeoutOptions ?? _agentsConfiguration.DefaultTimeoutOptions ?? new ClientTimeoutOptions();

            // 3. Креирајте еден ResilientChatClient кој интерно ќе управува со failover
            var resilientChatClient = ClientFactory.CreateChatClient(clientConfigs, timeoutOptions);
            var stream = InternalStreamResponseAsync(inputs.TemplateData, resilientChatClient, cancellationToken);

            return Task.FromResult(new Outputs { StreamableResult = new StreamableResult(stream) });
        }

        private async IAsyncEnumerable<string> InternalStreamResponseAsync(Dictionary<string, object> templateData, IChatClient client,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (systemPrompt, userPrompt) = await PreparePromptsAsync(_options.SystemPrompt, _options.UserPrompt, templateData);
            var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt), new(ChatRole.User, userPrompt) };
            
            _logger.LogDebug($"Streaming LlmCallStep Prompt :\n{systemPrompt}\n {userPrompt}");


            // ResilientChatClient ќе ги користи опциите дефинирани при неговото креирање,
            // па овде можеме да проследиме null.
            await foreach (var update in client.GetStreamingResponseAsync(messages, null, cancellationToken))
            {
                if (update?.Text != null) yield return update.Text;
            }
        }
    }
}