using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Utilities;
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;

namespace Chat2Report.Agents.Transformers;

public class JsonataTransformer : IMessageTransformer
{
    private readonly ILogger<JsonataTransformer> _logger;
    private readonly ISerializationHelper _serializationHelper;

    public JsonataTransformer(
        ISerializationHelper serializationHelper,
        ILogger<JsonataTransformer> logger = null)
    {
        _serializationHelper = serializationHelper ?? throw new ArgumentNullException(nameof(serializationHelper));
        _logger = logger ?? LoggerFactory.Create(factory => factory.AddConsole())
            .CreateLogger<JsonataTransformer>();
    }

    public async Task<Dictionary<string, object>> TransformAsync(
        TransformOptions options,
        Dictionary<string, object> state,
        IStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options?.Expression))
            throw new ArgumentException("Expression must be provided in TransformOptions.", nameof(options));

        var expression = options.Expression;

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
                    env.BindValue(kvp.Key, null); // Врзи како null ако серијализацијата не успее
                }
            }

            _logger.LogDebug(
                "JSONata transformation: Bound {BoundCount} variable(s) to environment.",
                state.Count);

            // 6. Изврши ја трансформацијата
            var resultToken = await ExecuteJsonataQuery(expression, json, env, cancellationToken);

            // 7. Конвертирај го резултатот назад во Dictionary
            var resultDict = JsonToDictionaryConverter.DeserializeToDictionary(resultToken.ToFlatString());

            _logger.LogDebug(
                "JSONata transformation successful. Result keys: {Keys}",
                string.Join(", ", resultDict.Keys));

            return resultDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "JSONata transformation failed. Expression: {Expression}",
                expression);
            throw new InvalidOperationException(
                $"Failed to evaluate JSONata expression: {expression}",
                ex);
        }
    }

    /// <summary>
    /// Извршува JSONata query
    /// </summary>
    private async Task<JToken> ExecuteJsonataQuery(
        string expression,
        string json,
        EvaluationEnvironment env,
        CancellationToken cancellationToken=default)
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