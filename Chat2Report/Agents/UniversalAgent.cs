﻿using Chat2Report.Agents.Evaluation;
using Chat2Report.Agents.Transformers;
using Chat2Report.Agents.WorkflowSteps;
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Models.Workflow;
using Chat2Report.Providers;
using Chat2Report.Services;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents
{
    public class UniversalAgent : BaseAgent, IHandle<WorkflowState>
    {
        private readonly AgentDefinition _agentDefinition;
        private readonly IWorkflowStepFactory _stepFactory;
        private readonly IExpressionEvaluator _expressionEvaluator;
        private readonly IMessageTransformer _transformerExecutor;
        private readonly ITopicTerminationService _topicTerminationService;
        private readonly ILogger<UniversalAgent> _logger;
        private readonly IWorkflowHistoryStore? _historyStore; // Опционален сервис за историја

        // Оптимизација: Компајлирани регуларни изрази за брза проверка на литерали.
        // Се иницијализираат само еднаш.
        private static readonly Regex _literalStringRegex = new Regex(@"^'(?<value>.*)'$", RegexOptions.Compiled);
        private static readonly Regex _literalNumberRegex = new Regex(@"^(?<value>-?\d+(\.\d+)?)$", RegexOptions.Compiled);
        private static readonly Regex _literalBooleanRegex = new Regex(@"^(true|false)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _literalNullRegex = new Regex(@"^null$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
       
        // Регуларен израз за едноставни патеки како 'user_query' или 'validation_result.is_valid'
        private static readonly Regex _simplePathRegex = new Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
        private const string TERMINATE = "Terminate";

        public UniversalAgent(
        AgentId id,
        IAgentRuntime runtime,
        IOptions<AgentsConfiguration> agentsConfig,
        IWorkflowStepFactory stepFactory,
        IExpressionEvaluator expressionEvaluator,
        IMessageTransformer transformerExecutor,
        ITopicTerminationService topicTerminationService,
        ILogger<UniversalAgent> logger,
        IWorkflowHistoryStore? historyStore = null) // Го примаме сервисот тука, опционално.
        // Го користиме помошниот метод за да ја вчитаме дефиницијата
        : base(id, runtime, ResolveAgentDefinitionOnce(id, agentsConfig.Value).Description, logger)
        {
            // Го поставуваме readonly полето за дефиницијата
            _agentDefinition = ResolveAgentDefinitionOnce(id, agentsConfig.Value).Definition;

            // Доделување на сите зависности
            _stepFactory = stepFactory;
            _expressionEvaluator = expressionEvaluator;
            _transformerExecutor = transformerExecutor;
            _topicTerminationService = topicTerminationService;
            _historyStore = historyStore;
            _logger = logger;
        }

        public async ValueTask HandleAsync(WorkflowState receivedState, MessageContext messageContext)
        {
            _logger.LogInformation("Agent '{AgentId}' starting execution pipeline for topic '{TopicId}'.", Id, receivedState.WorkflowTopicId);

            if (receivedState.Data.ContainsKey(TERMINATE))
            {
                _topicTerminationService.TryComplete(new TopicId("workflow-completion", receivedState.WorkflowTopicId), receivedState.Data);
                return;
            }

            // Експлицитно зачувај го WorkflowTopicId во состојбата за да биде достапен за сите чекори и рути.
            if (!receivedState.Data.ContainsKey("workflow_topic_id"))
            {
                receivedState.Data["workflow_topic_id"] = receivedState.WorkflowTopicId;
            }

            // 1. Иницијализирај ја историјата со почетната состојба
            var history = new AgentStepHistory(receivedState.Data);

            var context = new StepExecutionContext { WorkflowTopicId = receivedState.WorkflowTopicId, InitiatingAgentId = this.Id };

            try
            {
                if (_agentDefinition.Steps != null)
                {
                    foreach (var stepDefinition in _agentDefinition.Steps)
                    {
                        var currentSnapshot = history.GetCurrentSnapshot();

                        // 2. Креирај "рамен" поглед на тековната состојба за влезовите
                        var currentStateView = currentSnapshot.State.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Value,
                            StringComparer.OrdinalIgnoreCase);

                        var step = _stepFactory.CreateStep(stepDefinition);

                        _logger.LogDebug("Executing step '{StepType}' (Turn: {Turn}): {Description}",
                            stepDefinition.Type, currentSnapshot.Turn + 1, stepDefinition.Description);

                        var (typedInputs, _) = await PrepareStepInputs(step, stepDefinition, currentStateView, messageContext.CancellationToken);
                        var untypedOutput = await ExecuteStepAsync(step, typedInputs, context, messageContext.CancellationToken).ConfigureAwait(false);

                        // 3. Обработи ги излезите за да добиеш нови StateEntry објекти
                        var newEntries = ProcessStepOutputs(untypedOutput, stepDefinition, currentSnapshot.Turn + 1);

                        // 4. Креирај нов snapshot во историјата. Овој метод сега го прави и чистењето на StepScope.
                        history.CreateNextSnapshot(stepDefinition.Type, newEntries);
                    }
                }

                // 5. Проследи ја целата историја кон процесот на рутирање
                await ProcessRoutesAsync(history, receivedState.WorkflowTopicId, context, messageContext).ConfigureAwait(false);

                _logger.LogInformation("Agent '{AgentId}' finished execution pipeline.", Id);
            }
            catch (Exception ex)
            {
                var snapshot = history.GetCurrentSnapshot(); // За да добиеме последниот snapshot за дијагностика ако е потребно

                _logger.LogError(ex, "Agent '{AgentId}' failed during pipeline execution.\n{snapshot} \n Terminating workflow.", Id, JsonSerializer.Serialize(snapshot));
             
                _topicTerminationService?.TrySetException(new TopicId("workflow-completion", receivedState.WorkflowTopicId), ex);
            }
        }



        private async Task ProcessRoutesAsync(AgentStepHistory history, string workflowTopicId, IStepExecutionContext context, MessageContext messageContext)
        {
            var finalSnapshot = history.GetCurrentSnapshot();
            var fullStateForEval = finalSnapshot.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value, StringComparer.OrdinalIgnoreCase);

            if (_agentDefinition.Routes is null || !_agentDefinition.Routes.Any())
            {
                var finalData = FilterForWorkflowScope(finalSnapshot.State);
                _logger.LogInformation("Agent '{AgentId}' has no routes. Terminating workflow '{TopicId}'.", Id, workflowTopicId);
                _topicTerminationService.TryComplete(new TopicId("workflow-completion", workflowTopicId), finalData);
                return;
            }

            bool routeMatched = false;
            foreach (var route in _agentDefinition.Routes)
            {
                if (await IsConditionMet(route.Condition, fullStateForEval, messageContext.CancellationToken))
                {
                    routeMatched = true;
                    _logger.LogDebug("Agent '{AgentId}' matched route with condition: '{Condition}'", Id, route.Condition ?? "Always true");

                    var dataForRule = new Dictionary<string, object>(fullStateForEval, StringComparer.OrdinalIgnoreCase);

                    if (route.Transform != null)
                        dataForRule = await _transformerExecutor.TransformAsync(route.Transform, dataForRule, context, messageContext.CancellationToken).ConfigureAwait(false);

                    if (route.CleanupPolicy != null)
                        dataForRule = await ApplyCleanupPolicy(route.CleanupPolicy, dataForRule, messageContext.CancellationToken).ConfigureAwait(false);

                    bool isTerminate = route.Receivers == null || !route.Receivers.Any() || route.Receivers.Any(r => r.Equals(TERMINATE, StringComparison.OrdinalIgnoreCase));

                    // Филтрирај за да останат само Workflow податоците ПРЕД терминирање или рутирање
                    var finalData = FilterForWorkflowScope(dataForRule);

                    if (isTerminate)
                    {
                        _logger.LogInformation("Route matched TERMINATE for workflow '{TopicId}'.", workflowTopicId);
                        _topicTerminationService.TryComplete(new TopicId("workflow-completion", workflowTopicId), finalData);
                        return;
                    }

                    var stateToSend = new WorkflowState { WorkflowTopicId = workflowTopicId, Data = finalData };

                    // 
                    if (_historyStore != null && route.Receivers != null)
                    {
                        foreach (var receiverId in route.Receivers)
                        {
                            if (receiverId.Equals(TERMINATE, StringComparison.OrdinalIgnoreCase)) continue;

                            var snapshot = new WorkflowStateSnapshot
                            {
                                WorkflowTopicId = workflowTopicId,
                                TargetAgentId = receiverId,
                                Sequence = history.Snapshots.Count, // Користиме број на snapshot-ови за секвенца
                                Timestamp = DateTime.UtcNow,
                                StateData = finalData
                            };
                            await _historyStore.SaveStateSnapshotAsync(snapshot);
                        }
                    }


                    // Сега, откако состојбата е сигурно зачувана, ја испраќаме пораката.
                    _ = RouteMessageAsync(route.Receivers, stateToSend, messageContext.CancellationToken);

                    break;
                }
            }

            if (!routeMatched)
            {
                _logger.LogWarning("Agent '{AgentId}' did not match any route. Terminating workflow '{TopicId}' with final state.", Id, workflowTopicId);
                var finalData = FilterForWorkflowScope(finalSnapshot.State);
                _topicTerminationService.TryComplete(new TopicId("workflow-completion", workflowTopicId), finalData);
            }
        }

        #region Helper Methods


        private async Task RouteMessageAsync(List<string> receivers, WorkflowState state, CancellationToken cancellationToken)
        {
            foreach (var receiver in receivers.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                _logger.LogDebug("Agent '{AgentId}' routing message to: '{Receiver}'", Id, receiver);
                string[] parts = receiver.Split('/');
                if (parts.Length == 2) // "AgentType/key (uniqueId of particular instance of agent with AgentType can be also topic.Source to idenitify instances listening on same topic) default is 'default'"
                {
                    await SendMessageAsync(state, new AgentId(parts[0], parts[1]), null, cancellationToken);
                }
                else if (parts.Length == 1) // "TopicName" 
                {
                    //TopicName/source(uniqueId of particular topic with TopicName) default is 'default'
                    /*
                     * Examples: Internet-wide unique URI with a DNS authority.
                     *   https://github.com/cloudevents
                      *  mailto:cncf-wg-serverless@lists.cncf.io
                       * Universally-unique URN with a UUID:
                        *urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66
                        *
                        *
                        *
                    /// <summary>
                    /// This subscription matches on topics based on the exact type and maps to agents using the source of the topic as the agent key.
                    /// This subscription causes each source to have its own agent instance.
                    /// </summary>
                    /// <remarks>
                    /// Example:
                    /// <code>
                    /// var subscription = new TypeSubscription("t1", "a1");
                    /// </code>
                    /// In this case:
                    /// - A <see cref="TopicId"/> with type `"t1"` and source `"s1"` will be handled by an agent of type `"a1"` with key `"s1"`.
                    /// - A <see cref="TopicId"/> with type `"t1"` and source `"s2"` will be handled by an agent of type `"a1"` with key `"s2"`.
                    /// </remarks>
                    */
                    await PublishMessageAsync(state, new TopicId(receiver, state.WorkflowTopicId), null, cancellationToken); // state.WorkflowTopicId е клучен за рутирањето
                }
            }
        }


        // === ПРОМЕНА: ProcessStepOutputs сега враќа нови StateEntry објекти ===
        private Dictionary<string, StateEntry> ProcessStepOutputs(object untypedOutput, WorkflowStepDefinition stepDef, int currentTurn)
        {
            var newEntries = new Dictionary<string, StateEntry>(StringComparer.OrdinalIgnoreCase);
            var outputType = untypedOutput.GetType();
            foreach (var mapping in stepDef.Outputs)
            {
                var outputDef = mapping.Value;


                // Постави стандардни вредности ако недостасуваат
                var effectiveKey = string.IsNullOrEmpty(outputDef.Key) ? "*" : outputDef.Key;
                var effectiveScope = outputDef.Scope == StateScope.Unspecified ? StateScope.Workflow : outputDef.Scope;

                
                    // Постоечка логика за мапирање на единечни својства
                    var propInfo = outputType.GetProperty(ToPascalCase(mapping.Key));
                    if (propInfo == null) continue;

                    var value = propInfo.GetValue(untypedOutput);

                    if(effectiveKey=="*")
                {
                    // WILDCARD - Копирај ја целата состојба
                    if (value is IReadOnlyDictionary<string, object> dictValue)
                    {
                        foreach (var kvp in dictValue)
                        {
                            newEntries[kvp.Key] = new StateEntry
                            {
                                Value = kvp.Value,
                                Scope = effectiveScope,
                                TurnCreated = currentTurn,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                    }
                }
                else

                    newEntries[effectiveKey] = new StateEntry
                    {
                        Value = value,
                        Scope = effectiveScope,
                        TurnCreated = currentTurn,
                        Timestamp = DateTime.UtcNow
                    };
                
            }

            return newEntries;
        }

        /// <summary>
        /// Филтрира ја состојбата за да ги вклучи само клучевите со Workflow scope или оние кои не се експлицитно означени како Step/Agent scope.
        /// </summary>
        /// <param name="masterState"></param>
        /// <returns></returns>
        private Dictionary<string, object> FilterForWorkflowScope(IReadOnlyDictionary<string, StateEntry> masterState)
        {
            return masterState
                .Where(kvp => kvp.Value.Scope == StateScope.Workflow || kvp.Value.Scope == StateScope.Unspecified)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value, StringComparer.OrdinalIgnoreCase)!;
        }
        // Оверлоад за ракување со "рамен" речник по трансформации
        private Dictionary<string, object> FilterForWorkflowScope(IReadOnlyDictionary<string, object> flatState)
        {
            // 1. Најди ги сите клучеви што овој агент ги произведува како 'Workflow' scope
            var workflowOutputKeys = _agentDefinition.Steps?
                .SelectMany(s => s.Outputs.Values)
                .Where(o => o.Scope == StateScope.Workflow || o.Scope == StateScope.Unspecified) // Default is Workflow
                .Select(o => o.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            return flatState
                .Where(kvp =>
                {
                    // Никогаш не праќај системски полиња
                    if (kvp.Key.StartsWith("_")) return false;

                    // Ако клучот е експлицитно дефиниран како Workflow output -> ЗАЧУВАЈ
                    if (workflowOutputKeys.Contains(kvp.Key)) return true;

                    // Провери дали клучот НЕ Е дефиниран како Step или Agent scope.
                    var isExplicitlyTemp = _agentDefinition.Steps?
                        .SelectMany(s => s.Outputs.Values)
                        .Any(o => o?.Key != null && o.Key.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) && (o.Scope == StateScope.Step || o.Scope == StateScope.Agent)) ?? false;

                    return !isExplicitlyTemp;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }



        private async Task<(object typedInputs, Type inputType)> PrepareStepInputs(IBaseWorkflowStep step, WorkflowStepDefinition stepDef, IReadOnlyDictionary<string, object> currentState, CancellationToken cancellationToken)
        {
            var typedInterface = step.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<,>));
            if (typedInterface == null) throw new InvalidOperationException($"Step '{step.GetType().Name}' must implement IWorkflowStep<TInput, TOutput>.");

            var inputType = typedInterface.GetGenericArguments()[0];
            var typedInputs = Activator.CreateInstance(inputType)!;

            foreach (var mapping in stepDef.Inputs)
            {
                var inputKey = mapping.Key;   // e.g., "templateData.library" or "analysis"
                var expression = mapping.Value; // e.g., "supported_charts[...]" or "user_query_analysis"

                // Resolve the value based on the expression, regardless of the inputKey structure.
                var (resolvedValue, valueFound) = await ResolveValue(expression, currentState, cancellationToken);

                if (!valueFound)
                {
                    _logger.LogWarning("Input mapping for '{InputKey}' failed: Could not resolve expression '{Expression}'.", inputKey, expression);
                    continue;
                }

                // Now, figure out where to put the resolved value.
                var parts = inputKey.Split(new[] { '.' }, 2);
                var targetPropName = ToPascalCase(parts[0]);
                var propInfo = inputType.GetProperty(targetPropName);

                if (propInfo == null)
                {
                    _logger.LogWarning("Input mapping for '{InputKey}' skipped: Target property '{TargetPropName}' not found on type '{InputType}'.", inputKey, targetPropName, inputType.Name);
                    continue;
                }

                if (parts.Length == 1) // Direct assignment to a property (e.g., "analysis")
                {
                    propInfo.SetValue(typedInputs, ConvertValue(resolvedValue, propInfo.PropertyType));
                }
                else // parts.Length == 2
                {
                    var subKey = parts[1];
                    if (propInfo.GetValue(typedInputs) is not Dictionary<string, object> dict)
                    {
                        dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        propInfo.SetValue(typedInputs, dict);
                    }
                    dict[subKey] = resolvedValue;
                }
            }
            return (typedInputs, inputType);
        }

        private async Task<(object? value, bool found)> ResolveValue(string expression, IReadOnlyDictionary<string, object> currentState, CancellationToken cancellationToken)
        {
            // Case 1: Wildcard - return the whole state
            if (expression == "*") // Example: "*"
            {
                _logger.LogDebug("ResolveValue: '{Expression}' matched wildcard. Returning full state.", expression);
                return (new Dictionary<string, object>(currentState, StringComparer.OrdinalIgnoreCase), true);
            }

            // Case 2: Literal value (string, number, bool, null)
            // Examples: "'some string'", "123", "true", "null"
            if (TryGetLiteralValue(expression, out var literalValue))
            {
                _logger.LogDebug("ResolveValue: '{Expression}' matched literal. Value: {Value}", expression, literalValue);
                return (literalValue, true);
            }

           

            // Case 4: Simple path access (e.g., "my_key" or "my_object.my_property")
            if (_simplePathRegex.IsMatch(expression))
            {
                if (currentState.TryGetValueByPath(expression, out var pathValue))
                {
                    _logger.LogDebug("ResolveValue: '{Expression}' matched simple path. Found in state.", expression);
                    return (pathValue, true);
                }
                else
                {
                    // Патеката е со валидна синтакса, но не постои во состојбата.
                   
                    // Ова е чест случај за опционални влезови при првото извршување.
                    // Враќаме (null, true) за да означиме дека сме го "обработиле" и да го спречиме скапиот JSONata fallback.
                    _logger.LogDebug("ResolveValue: '{Expression}' matched simple path. NOT found in state. Returning null.", expression);
                    return (null, true); // Пронајдено, но вредноста е null
                }
            }

            // Case 5: Fallback to complex expression evaluation (JSONata) for anything else
            _logger.LogDebug("ResolveValue: '{Expression}' is a complex expression. Falling back to JSONata evaluation.", expression);
            try
            {
                var evaluatedValue =await _expressionEvaluator.Evaluate(expression, currentState, cancellationToken);
                // Evaluate can return null for valid expressions that result in no match, which is a valid "found" state.
                _logger.LogDebug("ResolveValue: '{Expression}' evaluated by JSONata. Result: {Value}", expression, evaluatedValue);
                return (evaluatedValue, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ResolveValue: Failed to evaluate expression '{Expression}' as JSONata. Returning (null, false).", expression);
                return (null, false);
            }
        }

        /// <summary>
        /// Обид за парсирање на изразот како литерална вредност (број, стринг, boolean, или null).
        /// </summary>
        private bool TryGetLiteralValue(string expression, out object? value)
        {
            var match = _literalStringRegex.Match(expression);
            if (match.Success)
            {
                value = match.Groups["value"].Value;
                return true;
            }

            match = _literalNumberRegex.Match(expression);
            if (match.Success)
            {
                // Пробува да парсира како decimal за најголема прецизност, па паѓа на double.
                if (decimal.TryParse(match.Groups["value"].Value, out var decValue))
                {
                    value = decValue;
                    return true;
                }
            }

            if (_literalBooleanRegex.IsMatch(expression)) { value = bool.Parse(expression); return true; }
            if (_literalNullRegex.IsMatch(expression)) { value = null; return true; }

            value = null;
            return false;
        }


        private async Task<Dictionary<string, object>> ApplyCleanupPolicy(CleanupPolicy? policy, Dictionary<string, object> data, CancellationToken cancellationToken)
        {
            if (policy == null) return data;

            var keysToRemove = new HashSet<string>(policy.RemoveKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            if (policy.RemoveKeysWhen != null && policy.RemoveKeysWhen.Any())
            {
                var jsonData = data.ToJSON(); 
                foreach (var kvp in policy.RemoveKeysWhen) // kvp.Value is the expression
                    if (await _expressionEvaluator.EvaluateAsBoolean(kvp.Value, jsonData, cancellationToken)) keysToRemove.Add(kvp.Key);
            }

            if (!keysToRemove.Any()) return data;

            _logger.LogDebug("Applying cleanup policy. Keys to remove: {Keys}", string.Join(", ", keysToRemove));
            return data.Where(kvp => !keysToRemove.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<bool> IsConditionMet(string? condition, IReadOnlyDictionary<string, object> data, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            if (bool.TryParse(condition, out bool simpleBool)) return simpleBool;
            try
            {
                var jsonData = new Dictionary<string, object>(data).ToJSON();
                return await _expressionEvaluator.EvaluateAsBoolean(condition, jsonData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate routing condition '{Condition}'. Assuming false.", condition);
                return false;
            }
        }

        #endregion Helper Methods

        #region Static Methods


        private static async Task<object> ExecuteStepAsync(IBaseWorkflowStep step, object typedInputs, IStepExecutionContext? context, CancellationToken ct)
        {
            var method = step.GetType().GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Instance, null,
                                        new[] { typedInputs.GetType(), typeof(IStepExecutionContext), typeof(CancellationToken) }, null);

            if (method == null)
                throw new InvalidOperationException($"Could not find 'ExecuteAsync' on '{step.GetType().Name}' for input '{typedInputs.GetType().Name}'.");

            var task = (Task)method.Invoke(step, new object?[] { typedInputs, context, ct })!;
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");
            if (resultProperty == null)
                throw new InvalidOperationException("ExecuteAsync returned a non-generic Task.");

            return resultProperty.GetValue(task)!;
        }

        private static (string Description, AgentDefinition Definition) ResolveAgentDefinitionOnce(AgentId id, AgentsConfiguration config)
        {
            if (config?.Workflows == null)
                throw new ArgumentException("'Workflows' section not found in configuration.");

            var parts = id.Type.Split(new[] { '_' }, 2);
            if (parts.Length != 2)
                throw new ArgumentException($"AgentId.Type '{id.Type}' is not in the 'WorkflowName_AgentName' format.");

            var workflowName = parts[0];
            var agentNameInWorkflow = parts[1];

            if (!config.Workflows.TryGetValue(workflowName, out var workflowDef))
                throw new ArgumentException($"Workflow '{workflowName}' not found in configuration.");

            if (workflowDef.Agents == null || !workflowDef.Agents.TryGetValue(agentNameInWorkflow, out var agentDef))
                throw new ArgumentException($"Agent '{agentNameInWorkflow}' not found in workflow '{workflowName}'.");

            return (agentDef.Description ?? $"No description for {id.Type}", agentDef);
        }

        private static string ToPascalCase(string input) => string.Concat(Regex.Split(input, @"[_\s\-]+").Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p.Substring(1) : ""));

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null || targetType.IsInstanceOfType(value)) return value;
            if (value is JsonElement json) return json.Deserialize(targetType, _jsonSerializerOptions);

            try { return Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType); }
            catch (Exception ex) when (ex is InvalidCastException or FormatException)
            {
                try
                {
                    var tempJson = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize(tempJson, targetType, _jsonSerializerOptions);
                }
                catch (Exception innerEx)
                {
                    throw new InvalidCastException($"Failed to convert '{value.GetType().FullName}' to '{targetType.FullName}'.", innerEx);
                }
            }
        }

        #endregion Static Methods
    }
}