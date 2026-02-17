// Util/FlexibleStringListConverter.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chat2Report.Converter
{
    /// <summary>
    /// A custom JSON converter for a list of strings that can gracefully handle
    /// a JSON array containing mixed types (strings, numbers) and convert them all to strings.
    /// It can also handle the case where the JSON value is a single item instead of an array.
    /// </summary>
    public class FlexibleStringListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<string>();

            // Parse the current JSON token/value into a JsonElement.
            // This robustly handles both single values and entire arrays without complex reader management.
            var jsonElement = JsonElement.ParseValue(ref reader);

            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in jsonElement.EnumerateArray())
                {
                    list.Add(GetStringFromElement(element));
                }
            }
            else
            {
                // Handle the case where the value is a single item instead of an array.
                list.Add(GetStringFromElement(jsonElement));
            }

            return list;
        }

        private string GetStringFromElement(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            // For consistency, always write an array, even if there's only one item.
            writer.WriteStartArray();
            if (value != null)
            {
                foreach (var s in value) writer.WriteStringValue(s);
            }
            writer.WriteEndArray();
        }
    }
}