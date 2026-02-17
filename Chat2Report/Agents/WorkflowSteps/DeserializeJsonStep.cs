﻿using Chat2Report.Agents.Generated.IO.DeserializeJson;
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Utilities;
using Microsoft.Extensions.AI;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class DeserializeJsonStepOptions
    {
        public string? TargetType { get; set; }
    }

    /// <summary>
    /// Десеријализира влезен податок (string или StreamableResult) во силно-типизиран .NET објект.
    /// </summary>
    public class DeserializeJsonStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly ILogger<DeserializeJsonStep> _logger;
        private DeserializeJsonStepOptions _options;

        public DeserializeJsonStep(ILogger<DeserializeJsonStep> logger) => _logger = logger;

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<DeserializeJsonStepOptions>();
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            object? deserializedObject;

            switch (inputs.Source)
            {
                case StreamableResult streamableResult:
                    _logger.LogInformation("Input is a StreamableResult. Awaiting materialized result...");
                    var materializedString = await streamableResult.GetAwaitableResult().ConfigureAwait(false);
                    var extractedJsonFromStream = JsonExtractor.ExtractJson(materializedString);
                    deserializedObject = DeserializeFromString(extractedJsonFromStream);
                    break;
                case string s:
                    _logger.LogInformation("Input is a string. Deserializing from string...");
                    var extractedJson = JsonExtractor.ExtractJson(s);
                    if (string.IsNullOrWhiteSpace(extractedJson))
                        throw new JsonException("Failed to extract valid JSON from the input string.");
                    deserializedObject = DeserializeFromString(extractedJson);
                    break;
                case null:
                    throw new InvalidOperationException("Input 'source' for DeserializeJsonStep cannot be null.");
                default:
                    throw new InvalidOperationException($"DeserializeJsonStep received an unsupported input type: {inputs.Source.GetType().Name}. Expected types `string` or `StreamableResult` ");
            }
            
            if (deserializedObject == null)
                throw new JsonException($"Failed to deserialize JSON to the target type.");
            
            return new Outputs { DeserializedObject = deserializedObject };
        }
        
        private object? DeserializeFromString(string json)
        {
            if (_options.TargetType != null)
            {
                var targetType = Type.GetType(_options.TargetType, throwOnError: false, ignoreCase: true);
                if (targetType == null)
                    throw new InvalidOperationException($"Target type '{_options.TargetType}' could not be resolved.");
                
                return JsonSerializer.Deserialize(json, targetType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            
            return JsonToDictionaryConverter.DeserializeToDictionary(json);
        }
    }
}