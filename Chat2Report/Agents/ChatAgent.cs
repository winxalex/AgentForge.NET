//using Chat2Report.Agents.Evaluation;
//using Chat2Report.Agents.Factories;
//using Chat2Report.Agents.Transformers;
//using Chat2Report.Extensions;
//using Chat2Report.Models;

//using Chat2Report.Providers;
//using Chat2Report.Services;
//using Chat2Report.Utilities;
//using HandlebarsDotNet;
//using Microsoft.AutoGen.Contracts;
//using Microsoft.AutoGen.Core;
//using Microsoft.Extensions.AI;
//using Microsoft.Extensions.Options;

//using System.Reflection;
//using System.Text.Json;
//using System.Text.Json.Schema;


//namespace Chat2Report.Agents
//{
//    // Keep TypeSubscription or adjust if AgentId.Type is now directly from config key
//    /**
//     * <summary>
//     * ChatAgent class for handling chat messages. 
//     * First Message received is in first Agent is starting state State.
//     * Agent will receive a Dictionary<string, object> message and apply to its prompt.
//     * Response of LLM is then merged with the State and as next message is sent to the next agent.
//     * </summary>
//     * <param name="id">AgentId for the agent.</param>
//     * <param name="runtime">IAgentRuntime instance for the agent.</param>
//     * <param name="chatClientFactory">IChatClientFactory instance for creating chat clients.</param>
//     * <param name="taskCompletionSource">TaskCompletionSource for handling agent completion.</param>
//     * <param name="configuration">Configuration instance for agent configuration.</param>
//     * <param name="sharedMemory">SharedMemoryProvider instance for shared memory management.</param>
//     * <param name="expressionEvaluator">IExpressionEvaluator instance for evaluating expressions.</param>
//     * <param name="transformerExecutor">IMessageTransformer instance for executing message transformations.</param>
//     * <param name="delegateCache">IDelegateCache instance for caching reflection artifacts.</param>
//     * <param name="toolInstanceProvider">IToolInstanceProvider instance for providing tool instances.</param>
//     * <param name="logger">ILogger instance for logging.</param>
//     */
//    //[TypeSubscription("chat")] // This might become less relevant if agent types are dynamic keys in config
//    public class ChatAgent : InferenceAgent, IHandle<WorkflowState>
//    {
//        // The <think> tag is no longer used for reasoning. Reasoning is expected as a 'reasoning' field in the JSON output.
//        private const string TERMINATE = "TERMINATE";
//        private const string WORKFLOW_TOPIC_ID = "workflow_topic_id";
//        private readonly Options __chatOptions;
//        private readonly IHandlebars __handleBars;
//        private AgentDefinition __agentOptions;
//        private readonly IToolInstanceProvider __toolInstanceProvider;

//        private readonly IPromptProvider _promptProvider; // <<< Add prompt provider

//        private readonly IExpressionEvaluator _expressionEvaluator;
//        private readonly IMessageTransformer _transformerExecutor;
//        private readonly IDelegateCache __delegateCache; // <<< Added delegate cache dependency
//        private readonly ITopicTerminationService __topicTerminationService;
//        private readonly ISharedMemoryProvider __sharedMemoryProvider;
//        // Constructor updated to accept new dependencies and config
//        private  bool _isStreamingEnabled;

//        public ChatAgent(AgentId id,
//           IAgentRuntime runtime,
//           ITopicTerminationService topicTerminationService,
//           IOptions<AgentsConfiguration> agentsConfiguration,

//           IChatClientFactory chatClientFactory,
//           IExpressionEvaluator expressionEvaluator, // Inject evaluator
//           IMessageTransformer transformerExecutor, // Inject transformer
//           IDelegateCache delegateCache, // <<< Inject cache
//           IPromptProvider promptProvider, // <<< Inject prompt provider
//           ISharedMemoryProvider sharedMemoryProvider = null, // Inject shared memory
//           IToolInstanceProvider toolInstanceProvider = null,
//           IUIStreamBroker broker = null,
//           ILogger<BaseAgent> logger = null
//            )
//            : base(id, runtime, TryCreateDescription(id, agentsConfiguration?.Value),
//        TryCreateChatClient(agentsConfiguration?.Value, chatClientFactory, id, logger),
//        TryCreateEmbeddingGenerator(agentsConfiguration?.Value, chatClientFactory, id, logger),
//        logger)

//        //: base(id, runtime, $"ChatAgent configured for {id.Type} ({id.Key})", // Dynamic description
//        //         null, // Client creation logic
//        //         null, // Embedding generator creation
//        //         logger)



//        {
//            try
//            {
//                logger?.Log(LogLevel.Debug, $"ChatAgent {Id} constructing...");

//                var configuration = agentsConfiguration?.Value ?? throw new ArgumentNullException(nameof(agentsConfiguration));


//                if (configuration?.Agents == null)
//                    throw new ArgumentException("Configuration or Agents cannot be null");

//                if (!configuration.Agents.ContainsKey(id.Type))
//                    throw new ArgumentException($"No agent configuration found for type: {id.Type}");

//                var agentConfig = configuration.Agents[id.Type];
//                if (agentConfig.Clients == null || !agentConfig.Clients.Any())
//                    throw new ArgumentException($"Clients configuration missing for agent type: {id.Type}");

//                _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider)); // Store provider
//                __delegateCache = delegateCache;
//                __sharedMemoryProvider = sharedMemoryProvider;
//                __agentOptions = configuration?.Agents[id.Type] ?? throw new ArgumentException($"No configuration found for agent type: {id.Type}");

//                // If no Display options are provided in the configuration, set up default behavior.
//                // This ensures that content is displayed by default, even without explicit config.
//                if (__agentOptions.Display == null)
//                {
//                    _logger?.LogDebug("Agent {AgentId} has no Display configuration. Applying default display settings.", Id);
//                    __agentOptions.Display = new DisplayOptions
//                    {
//                        // By default, enable all content types without a specific template.
//                        // The ProcessDisplayAsync method will fall back to showing raw content.
//                        Text = new DisplayContentOptions { Enabled = false },
//                        Reasoning = new DisplayContentOptions { Enabled = true },
//                        Table = new DisplayContentOptions { Enabled = true }
//                    };
//                }

//                __toolInstanceProvider = toolInstanceProvider ??= new ActivatorToolInstanceProvider(); // Default to Activator if not provided

//                __topicTerminationService = topicTerminationService ?? throw new ArgumentNullException(nameof(topicTerminationService));
//                __sharedMemoryProvider = sharedMemoryProvider ?? throw new ArgumentNullException(nameof(sharedMemoryProvider));
//                _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
//                _transformerExecutor = transformerExecutor ?? throw new ArgumentNullException(nameof(transformerExecutor));

//                __chatOptions = CreateChatOptions(__agentOptions);

//                // Determine if streaming is enabled for this agent.
//                // The top-level UseStreaming property on the agent options overrides the client-level setting.
//                _isStreamingEnabled = __agentOptions.UseStreaming ?? __agentOptions.Clients.First().UseStreaming;

//                __handleBars = Handlebars.Create();

//                __handleBars.RegisterHelper("toJSON", (writer, context, arguments) =>
//                {
//                    if (arguments.Length != 1)
//                    {
//                        writer.WriteSafeString("{\"error\": \"toJSON helper expects exactly one value argument.\"}");
//                        return;
//                    }
//                    object valueToSerialize = arguments[0];
//                    try
//                    {
//                        var options = new JsonSerializerOptions { WriteIndented = false };
//                        string jsonResult = JsonSerializer.Serialize(valueToSerialize, options);
//                        writer.WriteSafeString(jsonResult); // Use WriteSafeString for JSON
//                        //writer.Write(jsonResult); // Use WriteSafeString for JSON
//                    }
//                    catch (Exception ex)
//                    {
//                        writer.WriteSafeString($"{{\"error\": \"Serialization failed: {ex.Message}\"}}");
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                logger?.LogError(ex, $"ChatAgent {Id} construction failed.");
//                throw;
//            }
//        }


//        // Helper methods for base constructor arguments
//        private static string TryCreateDescription(AgentId id, AgentsConfiguration configuration)
//        {
//            try
//            {
//                if (configuration == null)
//                    throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null when creating a ChatClient.");

//                if (!configuration.Agents.ContainsKey(id.Type))
//                    throw new ArgumentException($"No agent configuration found for type: {id.Type}. Ensure the configuration contains a valid entry for this agent type.");


//                return $"{id.Type} ({id.Key}) {configuration.Agents[id.Type]?.Description}";
//            }
//            catch (Exception ex)
//            {
//                throw new InvalidOperationException("Failed to create description for ChatAgent.", ex);
//            }
//        }



//        private static IChatClient TryCreateChatClient(AgentsConfiguration configuration, IChatClientFactory chatClientFactory, AgentId id, ILogger logger)
//        {
//            try
//            {
//                if (configuration == null)
//                    throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null when creating a ChatClient.");

//                if (chatClientFactory == null)
//                    throw new ArgumentNullException(nameof(chatClientFactory), "ChatClientFactory cannot be null when creating a ChatClient.");

//                if (!configuration.Agents.ContainsKey(id.Type))
//                    throw new ArgumentException($"No agent configuration found for type: {id.Type}. Ensure the configuration contains a valid entry for this agent type.");

//                var agentConfig = configuration.Agents[id.Type];
//                if (agentConfig.Clients == null || !agentConfig.Clients.Any())
//                    throw new ArgumentException($"Clients configuration is missing for agent type: {id.Type}. Ensure the configuration specifies a valid client.");

//                var primaryClientConfig = agentConfig.Clients.First();
//                if (primaryClientConfig.UseEmbedding) return null;

//                if (agentConfig.Clients.Count == 1)
//                {
//                    return chatClientFactory.CreateChatClient(agentConfig.Clients[0]);
//                }
//                // If multiple clients, create a resilient client
//                return chatClientFactory.CreateChatClient(agentConfig.Clients, agentConfig.Timeouts);
//            }
//            catch (ArgumentException ex)
//            {
//                logger?.LogError(ex, "Failed to create ChatClient for agent type: {AgentType}. Check the configuration and ensure the agent type is correctly defined.", id.Type);
//                throw new InvalidOperationException($"Failed to create ChatClient for agent type: {id.Type}.", ex);
//            }
//            catch (Exception ex)
//            {
//                logger?.LogError(ex, "An unexpected error occurred while creating ChatClient for agent type: {AgentType}.", id.Type);
//                throw new InvalidOperationException($"An unexpected error occurred while creating ChatClient for agent type: {id.Type}. {ex?.Message ?? ex?.InnerException?.Message}.", ex);
//            }
//        }


//        private static IEmbeddingGenerator<string, Embedding<float>> TryCreateEmbeddingGenerator(AgentsConfiguration configuration, IChatClientFactory chatClientFactory, AgentId id, ILogger logger)
//        {
//            try
//            {
//                if (configuration == null)
//                    throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null when creating an EmbeddingGenerator.");

//                if (chatClientFactory == null)
//                    throw new ArgumentNullException(nameof(chatClientFactory), "ChatClientFactory cannot be null when creating an EmbeddingGenerator.");

//                if (!configuration.Agents.ContainsKey(id.Type))
//                    throw new ArgumentException($"No agent configuration found for type: {id.Type}. Ensure the configuration contains a valid entry for this agent type.");

//                var agentConfig = configuration.Agents[id.Type];
//                if (agentConfig.Clients == null || !agentConfig.Clients.Any())
//                    throw new ArgumentException($"Clients configuration is missing for agent type: {id.Type}. Ensure the configuration specifies a valid client.");

//                var primaryClientConfig = agentConfig.Clients.First();
//                if (primaryClientConfig.UseEmbedding)
//                {
//                    // Pass the specific ClientConfig to the factory, not the agent name.
//                    return chatClientFactory.CreateEmbeddingGenerator(primaryClientConfig);
//                }

//                return null;
//            }
//            catch (ArgumentException ex)
//            {
//                logger?.LogError(ex, "Failed to create EmbeddingGenerator for agent type: {AgentType}. Check the configuration and ensure the agent type is correctly defined.", id.Type);
//                throw new InvalidOperationException($"Failed to create EmbeddingGenerator for agent type: {id.Type}.", ex);
//            }
//            catch (Exception ex)
//            {
//                logger?.LogError(ex, "An unexpected error occurred while creating EmbeddingGenerator for agent type: {AgentType}.", id.Type);
//                throw new InvalidOperationException($"An unexpected error occurred while creating EmbeddingGenerator for agent type: {id.Type}.", ex);
//            }
//        }


//        public async ValueTask HandleAsync(WorkflowState state, MessageContext messageContext)
//        {
//            if (messageContext.CancellationToken.IsCancellationRequested)
//            {
//                _logger?.LogInformation("Agent {AgentId} received cancellation request. Terminating workflow for topic {TopicId}.", Id, state.WorkflowTopicId);
//                __topicTerminationService.Cancel(new TopicId(state.WorkflowTopicId));
//                return;
//            }
//            _logger?.Log(LogLevel.Debug, $"\n{Id}:Data input:\n{state.Data.ToJSON()}");
//            //Debug.WriteLine($"\n{Id}:Data input:\n{input.ToJSON()}");



//            //_logger?.LogDebug("Agent {AgentId} received input: {Input}", Id, System.Text.Json.JsonSerializer.Serialize(state.Data));

//            try
//            {
//                // 1. Check for Termination Signal
//                if (state.Data.ContainsKey(TERMINATE))
//                {
//                    _logger?.LogDebug("Agent {AgentId} received termination signal.", Id);

//                    __topicTerminationService.TryComplete(new TopicId(state.WorkflowTopicId), state.Data);
//                    return;
//                }

//                // Apply pre-processing transformation to the input state if configured
//                // This allows modifying the state before it's used in prompt formatting.
//                if (__agentOptions.Transform != null)
//                {
//                    _logger?.LogDebug("Agent {AgentId} applying pre-processing transformation to input state: {Expression}", Id, __agentOptions.Transform.Expression);
//                    state.Data = await _transformerExecutor.TransformAsync(__agentOptions.Transform, state.Data, __sharedMemoryProvider, null, messageContext.CancellationToken);
//                    _logger?.LogDebug("Agent {AgentId} transformed input state: {TransformedMessage}", Id, JsonSerializer.Serialize(state.Data));
//                }

//                var workflowTopicId = state.WorkflowTopicId;


//                // --- 2. Get Effective Prompts using Type ---
//                string effectiveSystemPromptTemplate = await _promptProvider.GetPromptAsync(__agentOptions.System);
//                string effectivePromptTemplate = await _promptProvider.GetPromptAsync(__agentOptions.Prompt);
//                // ---


//                //var formattedPrompt = Smart.Format(__agentOptions.Prompt, input);
//                string formattedPrompt = null;
//                HandlebarsTemplate<Dictionary<string, object>, object> template = null;

//                // 3. Prepare Chat Messages (including System Prompt)
//                List<ChatMessage> chatMessages = new List<ChatMessage>();
//                if (!string.IsNullOrWhiteSpace(effectiveSystemPromptTemplate))
//                {
//                    try
//                    {
//                        template = __handleBars.Compile(effectiveSystemPromptTemplate);
//                        formattedPrompt = template(state.Data);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger?.LogError(ex, "Agent {AgentId} encountered an error while formatting the system prompt.", Id);
//                        // If prompt formatting fails, the agent cannot proceed. Terminate the workflow.
//                        var exceptionToReport = new InvalidOperationException($"Agent {Id} failed to format system prompt. See inner exception for details.", ex);
//                        __topicTerminationService?.TrySetException(new TopicId(state.WorkflowTopicId), exceptionToReport);
//                        return; // Stop execution
//                    }

//                    _logger?.LogDebug("Agent {AgentId} formatted system prompt:\n {FormattedPrompt}", Id, formattedPrompt);

//                    chatMessages.Add(new ChatMessage(ChatRole.System, formattedPrompt));
//                }

//                if (!string.IsNullOrEmpty(effectivePromptTemplate))
//                {

//                    try
//                    {
//                        template = __handleBars.Compile(effectivePromptTemplate);
//                        formattedPrompt = template(state.Data);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger?.LogError(ex, "Agent {AgentId} encountered an error while formatting the prompt.", Id);
//                        // If prompt formatting fails, the agent cannot proceed. Terminate the workflow.
//                        var exceptionToReport = new InvalidOperationException($"Agent {Id} failed to format user prompt. See inner exception for details.", ex);
//                        __topicTerminationService?.TrySetException(new TopicId(state.WorkflowTopicId), exceptionToReport);
//                        return; // Stop execution
//                    }


//                }
//                else
//                {
//                    _logger?.LogError("Agent {Id} is not provided with user prompt template", Id);
//                    // Decide if this is an error or acceptable. If required, throw or return.
//                    throw new Exception($"Agent {Id} is not provided with user prompt template");
//                }

//                _logger?.LogDebug("Agent {AgentId} formatted prompt:\n {FormattedPrompt}", Id, formattedPrompt);


//                chatMessages.Add(new ChatMessage(ChatRole.User, formattedPrompt));


//                // 4. Check if input message doesn't contain tools

//                if (state.Data.ContainsKey("tools") && state.Data["tools"] is List<object> inputTools)
//                {
//                    // Allow overriding tools via input message (optional)
//                    var tools = ConvertToToolOptions(inputTools, _logger);


//                    __chatOptions.Tools ??= FunctionFactory(tools);
//                    _logger?.LogDebug("Agent {AgentId} using tools overridden from input.", Id);
//                }


//                // 5. Execute LLM Call (or Embedding)
//                Dictionary<string, object> responseDictionary = null;
//                string responseText = null;//json or text

//                try
//                {


//                    if (__chatOptions.Tools != null && __chatOptions.Tools.Count > 0)
//                    {


//                        _logger?.LogInformation("Agent {AgentId} calling LLM with tools.", Id);
//                        ChatResponse chatResponse = await CompleteAsync(chatMessages, __chatOptions, messageContext.CancellationToken);
//                        responseText = chatResponse?.Messages[chatResponse.Messages.Count() - 1]?.Text; // Adjust based on actual response structure
//                        //chatResponse.Messages[0].Contents[0]
//                        _logger?.LogDebug(chatResponse?.Messages[0]?.Text?.ToString());

//                        if (!string.IsNullOrEmpty(responseText))
//                        {
//                            responseDictionary = ProcessResponseText(responseText);
//                        }
//                        else
//                        {
//                            _logger?.LogWarning("Agent {AgentId} received null or empty response from tool/function call.", Id);
//                            throw new Exception($"Agent {Id} received null or empty response from tool/function call.");
//                        }
//                    }
//                    else if (__agentOptions.Clients.First().UseEmbedding)
//                    {
//                        _logger?.LogInformation("Agent {AgentId} generating embedding.", Id);
//                        GeneratedEmbeddings<Embedding<float>> embeddingResult = await GenerateAsync(EmbeddingGenerator, new List<string> { formattedPrompt }, messageContext.CancellationToken);
//                        // Structure the result as needed
//                        var outputKey = __agentOptions.Clients.First().EmbeddingKey ?? "embedding";
//                        responseDictionary = new Dictionary<string, object> { { outputKey, embeddingResult.FirstOrDefault()?.Vector ?? Array.Empty<float>() } };
//                        responseText = "{\"embedding_generated\": true}";
//                    }
//                    // Use the primary client to check for streaming.
//                    else if (_isStreamingEnabled)
//                    {
//                        _logger?.LogInformation("Agent {AgentId} calling LLM with streaming...", Id);
//                        var accumulatedResponse = new System.Text.StringBuilder();


//                        if (string.IsNullOrEmpty(workflowTopicId))
//                        {
//                            _logger?.LogWarning("Agent {AgentId} cannot stream response without a '{WorkflowTopicId}' in the input message.", Id, WORKFLOW_TOPIC_ID);
//                        }
//                        else
//                        {
//                            await foreach (var update in CompleteStreamingAsync(chatMessages, __chatOptions, messageContext.CancellationToken))
//                            {
//                                if (update?.Contents is not null)
//                                {


//                                    // Accumulate text for final processing after the loop
//                                    accumulatedResponse.Append(update.Text);

//                                    // If Display is configured, stream the raw chunks directly to the UI.
//                                    //Note: No Template is used here, as the full response is not yet available.
//                                    await ProcessDisplayAsync(update.Contents, workflowTopicId, messageContext.CancellationToken);
//                                }
//                            }
//                        }

//                        responseText = accumulatedResponse.ToString();

//                        _logger?.LogDebug("Agent {AgentId} received LLM streaming response: {ResponseText}", Id, string.IsNullOrWhiteSpace(responseText) ? "EMPTY!!!" : responseText);

//                        responseDictionary = ProcessResponseText(responseText);
//                    }
//                    else // Standard non-streaming chat completion
//                    {
//                        _logger?.LogInformation("Agent {AgentId} calling LLM.", Id);
//                        ChatResponse chatResponse = await CompleteAsync(chatMessages, __chatOptions, messageContext.CancellationToken);

//                        if (chatResponse != null && chatResponse.Messages.Any())
//                        {
//                            //Check if there is TextContent or ErrorContent
//                            if (chatResponse.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Contents.FirstOrDefault(c => c is Microsoft.Extensions.AI.TextContent) is Microsoft.Extensions.AI.TextContent textContent)
//                            {


//                                responseText = textContent.Text; // Handle potential null with empty string default

//                                if (!string.IsNullOrEmpty(responseText))
//                                {
//                                    _logger?.LogDebug("Agent {AgentId} received LLM response: {ResponseText}", Id, responseText);

//                                    //Extract JSON from text
//                                    responseText = JsonExtractor.ExtractJson(responseText);

//                                    _logger?.LogDebug("Agent {AgentId} extracted JSON:\n {ExtractedJson}", Id, responseText);

//                                    if (__agentOptions.ResponseMapping != null && !string.IsNullOrEmpty(__agentOptions.ResponseMapping.Type))
//                                    {
//                                        responseDictionary = MapResponseToObject(responseText);
//                                    }

//                                    if (responseDictionary == null)
//                                    {
//                                        try
//                                        {
//                                            responseDictionary = JsonToDictionaryConverter.DeserializeToDictionary(responseText);
//                                        }
//                                        catch (Exception ex)
//                                        {
//                                            _logger?.LogError(ex, $"Agent {Id} encountered an error while parsing the response{responseText} from tool/function call.");
//                                        }
//                                    }
//                                }
//                            }
//                            else if (chatResponse.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Contents.FirstOrDefault(c => c is ErrorContent) is ErrorContent errorContent)
//                            {


//                                if (__agentOptions.Clients.First().ThrowOnError)
//                                {
//                                    throw new Exception($"Agent {Id} received error from client: {errorContent?.Message} {errorContent?.Details}");
//                                }
//                                else
//                                {
//                                    var streamPayload = new StreamPayload
//                                    {
//                                        WorkflowTopicId = workflowTopicId,
//                                        Contents = new List<AIContent> { errorContent }
//                                    };

//                                    // Fire-and-forget with error logging (converted to Task)
//                                    _ = PublishMessageAsync(streamPayload, new TopicId("ui-stream"), null, messageContext.CancellationToken).AsTask()
//                                        .ContinueWith(t =>
//                                        {
//                                            if (t.IsFaulted) _logger?.LogError(t.Exception, "Failed to publish error content to UI.");
//                                        }, TaskContinuationOptions.OnlyOnFaulted);

//                                }


//                                // Default to true if not specified
//                                //throw new Exception($"Agent {Id} received error from client: {errorContent?.Message} {errorContent?.Details}");

//                                _logger?.LogWarning("Agent {AgentId} received error from client: {ErrorMessage}", Id, errorContent?.Message);
//                                responseDictionary = new Dictionary<string, object> { { "error", errorContent } };

//                            }
//                            else if (chatResponse.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Contents.FirstOrDefault(c => c is TableContent) is TableContent tableContent)
//                            {
//                                // Handle TableContent if needed
//                                responseDictionary = new Dictionary<string, object>
//                                {
//                                    { "table", tableContent.Rows }
//                                };
//                            }
//                            else
//                            {

//                                _logger?.LogWarning("Agent {AgentId} received unknown content type from client.", Id);
//                                throw new Exception($"Agent {Id} received unknown content type from client.");
//                            }

//                        }
//                        else
//                        {
//                            _logger?.LogWarning("Agent {AgentId} received null or empty response from Client.", Id);

//                            throw new Exception($"Agent {Id} received null or empty response from Client.");

//                        }

//                    }
//                }
//                catch (JsonException e)
//                {
//                    _logger?.LogError("Agent {AgentId} streamed response was not valid JSON. ", Id);

//                    throw new Exception($"Agent {{AgentId}} streamed response:\n `{(string.IsNullOrWhiteSpace(responseText) ? "EMPTY!!!" : responseText)}`\n was not valid JSON. Error:{e?.Message ?? e?.InnerException?.Message}");
//                }
//                catch (Exception ex)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} encountered an error during LLM/Embedding execution.Execution:{ex}", Id, ex);

//                    throw new Exception($"Agent {Id} encountered an error during LLM/Embedding execution: {ex}");

//                }


//                if (responseDictionary == null)
//                {
//                    _logger?.LogError("Agent {AgentId} failed to create responseDictionary from {response}", Id, responseText);
//                    //responseDictionary = new Dictionary<string, object> { { "error", "Failed to obtain result from inference." } };
//                    throw new Exception($"Agent {Id} Agent {{AgentId}} failed to create responseDictionary from {responseText}");

//                }

//                _logger?.LogDebug("Agent {AgentId} got result: {Result}", Id, JsonSerializer.Serialize(responseDictionary));


//                await ProcessDisplayAsync(responseDictionary, responseText, state.WorkflowTopicId, messageContext.CancellationToken, _isStreamingEnabled);


//                // Create a base state for this agent's turn by merging the response into the original state.
//                // This base state will be used as the starting point for each rule evaluation.
//                var baseStateForRules = new WorkflowState
//                {
//                    WorkflowTopicId = state.WorkflowTopicId,
//                    Data = new Dictionary<string, object>(state.Data)
//                };

//                // 6. Process Message Routing Rules.
//                // If no routing rules are defined, this agent is considered a terminal node in the workflow.
//                // The workflow concludes, and the agent's result is the final result.
//                if (__agentOptions.Rules is null || !__agentOptions.Rules.Any())
//                {
//                    _logger?.LogInformation("Agent {AgentId} has no routing rules. Terminating workflow.", Id);
//                    __topicTerminationService.TryComplete(new TopicId(state.WorkflowTopicId), responseDictionary);
//                    _logger?.LogInformation($"Agent {Id} finished handling message from {messageContext?.Sender?.Key}.");
//                    return;
//                }

//                // Merge the response dictionary into the base state.
//                // This now overwrites existing keys, which is usually the desired behavior for state updates.
//                foreach (var kvp in responseDictionary)
//                {
//                    baseStateForRules.Data[kvp.Key] = kvp.Value;
//                }


//                //TODO: Maybe save current state for debuging or memory

//                bool atLeastOneRuleMatched = false;

//                foreach (var rule in __agentOptions.Rules)
//                {
//                    bool conditionMet = false;

//                    // If there's no condition, the rule always matches.
//                    if (string.IsNullOrWhiteSpace(rule.Condition))
//                    {
//                        conditionMet = true;
//                    }
//                    else
//                    {
//                        _logger?.LogTrace("Agent {AgentId} evaluating rule with condition: {Condition}", Id, rule.Condition);

//                        // 1. Try to parse as a simple boolean first.
//                        if (bool.TryParse(rule.Condition, out bool simpleBool))
//                        {
//                            conditionMet = simpleBool;
//                            _logger?.LogTrace("Agent {AgentId} evaluated condition as simple boolean: {Result}", Id, conditionMet);
//                        }
//                        else
//                        {
//                            // 2. If not a simple boolean, try to evaluate as a Handlebars template first.
//                            // This is often used for simpler conditions and is less strict than the JSONata parser.
//                            try
//                            {
//                                // The template should resolve to a string that can be parsed as a boolean.
//                                var conditionTemplate = __handleBars.Compile(rule.Condition);
//                                var templateResult = conditionTemplate(baseStateForRules.Data); // Evaluate against data
//                                if (bool.TryParse(templateResult, out bool handlebarsBool))
//                                {
//                                    conditionMet = handlebarsBool;
//                                    _logger?.LogTrace("Agent {AgentId} evaluated condition`{Condition}` as Handlebars template: {Result}", Id, rule.Condition, conditionMet);
//                                }
//                            }
//                            catch (Exception ex)
//                            {
//                                _logger?.LogTrace(ex, "Agent {AgentId} could not evaluate condition as Handlebars. Will try as JSONata expression. Condition: {Condition}", Id, rule.Condition);

//                                // 3. If Handlebars fails, assume it's a JSONata expression.
//                                conditionMet = _expressionEvaluator.Evaluate(rule.Condition, baseStateForRules.Data.ToJSON());
//                                _logger?.LogTrace("Agent {AgentId} evaluated condition`{Condition}` as JSONata expression: {Result}", Id, rule.Condition, conditionMet);
//                            }
//                        }
//                    }

//                    if (!conditionMet)
//                    {
//                        _logger?.LogDebug("Agent {AgentId} condition not met for rule: {Condition}", Id, rule.Condition);
//                        continue; // Skip to the next rule
//                    }

//                    _logger?.LogDebug("Agent {AgentId} condition met for rule: {Condition} will route to:", Id, rule.Condition, string.Join(',',rule.Receivers??=new List<string>()));
                   

//                    // Create a rule-specific state by cloning the base state.
//                    // This ensures that transformations for one rule do not affect subsequent rules in the same loop.
//                    var ruleSpecificState = baseStateForRules;


//                    // Apply transformation if defined
//                    if (rule.Transform != null)
//                    {
//                        _logger?.LogDebug("Agent {AgentId} applying transformation:{transformation} ", Id, rule.Transform.Expression ?? rule.Transform.ServiceType ?? "${rule.Transform.AssemblyQualifiedName}.{rule.Transform.Method}");

//                        // Create an explicit, extensible context for the transformation step.
//                        var transformContext = new StepExecutionContext
//                        {
//                            // Set the new key we just added.
//                            InitiatingAgentId = this.Id
//                        };

//                        // Set the response mapping key if it's configured.
//                        if (__agentOptions.ResponseMapping?.StateKey is string responseMappingKey && !string.IsNullOrEmpty(responseMappingKey))
//                        {
//                            transformContext.ResponseMappingKey = responseMappingKey;
//                        }

//                        // Clone the base state again to ensure isolation between rules.
//                        ruleSpecificState = new WorkflowState
//                        {
//                            WorkflowTopicId = baseStateForRules.WorkflowTopicId,
//                            Data = new Dictionary<string, object>(baseStateForRules.Data)
//                        };

//                        // Pass the state data and the explicit context to the transformer. (Assuming IMessageTransformer is updated)
//                        ruleSpecificState.Data = await _transformerExecutor.TransformAsync(rule.Transform, ruleSpecificState.Data, __sharedMemoryProvider, transformContext, messageContext.CancellationToken);
//                        _logger?.LogDebug("Agent {AgentId} transformed message: {TransformedMessage}", Id, JsonSerializer.Serialize(ruleSpecificState.Data));
//                    }

//                    atLeastOneRuleMatched = true;//even one rule is matched and even the rule is without condition

//                    if (rule.Receivers == null || rule.Receivers.Count == 0)
//                    {
//                        _logger?.LogWarning("Agent {AgentId} - No receivers specified for matched rule. The workflow will terminate after this rule.", Id);

//                        __topicTerminationService.TryComplete(new TopicId(ruleSpecificState.WorkflowTopicId), ruleSpecificState.Data);

//                        return;
//                    }
//                    else
//                    {
//                        // Send message to receivers
//                        // Check if this rule explicitly terminates the workflow.
//                        bool willTerminate = rule.Receivers.Any(r => r.Equals(TERMINATE, StringComparison.OrdinalIgnoreCase));

//                        // Use fire-and-forget for routing, but observe exceptions.
//                        _ = RouteMessageAsync(rule.Receivers, ruleSpecificState, messageContext.CancellationToken)
//                            .ContinueWith(t =>
//                            {
//                                if (t.IsFaulted)
//                                {
//                                    _logger?.LogError(t.Exception, "Agent {AgentId} fire-and-forget message routing failed for rule condition: {Condition}", Id, rule.Condition);
//                                    // If a critical routing fails, you might want to terminate the workflow.
//                                    // __topicTerminationService?.TrySetException(new TopicId(state.WorkflowTopicId), t.Exception);
//                                }
//                            }, TaskContinuationOptions.OnlyOnFaulted);

//                        // If the rule includes TERMINATE, it has the highest priority.
//                        // The agent's work for this message is done, so we exit immediately.
//                        if (willTerminate)
//                        {
//                            _logger?.LogInformation("Agent {AgentId} matched a rule with TERMINATE. Halting further rule processing for this message.", Id);
//                            return; // Exit HandleAsync immediately.
//                        }
//                    }



//                }


//                if (!atLeastOneRuleMatched)
//                {
//                    _logger?.LogWarning($"Agent {Id} completed execution but no routing rule was matched from the defined rules.");

//                    // If no rules matched, we should still terminate the workflow with the current result
//                    // to prevent the workflow from getting stuck.
//                    var finalState = new Dictionary<string, object>(state.Data);
//                    foreach (var kvp in responseDictionary) { finalState[kvp.Key] = kvp.Value; }

//                    __topicTerminationService.TryComplete(new TopicId(state.WorkflowTopicId), finalState);
//                }

//                _logger?.LogInformation($"Agent {Id} finished handling message from {messageContext?.Topic?.Source}.");
//            }
//            catch (Exception e)
//            {

//                _logger?.LogError(e, "Agent {AgentId} error:{err} ", Id, $"{e?.Message} {e?.InnerException}\n{e?.StackTrace}");
//                // Use the specific workflow topic ID from the state to ensure the correct workflow is terminated.
//                __topicTerminationService?.TrySetException(new TopicId(state.WorkflowTopicId), e);
//            }
//        }


//        // Add this method to the ChatAgent class
//        private Options CreateChatOptions(AgentDefinition agentOptions)
//        {
//            Options chatOptions = new Options();

//            // Use options from the primary client configuration. The constructor ensures Clients is not null/empty.
//            var clientOptions = agentOptions.Clients.First().Options;

//            if (clientOptions != null)
//            {


//                if (clientOptions.Temperature.HasValue)
//                {
//                    chatOptions.Temperature = clientOptions.Temperature.Value;
//                }

//                if (clientOptions.MaxOutputTokens.HasValue)
//                {
//                    chatOptions.MaxOutputTokens = clientOptions.MaxOutputTokens.Value;
//                }

//                if (clientOptions.TopP.HasValue)
//                {
//                    chatOptions.TopP = clientOptions.TopP.Value;
//                }

//                if (clientOptions.TopK.HasValue)
//                {
//                    chatOptions.TopK = clientOptions.TopK.Value;
//                }

//                if (clientOptions.FrequencyPenalty.HasValue)
//                {
//                    chatOptions.FrequencyPenalty = clientOptions.FrequencyPenalty.Value;
//                }

//                if (clientOptions.PresencePenalty.HasValue)
//                {
//                    chatOptions.PresencePenalty = clientOptions.PresencePenalty.Value;
//                }

//                if (clientOptions.Seed.HasValue)
//                {
//                    chatOptions.Seed = clientOptions.Seed.Value;
//                }

//                if (clientOptions.ResponseFormat != null)
//                {
//                    if (clientOptions.ResponseFormat.Type == ResponseFormatType.Type)
//                    {
//                        var type = Type.GetType(clientOptions.ResponseFormat.Format);

//                        _logger?.LogDebug("Agent {AgentId} using type as response format: {Type}", Id, type);

//                        JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
//                        System.Text.Json.Nodes.JsonNode jsonNode = jsonSerializerOptions.GetJsonSchemaAsNode(type);

//                        _logger?.LogDebug("Agent {AgentId} generated JSON schema from type: {JsonNode}", Id, jsonNode.ToJsonString());

//                        JsonElement schemaElement = JsonDocument.Parse(jsonNode.ToJsonString()).RootElement;
//                        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement);
//                    }
//                    else if (clientOptions.ResponseFormat.Type == ResponseFormatType.Json)
//                    {
//                        // Assuming the schema is provided as a string in the options
//                        string jsonSchemaString = clientOptions.ResponseFormat.Format;

//                        _logger?.LogDebug("Agent {AgentId} using JSON schema as response format: {JsonSchema}", Id, jsonSchemaString);

//                        JsonDocument schemaDocument = JsonDocument.Parse(jsonSchemaString);
//                        JsonElement schemaElement = schemaDocument.RootElement;

//                        _logger?.LogDebug("Agent {AgentId} parsed JSON schema: {SchemaElement}", Id, schemaElement);

//                        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement);
//                    }
//                }

//                if (clientOptions.StopSequences != null)
//                {
//                    chatOptions.StopSequences = clientOptions.StopSequences;
//                }

//                if (clientOptions.Tools != null)
//                {
//                    chatOptions.Tools = FunctionFactory(clientOptions.Tools);
//                }
//            }

//            return chatOptions;
//        }



//        private async Task RouteMessageAsync(List<string> receivers, WorkflowState state, CancellationToken cancellationToken = default)
//        {


//            foreach (var item in receivers)
//            {
//                if (string.IsNullOrWhiteSpace(item)) continue;

//                if (item.Equals(TERMINATE, StringComparison.OrdinalIgnoreCase))
//                {
//                    _logger?.LogInformation("Agent {AgentId} received TERMINATE receiver. Terminating workflow for topic {TopicId}.", Id, state.WorkflowTopicId);
//                    __topicTerminationService.TryComplete(new TopicId(state.WorkflowTopicId), state.Data);
//                    // If we are terminating, we don't need to process other receivers in this rule.
//                    // We break out of the loop.
//                    break;
//                }


//                _logger?.LogDebug("Agent {AgentId} routing message to: {Receiver}", Id, item);

//                string[] parts = item.Split('/');

//                if (parts.Length == 2) // Format: "AgentType/AgentName"
//                {
//                    // TODO: Need a way to resolve AgentName to AgentId if AgentName is not the unique ID.
//                    // Assuming AgentName here IS the unique name part of AgentId for now.
//                    // The runtime might need a registry: AgentName -> AgentId
//                    //  await this.SendMessageAsync(keyValuePairs, new AgentId("Checker2", "default"), "MySifrovanaPoruka");

//                    //await SendMessageAsync(message, new AgentId(parts[1], parts[0])); // Name, Type
//                    _logger?.LogDebug("Agent {AgentId} routing state to agent: {AgentName}/{parms}", Id, parts[0], parts[1]);
//                    await SendMessageAsync(state, new AgentId(parts[0], parts[1]), null, cancellationToken); // Name, Type
//                }
//                else if (parts.Length == 1) // Format: "TopicName"
//                {
//                    _logger?.LogDebug("Agent {AgentId} routing state to topic: {Topic}", Id, item);
//                    await PublishMessageAsync(state, new TopicId(item), null, cancellationToken);

//                }
//                else
//                {
//                    _logger?.LogWarning("Agent {AgentId} - Invalid receiver format: {Receiver}", Id, item);
//                }
//            }
//        }


//        private IList<AITool> FunctionFactory(List<ToolConfig> tools)
//        {
//            var aiTools = new List<AITool>();
//            if (tools == null) return aiTools;

//            foreach (var item in tools)
//            {
//                if (string.IsNullOrWhiteSpace(item.AssemblyQualifiedName) || string.IsNullOrWhiteSpace(item.Method))
//                {
//                    _logger?.LogWarning("Agent {AgentId} skipping tool with missing AssemblyQualifiedName or FunctionName: {@ToolOptions}", Id, item);
//                    continue;
//                }

//                try
//                {
//                    // 1. Get MethodInfo from cache
//                    MethodInfo toolMethodInfo = __delegateCache.GetOrAddMethodInfo(item.AssemblyQualifiedName, item.Method);

//                    // 2. Handle Instance Methods using IToolInstanceProvider
//                    // if instance exist return if not create paramterless constructor instance
//                    object? target = null;
//                    if (toolMethodInfo != null && !toolMethodInfo.IsStatic)
//                    {
//                        Type declaringType = toolMethodInfo.DeclaringType;
//                        if (declaringType == null)
//                        {
//                            // Should be impossible for non-static methods, but defensive check
//                            _logger?.LogError("Agent {AgentId} could not determine DeclaringType for non-static method '{FunctionName}' in '{TypeName}'. Skipping tool.", Id, item.Method, item.AssemblyQualifiedName);
//                            continue;
//                        }

//                        _logger?.LogDebug("Agent {AgentId} requesting instance for non-static tool method '{FunctionName}' of type '{DeclaringType}'.", Id, item.Method, declaringType.FullName);
//                        try
//                        {
//                            // *** Use the provider to get the instance ***
//                            target = __toolInstanceProvider.GetInstance(declaringType);
//                            // Type throws if it cannot get an instance, so no null check needed here unless provider interface changes
//                            _logger?.LogTrace("Agent {AgentId} obtained instance of type '{DeclaringType}' for tool '{FunctionName}'.", Id, declaringType.FullName, item.Method);
//                        }
//                        catch (Exception ex) // Catch exceptions from the provider
//                        {
//                            _logger?.LogError(ex, "Agent {AgentId} failed to get instance of type '{DeclaringType}' for tool '{FunctionName}' using the configured IToolInstanceProvider. Skipping tool.", Id, declaringType.FullName, item.Method);
//                            continue; // Skip this tool if instance cannot be obtained
//                        }
//                    }
//                    // else: target remains null for static methods

//                    // 3. Prepare Factory Options (if overrides provided)

//                    //if (!string.IsNullOrWhiteSpace(item.DescriptiveFunctionName) || !string.IsNullOrWhiteSpace(item.Description))
//                    //{
//                    //    factoryOptions = new AIFunctionFactoryOptions { /* ... set options ... */ };
//                    //    // ... logging ...
//                    //}

//                    // 4. Create AITool using the factory
//                    AITool aiTool = AIFunctionFactory.Create(toolMethodInfo, target, item.DescriptiveFunctionName, item.Description);

//                    aiTools.Add(aiTool);
//                    _logger?.LogDebug("Agent {AgentId} successfully added tool '{Tool}' (Method: {FunctionName}, Static: {IsStatic}) from type '{TypeName}'.", Id, aiTool.ToDetailString(), item.Method, toolMethodInfo.IsStatic, item.AssemblyQualifiedName);

//                }
//                catch (TypeLoadException ex)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} failed to load type '{TypeName}' for tool '{FunctionName}'. Check AssemblyQualifiedName.", Id, item.AssemblyQualifiedName, item.Method);
//                }
//                catch (MissingMethodException ex)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} failed to find method '{FunctionName}' in type '{TypeName}' for tool. Check method name, parameters (if applicable), and BindingFlags.", Id, item.Method, item.AssemblyQualifiedName);
//                }
//                catch (Exception ex) // Catch other potential errors (e.g., from AIFunctionFactory.Create)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} failed to create or add tool '{FunctionName}' from type '{TypeName}' using MethodInfo.", Id, item.Method, item.AssemblyQualifiedName);
//                }
//            }
//            return aiTools;
//        }

//        private Dictionary<string, object> MapResponseToObject(string responseText)
//        {
//            try
//            {
//                var type = Type.GetType(__agentOptions.ResponseMapping.Type);
//                if (type == null)
//                {
//                    _logger?.LogError("Agent {AgentId} could not find type for ResponseMapping: {ResponseMapping}", Id, __agentOptions.ResponseMapping.Type);

//                    throw new InvalidOperationException($"Agent {Id} could not find type for ResponseMapping: {__agentOptions.ResponseMapping.Type}");
//                }

//                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//                var deserializedObject = JsonSerializer.Deserialize(responseText, type, options);

//                var key = __agentOptions.ResponseMapping.StateKey;
//                if (string.IsNullOrWhiteSpace(key))
//                {
//                    var typeName = type.Name;
//                    if (!string.IsNullOrEmpty(typeName) && char.IsUpper(typeName[0]))
//                    {
//                        key = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
//                    }
//                    else
//                    {
//                        key = typeName ?? "mappedObject";
//                    }
//                    _logger?.LogDebug("Agent {AgentId} ResponseMapping.StateKey not provided, using default key: {Key}", Id, key);
//                }

//                return new Dictionary<string, object>
//                {
//                    { key, deserializedObject }
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "Agent {AgentId} failed to deserialize response to type {ResponseMapping}. Response text: {ResponseText}", Id, __agentOptions.ResponseMapping.Type, responseText);
//                throw; // Rethrow to be handled by caller
//            }
//        }

//        private Dictionary<string, object> ProcessResponseText(string responseText)
//        {
//            if (string.IsNullOrWhiteSpace(responseText)) return null;
//            _logger?.LogDebug("Agent {AgentId} received LLM response: {ResponseText}", Id, responseText);
//            //Extract JSON from text
//            responseText = JsonExtractor.ExtractJson(responseText);
//            _logger?.LogDebug("Agent {AgentId} extracted JSON:\n {ExtractedJson}", Id, responseText);

//            Dictionary<string, object> responseDictionary = null;
//            if (__agentOptions.ResponseMapping != null && !string.IsNullOrEmpty(__agentOptions.ResponseMapping.Type))
//            {
//                try
//                {
//                    responseDictionary = MapResponseToObject(responseText);
//                }
//                catch (Exception ex)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} failed to map response to object: {ResponseText}", Id, responseText);
//                }
//            }

//            if (responseDictionary == null)
//            {
//                try
//                {
//                    responseDictionary = JsonToDictionaryConverter.DeserializeToDictionary(responseText);
//                }
//                catch (Exception ex)
//                {
//                    _logger?.LogError(ex, "Agent {AgentId} encountered an error while parsing the response from LLM: {ResponseText}", Id, responseText);
//                }
//            }
//            return responseDictionary;
//        }


//        private static List<ToolConfig> ConvertToToolOptions(List<object> inputTools, ILogger logger)
//        {
//            // Existing implementation seems okay, add null checks
//            List<ToolConfig> toolOptions = new List<ToolConfig>();
//            if (inputTools == null) return toolOptions;

//            foreach (var item in inputTools)
//            {
//                if (item is Dictionary<string, object> toolDict)
//                {
//                    if (toolDict.TryGetValue("Type", out var typeObj) && typeObj is string typeStr &&
//                        toolDict.TryGetValue("FunctionName", out var funcObj) && funcObj is string funcStr)
//                    {
//                        toolOptions.Add(new ToolConfig
//                        {
//                            AssemblyQualifiedName = typeStr,
//                            Method = funcStr
//                        });
//                    }
//                    else
//                    {
//                        logger?.LogWarning("Invalid tool format found in input override: {ToolDict}", JsonSerializer.Serialize(toolDict));
//                    }
//                }
//            }
//            return toolOptions;
//        }

//        /// <summary>
//        /// Overload for handling the final, complete response dictionary.
//        /// </summary>
//        private async Task ProcessDisplayAsync(Dictionary<string, object> responseDictionary, string rawResponseText, string workflowTopicId, CancellationToken cancellationToken, bool textAlreadyStreamed = false)
//        {
//            if (__agentOptions.Display == null) return;

//            var contents = new List<AIContent>();

//            // 1. Process Text Content
//            if (!textAlreadyStreamed)
//            {
//                var textOptions = __agentOptions.Display.Text;
//                if (textOptions?.Enabled ?? true)
//                {
//                    string displayText = null;
//                    if (!string.IsNullOrWhiteSpace(textOptions?.Template))
//                    {
//                        try
//                        {
//                            var template = __handleBars.Compile(textOptions.Template);
//                            displayText = template(responseDictionary);
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger?.LogError(ex, "Failed to compile or execute Display.Text.Template for agent {AgentId}.", Id);
//                            displayText = $"Error formatting display: {ex.Message}";
//                        }
//                    }
//                    else
//                    {
//                        // Fallback to raw response text if no template
//                        displayText = rawResponseText;
//                    }

//                    if (!string.IsNullOrEmpty(displayText))
//                    {
//                        contents.Add(new TextContent(displayText));
//                    }
//                }
//            }
//            else if (__agentOptions.Display.Text?.Template != null)
//            {
//                _logger?.LogTrace("Text was streamed, but a final template exists. The UI will show the raw streamed text. To show the templated version, a UI update is needed to handle content replacement.");
//            }

//            // 2. Process Reasoning Content
//            var reasoningOptions = __agentOptions.Display.Reasoning;
//            if ((reasoningOptions?.Enabled ?? true) && responseDictionary != null && responseDictionary.TryGetValue("reasoning", out var reasoningObj) && reasoningObj is string reasoning)
//            {
//                string displayReasoning = reasoning.Trim();
//                if (!string.IsNullOrWhiteSpace(reasoningOptions.Template))
//                {
//                    // Allow template to format reasoning, e.g., add a header
//                    displayReasoning = __handleBars.Compile(reasoningOptions.Template)(responseDictionary);
//                }

//                if (!string.IsNullOrWhiteSpace(displayReasoning))
//                {
//                    contents.Add(new TextReasoningContent(displayReasoning));
//                }
//            }

//            // 3. Process Table Content
//            var tableOptions = __agentOptions.Display.Table;
//            if ((tableOptions?.Enabled ?? true) && responseDictionary != null && responseDictionary.TryGetValue("table", out var tableObj) && tableObj is IReadOnlyList<IReadOnlyDictionary<string, object?>> tableData)
//            {
//                // Note: Tables don't use a template, they are sent as structured data.
//                contents.Add(new TableContent(tableData));
//            }

//            // Send all collected contents in a single payload
//            if (contents.Any())
//            {
//                await ProcessDisplayAsync(contents, workflowTopicId, cancellationToken);
//            }
//        }

//        /// <summary>
//        /// Base method to publish a list of AIContent to the UI stream.
//        /// </summary>
//        private async Task ProcessDisplayAsync(IEnumerable<AIContent> contents, string workflowTopicId, CancellationToken cancellationToken)
//        {
//            if (__agentOptions.Display == null || !contents.Any()) return;

//            //what if DisplayTextOptions.Enabled is false
//            if(__agentOptions.Display.Text != null && (__agentOptions.Display.Text.Enabled == false))
//            {
//                _logger?.LogTrace("Agent {AgentId} Display is configured but all content types are disabled. No content will be sent to UI.", Id);
//                return;
//            }


//            var streamPayload = new StreamPayload { WorkflowTopicId = workflowTopicId, Contents = contents.ToList() };

//            // Fire-and-forget with error logging
//            _ = PublishMessageAsync(streamPayload, new TopicId("ui-stream"), null, cancellationToken).AsTask()
//                .ContinueWith(t => { if (t.IsFaulted) _logger?.LogError(t.Exception, "Failed to publish display message to UI."); }, TaskContinuationOptions.OnlyOnFaulted);
//        }
//    }
//}
