﻿using Chat2Report.Agents.Factories;
using Chat2Report.Agents.Generated.IO.LlmCall; // Генериран namespace
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Services;
using HandlebarsDotNet;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text.Json;


namespace Chat2Report.Agents.WorkflowSteps
{
    public class LlmCallStepOptions
    {
        public List<string> ClientNames { get; set; }
        public string? SystemPrompt { get; set; }
        public string? UserPrompt { get; set; }
        public ChatOptions? Options { get; set; }
        public ClientTimeoutOptions? TimeoutOptions { get; set; }
    }

    /// <summary>
    /// Врши стандарден (не-стриминг) повик до LLM и го враќа целосниот одговор како суров стринг.
    ///  //**НОВ, КОНЦИЗЕН НАЧИН:**
    ////```json
    ////{
    ////  "Type": "LlmCall",
    ////  "InstanceConfiguration": {
    ////    "InputMapping": {
    ////      // "Проследи сè што имаш во templateData."
    ////      "templateData.*": "*" 
    ////    },
    ////    // Може дури и да се комбинира!
    ////    // "templateData.some_specific_key": "another_state_key"
    ////  },
    ////  // ...
    ////}
    ////```
    /// </summary>
    public class LlmCallStep :BaseLlmCallStep, IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
       
        private readonly ILogger<LlmCallStep> _logger;
        private LlmCallStepOptions _options;

        private readonly AgentsConfiguration  _agentsConfiguration;
        private IAIFunctionFactoryService _aIFunctionFactoryService;

        public LlmCallStep(
    IChatClientFactory clientFactory,
    IPromptProvider promptProvider,
    ITemplateEngine templateEngine,
    IOptions<AgentsConfiguration> agentsConfiguration,
    IAIFunctionFactoryService aIFunctionFactoryService,
    ILogger<LlmCallStep> logger)
    : base(clientFactory, promptProvider, templateEngine, agentsConfiguration, logger)
        {
            _logger = logger;
            _agentsConfiguration = agentsConfiguration.Value;
            _aIFunctionFactoryService = aIFunctionFactoryService;
        }


        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<LlmCallStepOptions>();

            if (_options.ClientNames == null || !_options.ClientNames.Any())
                throw new JsonException("The 'ClientNames' property is required in the configuration for LlmCallStep but was not found.");

            // The default deserializer for LlmCallStepOptions won't be able to handle the 'Tools' property
            // because it's a list of AITool, which is an abstract type. We need to manually extract the
            // tool configuration, deserialize it into a concrete List<ToolConfig>, and then use our factory.
            if (config.TryGetProperty("Options", out var optionsElement) &&
                optionsElement.TryGetProperty("Tools", out var toolsElement))
            {
                var toolConfigs = toolsElement.Deserialize<List<ToolConfig>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (toolConfigs != null && toolConfigs.Any())
                {
                    _options.Options ??= new ChatOptions();
                    _options.Options.Tools = _aIFunctionFactoryService.CreateToolsFromConfig(toolConfigs);
                }
            }
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (_options.ClientNames == null || !_options.ClientNames.Any())
                throw new InvalidOperationException("No 'ClientNames' configured for LlmCallStep.");

            var (userPrompt,systemPrompt) = await PreparePromptsAsync(_options?.UserPrompt,_options?.SystemPrompt,inputs.TemplateData);
            var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt), new(ChatRole.User, userPrompt) };

            _logger.LogDebug($"LlmCallStep Prompt :\n{systemPrompt}\n {userPrompt}");

            // 1. Соберете ги сите ефективни конфигурации заедно со нивните оригинални имиња (ID)
            var clientConfigs = _options.ClientNames
                .Select(name => (Id: name, Config: CreateEffectiveClientConfig(name, _options.Options)))
                .Where(cc => cc.Config != null)
                .ToList();

            if (!clientConfigs.Any())
                throw new InvalidOperationException("No valid clients could be configured from the provided 'ClientNames'.");

            // Одреди кои тајмаути да се користат: специфичните од чекорот, или глобалните.
            var timeoutOptions = _options.TimeoutOptions ?? _agentsConfiguration.DefaultTimeoutOptions ?? new ClientTimeoutOptions();

            // 2. Креирајте еден ResilientChatClient кој интерно ќе управува со failover
            var resilientChatClient = ClientFactory.CreateChatClient(clientConfigs, timeoutOptions);

            
            // 3. Повикајте го клиентот само еднаш. Failover-от е транспарентен.
            var response = await resilientChatClient.GetResponseAsync(messages, null, cancellationToken).ConfigureAwait(false);
            var assistantMessage = response.Messages?.LastOrDefault(m => m.Role == ChatRole.Assistant);
            var rawResult = assistantMessage?.Text ?? string.Empty;
            _logger.LogDebug("{client} LLM call completed. RESPONSE :\n{result}", resilientChatClient, rawResult);

            return new Outputs { Result = rawResult };
        }



    }
}
