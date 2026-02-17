using Chat2Report.Utilities;
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;

namespace Chat2Report.Agents.Evaluation
{
    public class JsonataEvaluator : IExpressionEvaluator
    {
        private readonly ISerializationHelper _serializationHelper;
        private readonly ILogger<JsonataEvaluator> _logger;

        public JsonataEvaluator(ISerializationHelper serializationHelper,ILogger<JsonataEvaluator> logger=null)
        {
            _serializationHelper = serializationHelper ?? throw new ArgumentNullException(nameof(serializationHelper));
            _logger = logger ??= LoggerFactory.Create(LoggerFactory => LoggerFactory.AddConsole()).CreateLogger<JsonataEvaluator>();
        }

        /// <summary>
        /// Example usage:
        /// <code>
        /// string expression = "$.message.name = 'John'";
        /// string json = "{\"message\": {\"name\": \"John\"}, \"sharedMemory\": {}}";
        /// bool result = evaluator.EvaluateAsBoolean(expression, json);
        /// // result will be true if the expression matches the JSON data
        /// </code>
        /// </summary>
        public async Task<bool> EvaluateAsBoolean(string expression, string json, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Evaluating Jsonata expression for boolean: {Expression}", expression);
            _logger.LogTrace("Jsonata input JSON: {Json}", json);
            try
            {
                var query = new JsonataQuery(expression);
                // Eval on a string input is efficient for simple boolean/value results
                var result = query.Eval(json);
                var boolResult = Convert.ToBoolean(result);
                _logger.LogDebug("{Expression} => {Result}", expression, boolResult);
                return boolResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate Jsonata boolean expression '{Expression}'.", expression);
                throw;
            }
        }

        public async Task<string> EvaluateAsString(string expression, string json, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Evaluating Jsonata expression for string transformation: {Expression}", expression);
            _logger.LogTrace("Jsonata input JSON: {Json}", json);

            try
            {
                var query = new JsonataQuery(expression);
                // Eval(string) is fine here, as it returns the transformed JSON string directly.
                return query.Eval(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate Jsonata string transformation '{Expression}'.", expression);
                throw;
            }
        }

        /// <summary>
        /// Evaluates a JSONata expression against a context dictionary.
        /// This implementation incorporates performance improvements by pre-filtering the context
        /// to include only the data explicitly referenced by variables in the expression.
        /// </summary>
        /// <param name="expression">The JSONata expression to evaluate.</param>
        /// <param name="context">The data context, where keys are available as $variable in the expression.</param>
        /// <returns>The result of the evaluation as a .NET object.</returns>
        public async Task<object?> Evaluate(string expression, IReadOnlyDictionary<string, object> state, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Evaluating complex Jsonata expression: {Expression}", expression);

            if (string.IsNullOrWhiteSpace(expression))
            {
                _logger.LogWarning("Jsonata expression is null or empty.");
                return null;
            }

            try
            {
                // 1. Сериализирај ја целата состојба користејќи SafeJsonSerializer.
                // Ова ги ракува [JsonIgnore], несериализибилни типови, кориснички стратегии и циклични референци.
                var json = SafeJsonSerializer.Serialize(state, _serializationHelper, _logger);

                _logger.LogTrace("Serialized entire state for JSONata: {Json}", json);

                // 2. Креирај JSONata environment и врзи ги сите топ-ниво клучеви од оригиналната состојба како променливи.
                // Ова овозможува изрази како $myVar да работат.
                var env = new EvaluationEnvironment();
                foreach (var kvp in state)
                {
                    try
                    {
                        // Сериализирај ја вредноста користејќи SafeJsonSerializer за да се ракуваат комплексни C# типови
                        var serializedValue = SafeJsonSerializer.Serialize(kvp.Value, _serializationHelper, _logger);
                        // Парсирај го серијализираниот JSON стринг во JToken
                        var jTokenValue = JToken.Parse(serializedValue);
                        env.BindValue(kvp.Key, jTokenValue);
                        _logger.LogTrace("Bound variable ${Key} to JSONata environment.", kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to bind variable ${Key} to JSONata environment. Value will be null in JSONata.", kvp.Key);
                        env.BindValue(kvp.Key, JToken.FromObject(null)); // Врзи како null ако серијализацијата не успее
                    }
                }

                _logger.LogDebug(
                    "JSONata transformation: Bound {BoundCount} variable(s) to environment.",
                    state.Count);

                // 6. Изврши ја трансформацијата
                var resultToken = await ExecuteJsonataQuery(expression, json, env, cancellationToken);



                _logger.LogDebug("Jsonata evaluation successful for expression: {Expression}", expression);

                // 7. Convert the result JToken back to a standard .NET object.
                return resultToken?.ToObject<object?>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate complex Jsonata expression '{Expression}'.", expression);
                throw new InvalidOperationException($"Failed to evaluate JSONata expression: {expression}", ex);
            }
        }



        private async Task<JToken> ExecuteJsonataQuery(
           string expression,
           string json,
           EvaluationEnvironment env,
           CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new JsonataQuery(expression);

                using var reader = new StringReader(json);
                var dataToken = await JToken.ParseAsync(reader, cancellationToken);

                var resultToken = query.Eval(dataToken, env);

                return resultToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "JSONata query execution failed. Expression: {Expression}, JSON: {Json}",
                    expression,
                    json);
                throw;
            }
        }
    }
}


