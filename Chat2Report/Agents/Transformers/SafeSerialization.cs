using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Chat2Report.Utilities;

/// <summary>
/// Безбеден JSON серијализатор што автоматски ракува со несериализибилни типови.
/// Клучната идеја: прво ја "прочистуваме" состојбата, па потоа сериализираме.
/// </summary>
public static class SafeJsonSerializer
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        MaxDepth = 64
    };

    /// <summary>
    /// Сериализира објект, прво го прочистувајќи преку ISerializationHelper.
    /// Ова гарантира дека сè што оди во JsonSerializer е веќе сериализибилно.
    /// </summary>
    public static string Serialize(
        object obj,
        ISerializationHelper serializationHelper,
        ILogger? logger = null)
    {
        // 1. Оптимистички пат: Обиди се да го серијализираш објектот директно.
        // Ова е најбрзиот пат и ќе успее за едноставни објекти или веќе "чисти" состојби.
        try
        {
            return JsonSerializer.Serialize(obj, _defaultOptions);
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
        {
            logger?.LogTrace(ex, "Direct serialization failed for type {Type}. Starting fallback procedure.", obj?.GetType().Name);
            // Ова е очекувано за комплексни состојби, па продолжуваме кон по-робусна логика.
        }

        // 2. Грануларен Fallback: Ако објектот е речник (најчест случај за состојба),
        // обработи го клуч-по-клуч. Ова ги изолира проблематичните вредности.
        if (obj is IDictionary<string, object> stateDict)
        {
            logger?.LogDebug("Object is a dictionary. Applying granular serialization fallback.");
            // Користиме Utf8JsonWriter за ефикасно градење на JSON стрингот без средни објекти.
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = _defaultOptions.WriteIndented }))
            {
                writer.WriteStartObject();
                foreach (var kvp in stateDict)
                {
                    try
                    {
                        // Рекурзивно серијализирај ја секоја вредност.
                        // Ова овозможува "добрите" вредности (пр. ValidationResult) да се серијализираат брзо,
                        // додека "лошите" (пр. ChartContent) ќе поминат низ своите fallback патеки.
                        var valueAsJsonString = Serialize(kvp.Value, serializationHelper, logger);
                        writer.WritePropertyName(kvp.Key);
                        // Ја запишуваме суровата JSON вредност, избегнувајќи двојна серијализација/ескејпинг.
                        writer.WriteRawValue(valueAsJsonString);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to safely serialize value for key '{Key}'. It will be omitted from the final JSON.", kvp.Key);
                    }
                }
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        // 3. Fallback за објекти кои не се речници (или ако самиот речник е од чуден тип)
        logger?.LogDebug("Object is not a dictionary. Applying standard fallbacks (Filtered, then Deep Clean).");

        // 3а. "Filtered" серијализација со TypeInfoResolver
        try
        {
            var safeOptions = new JsonSerializerOptions(_defaultOptions)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { typeInfo => ModifyTypeInfo(typeInfo, serializationHelper) } }
            };
            return JsonSerializer.Serialize(obj, safeOptions);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Filtered serialization failed for {Type}. Falling back to deep cleaning.", obj?.GetType().Name);
        }

        // 3б. Длабоко чистење (последна линија на одбрана)
        try
        {
            var cleanedObj = CleanObjectRecursively(obj, serializationHelper, logger);
            return JsonSerializer.Serialize(cleanedObj, _defaultOptions);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "Safe JSON serialization failed completely for object of type {Type}. Returning empty JSON object.",
                obj?.GetType().Name ?? "null");
            return "{}"; // Врати празен објект за да не падне апликацијата
        }
    }

    /// <summary>
    /// Modifier за System.Text.Json кој динамички ги исклучува својствата
    /// чии типови се означени како Ignored во ISerializationHelper.
    /// </summary>
    private static void ModifyTypeInfo(JsonTypeInfo typeInfo, ISerializationHelper helper)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            if (helper.IsIgnoredType(property.PropertyType))
            {
                // Ефективно додава [JsonIgnore] динамички
                property.ShouldSerialize = (_, _) => false;
            }
        }
    }

    /// <summary>
    /// Рекурзивно прочистува објект, заменувајќи ги несериализибилните типови
    /// со нивните serializable views.
    /// </summary>
    private static object? CleanObjectRecursively(
        object? obj,
        ISerializationHelper helper,
        ILogger? logger)
    {
        if (obj == null) return null;

        var type = obj.GetType();

        // 1. Едноставни типови - враќај директно
        if (IsSimpleType(type))
            return obj;

        // 2. Dictionary<string, object> - прочисти ги вредностите
        if (obj is IDictionary<string, object> dict)
        {
            var cleanDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dict)
            {
                cleanDict[kvp.Key] = CleanObjectRecursively(kvp.Value, helper, logger);
            }
            return cleanDict;
        }

        // 3. IEnumerable (Lists, Arrays, итн.) - прочисти ги елементите
        if (obj is System.Collections.IEnumerable enumerable && type != typeof(string))
        {
            var cleanList = new List<object?>();
            foreach (var item in enumerable)
            {
                cleanList.Add(CleanObjectRecursively(item, helper, logger));
            }
            return cleanList;
        }

        // 4. Комплексни објекти - користи SerializationHelper
        logger?.LogTrace("Cleaning complex object of type {Type}", type.Name);
        var serializableView = helper.GetSerializableView(obj);

        // Ако GetSerializableView врати Dictionary, рекурзивно прочисти го и него
        if (serializableView is IDictionary<string, object> viewDict)
        {
            return CleanObjectRecursively(viewDict, helper, logger);
        }

        return serializableView;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }
}