using System.Text.Json;

namespace Chat2Report.Utilities
{
    public class JsonToDictionaryConverter
    {
        /// <summary>
        /// Deserialize to Dictionary&lt;string,object&gt; using System.Text.Json
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static Dictionary<string, object> DeserializeToDictionary(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException($"Invalid JSON: {json}");
            }

            try
            {
                var jsonObject = JsonDocument.Parse(json).RootElement;

                if (jsonObject.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Expected a JSON object, but found a different JSON type.");
                }

                var dictionary = new Dictionary<string, object>();

                foreach (var property in jsonObject.EnumerateObject())
                {
                    dictionary.Add(property.Name, ConvertJsonElementToObject(property.Value));
                }

                return dictionary;
            }
            catch (JsonException ex)
            {
                // Handle invalid JSON object
                Console.WriteLine($"Error deserializing JSON {json} to Dictionary: {ex.Message}");
                throw;
            }
        }

        private static object ConvertJsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var dictionary = new Dictionary<string, object>();
                        foreach (var property in element.EnumerateObject())
                        {
                            dictionary.Add(property.Name, ConvertJsonElementToObject(property.Value));
                        }
                        return dictionary;
                    }
                case JsonValueKind.Array:
                    {
                        var list = element.EnumerateArray()
                            .Select(ConvertJsonElementToObject)
                            .ToList();

                        if (!list.Any())
                        {
                            return list; // Return empty List<object>
                        }

                        // To improve usability, attempt to convert the List<object> to a strongly-typed
                        // list if all elements are of the same or compatible primitive types.

                        // All strings?
                        if (list.All(i => i is string))
                        {
                            return list.Cast<string>().ToList();
                        }

                        // All booleans?
                        if (list.All(i => i is bool))
                        {
                            return list.Cast<bool>().ToList();
                        }

                        // All numbers? Handle type promotion (int -> long -> double).
                        if (list.All(i => i is int || i is long || i is double))
                        {
                            // If any element is a double, convert the whole list to double for consistency.
                            if (list.Any(i => i is double))
                            {
                                return list.Select(Convert.ToDouble).ToList();
                            }

                            // If any element is a long (and no doubles), convert to long.
                            if (list.Any(i => i is long))
                            {
                                return list.Select(Convert.ToInt64).ToList();
                            }

                            // Otherwise, all must be ints.
                            return list.Cast<int>().ToList();
                        }

                        // Fallback for heterogeneous arrays (e.g., [1, "a", true]), arrays with nulls,
                        // or arrays of complex objects/other arrays.
                        return list;
                    }
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                    {
                        return intValue;
                    }
                    else if (element.TryGetDouble(out double doubleValue))
                    {
                        return doubleValue;
                    }
                    else if (element.TryGetInt64(out long longValue))
                    {
                        return longValue;
                    }
                    throw new ArgumentException($"Invalid number format:{element.ToString()}. Supported int,double,long.");
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return null;
            }
        }
    }
}
