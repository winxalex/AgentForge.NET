using System.Collections.Concurrent;
using System.Text.Json;

namespace Chat2Report.Providers
{
    public class JSONStatePeristanceProvider : IStatePersitanceProvider
    {
        private readonly ConcurrentDictionary<string, JsonElement> _memory = new();
        private readonly JsonSerializerOptions _serializerOptions;

        public JSONStatePeristanceProvider()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // Add any other default options you need
            };
        }

        public T Get<T>(string key)
        {
            if (_memory.TryGetValue(key, out var jsonElement))
            {
                // Deserialize the JsonElement to the requested type T
                return jsonElement.Deserialize<T>(_serializerOptions);
            }
            return default; // Return default value for T if key is not found
        }

        public void Set<T>(string key, T value)
        {
            // Serialize the value of type T to a JsonElement
            var jsonElement = JsonSerializer.SerializeToElement(value, _serializerOptions);
            _memory.AddOrUpdate(key, jsonElement, (_, _) => jsonElement);
        }
    }
}
