﻿using Chat2Report.Agents.Factories;
using Chat2Report.Models;
using Chat2Report.Providers;
using Microsoft.Extensions.AI;
using Chat2Report.Services;
using Microsoft.Extensions.Options;
using MudBlazor;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Threading.Tasks;


namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Апстрактна класа која ја содржи споделената логика за сите чекори
    /// кои комуницираат со LLM клиенти.
    /// </summary>
    public abstract class BaseLlmCallStep
    {
        protected readonly IChatClientFactory ClientFactory;
        protected readonly IPromptProvider PromptProvider;
        private readonly ITemplateEngine _templateEngine;
        protected readonly AgentsConfiguration GlobalConfig;
        protected readonly ILogger _logger;

        protected BaseLlmCallStep(
            IChatClientFactory clientFactory,
            IPromptProvider promptProvider,
            ITemplateEngine templateEngine,
            IOptions<AgentsConfiguration> agentsConfig,
            ILogger logger)
        {
            ClientFactory = clientFactory;
            PromptProvider = promptProvider;
            _templateEngine = templateEngine;
            GlobalConfig = agentsConfig.Value;
            _logger = logger;
        }

        protected async Task<(string system, string user)> PreparePromptsAsync(string? systemPromptName, string? userPromptName, Dictionary<string, object> templateData)
        {
            var systemPromptTemplate = await PromptProvider.GetPromptAsync(systemPromptName).ConfigureAwait(false);
            var userPromptTemplate = await PromptProvider.GetPromptAsync(userPromptName).ConfigureAwait(false);

            var systemPrompt = _templateEngine.Compile(systemPromptTemplate, templateData);
            var userPrompt = _templateEngine.Compile(userPromptTemplate, templateData);

            return (systemPrompt, userPrompt);
        }


        #region Client Configuration Merging

        /// <summary>
        /// Креира ефективна конфигурација за клиентот со спојување на глобалната конфигурација
        /// и опциите дефинирани на ниво на чекор. Опциите од чекорот имаат предност.
        /// </summary>
        protected ClientConfig CreateEffectiveClientConfig(string clientName, ChatOptions? stepSpecificOptions)
        {
            if (!GlobalConfig.Clients.TryGetValue(clientName, out var globalClientConfig))
            {
                return null;
            }

            // Креирај длабока копија за да не се менува глобалната конфигурација
            var effectiveConfig = JsonSerializer.Deserialize<ClientConfig>(JsonSerializer.Serialize(globalClientConfig));

           

            // Спој ги опциите. Оние од чекорот (_options.Options) имаат предност.
            var baseOptions = effectiveConfig?.Options ?? new ChatOptions();

            // Ако нема опции во чекорот, врати ја копираната глобална конфигурација
            if (stepSpecificOptions == null) return effectiveConfig?? new ClientConfig();

            var overrideOptions = stepSpecificOptions;

            effectiveConfig.Options = new ChatOptions
            {
                Temperature = overrideOptions.Temperature ?? baseOptions.Temperature,
                MaxOutputTokens = overrideOptions.MaxOutputTokens ?? baseOptions.MaxOutputTokens,
                TopP = overrideOptions.TopP ?? baseOptions.TopP,
                TopK = overrideOptions.TopK ?? baseOptions.TopK,
                FrequencyPenalty = overrideOptions.FrequencyPenalty ?? baseOptions.FrequencyPenalty,
                PresencePenalty = overrideOptions.PresencePenalty ?? baseOptions.PresencePenalty,
                Seed = overrideOptions.Seed ?? baseOptions.Seed,
                ResponseFormat = overrideOptions.ResponseFormat ?? baseOptions.ResponseFormat,
                StopSequences = overrideOptions.StopSequences ?? baseOptions.StopSequences,
                Tools = overrideOptions.Tools ?? baseOptions.Tools,

                
                ToolMode = overrideOptions.ToolMode ?? baseOptions.ToolMode,
                AdditionalProperties = overrideOptions.AdditionalProperties ?? baseOptions.AdditionalProperties,

            };

            var additionalProperties = effectiveConfig.Options.AdditionalProperties;

            //It is expected that custom ReponseFormat is stored in AdditionaProperties
            //Format of ResponseFormat in Json based on Models.ResponseFormat is for example:

            //    "ResponseFormat": {
            //        "Type": "Type",// Text, Json, Type
            //        "Format": "Chat2Report.Models.MyCustomResponseType, Chat2Report.Models"
            //    }



            if (additionalProperties!=null && additionalProperties.TryGetValue("ResponseFormat",out ResponseFormat responseFormat))
            {
                if (responseFormat.Type == ResponseFormatType.Type)
                {
                    var type = Type.GetType(responseFormat.Format, throwOnError: true);
                    _logger.LogDebug("LlmCallStep using type as response format: {Type}", type);

                    JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
                    System.Text.Json.Nodes.JsonNode jsonNode = jsonSerializerOptions.GetJsonSchemaAsNode(type);
                    _logger.LogDebug("LlmCallStep generated JSON schema from type: {JsonNode}", jsonNode.ToJsonString());

                    JsonElement schemaElement = JsonDocument.Parse(jsonNode.ToJsonString()).RootElement;
                    effectiveConfig.Options.ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement);
                }
                else if (responseFormat.Type == ResponseFormatType.Json)
                {
                    string jsonSchemaString = responseFormat.Format;
                    _logger.LogDebug("LlmCallStep using JSON schema as response format: {JsonSchema}", jsonSchemaString);

                    JsonDocument schemaDocument = JsonDocument.Parse(jsonSchemaString);
                    JsonElement schemaElement = schemaDocument.RootElement;
                    _logger.LogDebug("LlmCallStep parsed JSON schema: {SchemaElement}", schemaElement);

                    effectiveConfig.Options.ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement);
                }
            }



            return effectiveConfig;
        }

       



#endregion


    }
}