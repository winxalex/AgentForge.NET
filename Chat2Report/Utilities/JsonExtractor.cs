using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Chat2Report.Utilities
{
    /// <summary>
    /// A utility class for extracting JSON from a string.
    /// </summary>
    /// <remarks>
    /// This class provides a method to extract JSON content enclosed within triple backticks and the "json" language identifier.
    /// </remarks>
    public class JsonExtractor
    {
        public static string ExtractJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            // 1) Prefer fenced ```json blocks (common LLM format)
            string fencedPattern = @"(?s)```json\s*(\{[\s\S]*?\}|\[[\s\S]*?\])\s*```";
            Match match = Regex.Match(input, fencedPattern);
            if (match.Success && IsValidJson(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Trim();
            }

            // 2) Fall back to the first valid JSON object/array found in the text
            foreach (var candidate in ExtractJsonCandidates(input))
            {
                if (IsValidJson(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsValidJson(string json)
        {
            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static IEnumerable<string> ExtractJsonCandidates(string input)
        {
            var candidates = new List<string>();
            int length = input.Length;

            for (int i = 0; i < length; i++)
            {
                char ch = input[i];
                if (ch == '{' || ch == '[')
                {
                    int start = i;
                    if (TryFindJsonEnd(input, start, out int end))
                    {
                        candidates.Add(input.Substring(start, end - start + 1).Trim());
                        i = end;
                    }
                }
            }

            return candidates;
        }

        private static bool TryFindJsonEnd(string input, int start, out int end)
        {
            end = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < input.Length; i++)
            {
                char ch = input[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{' || ch == '[')
                {
                    depth++;
                }
                else if (ch == '}' || ch == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
