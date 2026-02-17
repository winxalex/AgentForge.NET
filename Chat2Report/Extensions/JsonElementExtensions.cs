using System.Text.Json;

namespace Chat2Report.Extensions
{
    /// <summary>
    /// Помошни екстензивни методи за работа со System.Text.Json.JsonElement.
    /// </summary>
    public static class JsonElementExtensions
    {
        private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Безбедно десеријализира 'Config' JsonElement во даден тип 'T'.
        /// Ако елементот е недефиниран или null, враќа нова инстанца од 'T'.
        /// </summary>
        /// <typeparam name="T">Типот во кој треба да се десеријализира.</typeparam>
        /// <param name="configElement">JsonElement кој ја претставува 'Config' секцијата.</param>
        /// <returns>Десеријализиран објект од тип 'T' или нова инстанца ако конфигурацијата недостасува.</returns>
        public static T DeserializeConfig<T>(this JsonElement configElement) where T : new()
        {
            if (configElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return new T();

            return configElement.Deserialize<T>(_options) ?? new T();
        }
    }
}