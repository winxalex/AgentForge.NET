﻿namespace Chat2Report.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Diagnostics;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Unicode;
    using System.Reflection;

    public static class DictionaryExtensions
    {
 
        public static string ToJSON(this IReadOnlyDictionary<string, object?> dictionary, bool indented = false)
        {
            return JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = indented });
        }

        public static string ToJSON(this Dictionary<string, object> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {

                return "{}";
            }

            return JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), WriteIndented = true });

        }


        public static string ToStringAll(this Dictionary<string, object> dictionary)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in dictionary)
            {
                sb.AppendLine($"  Key: {kvp.Key}, Value: {kvp.Value}");
            }

            return sb.ToString();
        }
        

        /// <summary>
        /// Sets a value in a nested dictionary structure using a dot-separated path.
        /// Creates nested dictionaries if they don't exist.
        /// </summary>
        /// <param name="data">The dictionary to modify.</param>
        /// <param name="path">The dot-separated path (e.g., "a.b.c").</param>
        /// <param name="value">The value to set.</param>
        /// <param name="logger">Optional logger for warnings.</param>
        public static void SetValueByPath(this IDictionary<string, object> data, string path, object value, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                logger?.LogWarning("Path is null or empty. Cannot set value in state.");
                return;
            }

            var parts = path.Split('.');
            IDictionary<string, object> current = data;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (!current.TryGetValue(part, out var next) || next is not IDictionary<string, object> nextDict)
                {
                    // Create a new dictionary with case-insensitive keys for consistency
                    nextDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    current[part] = nextDict;
                }
                current = nextDict;
            }

            current[parts.Last()] = value;
        }

        /// <summary>
        /// Gets a value from a nested dictionary or object structure using a dot-separated path.
        /// Handles both IDictionary<string, object> and properties of complex objects.
        /// </summary>
        public static bool TryGetValueByPath(this IReadOnlyDictionary<string, object> state, string path, out object? value)
        {
            var pathParts = path.Split('.');
            object? currentObject = null;

            if (!state.TryGetValue(pathParts[0], out currentObject))
            {
                value = null;
                return false;
            }

            for (int i = 1; i < pathParts.Length; i++)
            {
                if (currentObject == null) { value = null; return false; }

                if (currentObject is IDictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(pathParts[i], out currentObject)) { value = null; return false; }
                }
                else
                {
                    // Fallback to reflection for complex objects
                    var prop = currentObject.GetType().GetProperty(string.Concat(pathParts[i].Select((c, j) => j == 0 ? char.ToUpper(c) : c)), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null) { value = null; return false; }
                    currentObject = prop.GetValue(currentObject);
                }
            }

            value = currentObject;
            return true;
        }
    }


}
