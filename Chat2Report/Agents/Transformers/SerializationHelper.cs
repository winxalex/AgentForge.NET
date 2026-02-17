using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Chat2Report.Utilities;

/// <summary>
/// Интерфејс за детекција и ракување со сериализација
/// </summary>
public interface ISerializationHelper
{
    /// <summary>
    /// Враќа сериализибилна претстава на објектот.
    /// Враќа null ако објектот треба да се игнорира.
    /// </summary>
    object? GetSerializableView(object? value);

    /// <summary>
    /// Регистрирај custom стратегија за одреден тип
    /// </summary>
    void RegisterStrategy(Type type, Func<object, object?> converter);

    /// <summary>
    /// Регистрирај тип што треба целосно да се игнорира
    /// </summary>
    void RegisterIgnoredType(Type type);

    /// <summary>
    /// Проверува дали типот е регистриран за игнорирање
    /// </summary>
    bool IsIgnoredType(Type type);
}

/// <summary>
/// SerializationHelper со три нивоа на ракување:
/// 1. Custom стратегии (највисок приоритет)
/// 2. Ignored типови (враќа null)
/// 3. Fallback кон ToDeepDictionary (автоматско)
/// </summary>
public class SerializationHelper : ISerializationHelper
{
    private readonly Dictionary<Type, Func<object, object?>> _customStrategies = new();
    private readonly HashSet<Type> _ignoredTypes = new();
    private readonly HashSet<Type> _processingTypes = new(); // За детекција на циклични типови
    private readonly ILogger<SerializationHelper>? _logger;

    public SerializationHelper(ILogger<SerializationHelper>? logger = null)
    {
        _logger = logger;
        RegisterDefaultIgnoredTypes();
    }

    private void RegisterDefaultIgnoredTypes()
    {
        // Streams
        RegisterIgnoredType(typeof(Stream));
        RegisterIgnoredType(typeof(MemoryStream));
        RegisterIgnoredType(typeof(FileStream));

        // Async/Threading
        RegisterIgnoredType(typeof(IAsyncEnumerable<>));
        RegisterIgnoredType(typeof(Task));
        RegisterIgnoredType(typeof(Task<>));
        RegisterIgnoredType(typeof(CancellationToken));
        RegisterIgnoredType(typeof(CancellationTokenSource));
        RegisterIgnoredType(typeof(Thread));

        // Reflection
        RegisterIgnoredType(typeof(MethodInfo));
        RegisterIgnoredType(typeof(PropertyInfo));
        RegisterIgnoredType(typeof(FieldInfo));
        RegisterIgnoredType(typeof(Type));
        RegisterIgnoredType(typeof(Assembly));

        // Delegates
        RegisterIgnoredType(typeof(Delegate));
        RegisterIgnoredType(typeof(MulticastDelegate));
        RegisterIgnoredType(typeof(Action));
        RegisterIgnoredType(typeof(Func<>));

        // Database
        RegisterIgnoredType(typeof(System.Data.IDbConnection));
        RegisterIgnoredType(typeof(System.Data.IDbCommand));
    }

    public void RegisterStrategy(Type type, Func<object, object?> converter)
    {
        _customStrategies[type] = converter;
        _logger?.LogDebug("Registered custom serialization strategy for {Type}", type.Name);
    }

    public void RegisterIgnoredType(Type type)
    {
        _ignoredTypes.Add(type);
        _logger?.LogTrace("Registered ignored type {Type}", type.Name);
    }

    public object? GetSerializableView(object? value)
    {
        if (value == null) return null;

        var type = value.GetType();

        // 1. Едноставни типови - враќај директно
        if (IsSimpleType(type))
            return value;

        // 2. Ignored типови - враќа null
        if (IsIgnoredType(type))
        {
            _logger?.LogTrace("Ignoring type {Type}", type.Name);
            return null;
        }

        // 3. Custom стратегии - највисок приоритет
        if (_customStrategies.TryGetValue(type, out var strategy))
        {
            try
            {
                var result = strategy(value);
                _logger?.LogTrace("Applied custom strategy for {Type}", type.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Custom strategy failed for {Type}", type.Name);
                return null;
            }
        }

        // 4. Collections - прочисти ги елементите
        if (value is IEnumerable enumerable && type != typeof(string))
        {
            // Ако е Dictionary, третирај го специјално
            if (value is IDictionary dictionary)
            {
                return ConvertDictionary(dictionary);
            }

            // Други колекции - конвертирај во List
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                var cleanItem = GetSerializableView(item);
                list.Add(cleanItem);
            }
            return list;
        }

        // 5. Fallback: Конвертирај во Dictionary
        _logger?.LogDebug("Using ToDeepDictionary fallback for {Type}", type.Name);
        return ToDeepDictionary(value);
    }

    private Dictionary<string, object?> ConvertDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrEmpty(key)) continue;

            var cleanValue = GetSerializableView(entry.Value);
            result[key] = cleanValue;
        }

        return result;
    }

    private Dictionary<string, object?>? ToDeepDictionary(object obj)
    {
        var type = obj.GetType();

        // Детекција на циклични референци
        if (!_processingTypes.Add(type))
        {
            _logger?.LogWarning("Detected circular reference for type {Type}", type.Name);
            return null;
        }

        try
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                // Скокни [JsonIgnore] properties
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                // Скокни indexed properties (this[int index])
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                try
                {
                    var value = prop.GetValue(obj);
                    var cleanValue = GetSerializableView(value);
                    result[prop.Name] = cleanValue;
                }
                catch (Exception ex)
                {
                    // Property throw exception при read - скокни го
                    _logger?.LogTrace(ex, "Skipping property {Property} of {Type}", prop.Name, type.Name);
                    continue;
                }
            }

            return result.Count > 0 ? result : null;
        }
        finally
        {
            _processingTypes.Remove(type);
        }
    }

    public bool IsIgnoredType(Type type)
    {
        // Директна проверка
        if (_ignoredTypes.Contains(type))
            return true;

        // Проверка за наследени типови
        foreach (var ignoredType in _ignoredTypes)
        {
            // Проверка за директно наследување
            if (ignoredType.IsAssignableFrom(type))
                return true;

            // Проверка за генерички типови (Task<T>, IAsyncEnumerable<T>)
            if (type.IsGenericType && ignoredType.IsGenericTypeDefinition)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == ignoredType || ignoredType.IsAssignableFrom(genericDef))
                    return true;
            }
        }

        return false;
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
               type == typeof(Guid) ||
               Nullable.GetUnderlyingType(type) != null;
    }
}