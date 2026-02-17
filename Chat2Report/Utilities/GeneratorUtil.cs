﻿using Chat2Report.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Chat2Report.Utilities
{
    /// <summary>
    /// Represents a class for demonstration purposes.
    /// </summary>
    public class GeneratorUtil
    {

        // Static SHA256 instance for reuse
        private static readonly System.Security.Cryptography.SHA256 _sha256 = System.Security.Cryptography.SHA256.Create();
        
        //private static ulong _nextId = 1; // Static variable to keep track of the next ID
        private static readonly object _lockObject = new();

        public static ulong GenerateDeterministicUlongId(string input, bool limitToInteger = false)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Input cannot be null or empty", nameof(input));

            lock (_lockObject) // Need lock since SHA256 operations are not thread-safe
            {
                byte[] hashBytes = _sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                ulong result = BitConverter.ToUInt64(hashBytes, 0);

                if (limitToInteger)
                    // Mask the highest bit to ensure the value is within the positive range of a signed long
                    return result & 0x7FFFFFFFFFFFFFFF;

                return result;
                //Console.WriteLine($"Generating ID{_nextId} for input: {input}");
                //return _nextId++; // Increment the static ID for each call
            }
        }

    

        public async static Task<(string CanonicalValue, double Score, ValueDefinition? RawValueDefinition)> FindCanonicalValueInVDBWithScore(
     IVectorStoreRecordCollection<ulong, ValueDefinition> vc,
     string term,
     IEmbeddingGenerator<string, Embedding<float>> gen,
     VectorSearchOptions<ValueDefinition> opts, // Ова треба да е VectorSearchOptions<ValueDefinition>
     double threshold,
     ILogger log)
        {
            if (string.IsNullOrEmpty(term)) return (null, double.MaxValue,null);
            //ReadOnlyMemory<float> emb = await gen.GenerateEmbeddingVectorAsync(term);
            ReadOnlyMemory<float> emb = await gen.GenerateVectorAsync(term, null); // Use GenerateVectorAsync for consistency with VectorSearchOptions
            var sr = await vc.VectorizedSearchAsync(emb, opts); // opts е VectorSearchOptions<ValueDefinition>
            await foreach (var res in sr.Results)
            {
                if (res.Record != null && res.Score.GetValueOrDefault(double.MaxValue) < threshold)
                {
                    log.LogInformation($"      Canonical found for '{term}': '{res.Record.ValueStringified}' (Score: {res.Score:F4})");
                    return (res.Record.ValueStringified, res.Score.GetValueOrDefault(double.MaxValue),res.Record);
                }
                else
                {
                    log.LogInformation($" {res.Record.ValueStringified} is no strong match '{term}' with score {res.Score:F4}.");
                }
            }
           
            return (null, double.MaxValue,null);
        }


        /// <summary>
        /// Пресметува Левенштајново растојание помеѓу два стринга.
        /// TODO: Install-Package Fastenshtein
        /// </summary>
        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }




    // Оваа функција може да биде статична во истата класа
    public static bool IsTypeCompatible(ColumnDefinition column, ConceptValue value)
        {
            string colType = column.Type.ToLowerInvariant();

            switch (value.Type)
            {
                case MentionedValueType.SingleEnum:
                case MentionedValueType.EnumSet:
                case MentionedValueType.SingleText:

                    // Текстуалните вредности може да се применат на текстуални или нумерички колони (пр. ID=5),
                    // бидејќи SQL ќе се справи со кастингот или ќе бидат правилно цитирани.
                    return colType.Contains("char") || colType.Contains("text") || colType.Contains("int") || colType.Contains("bigint") || colType.Contains("decimal") || colType.Contains("numeric");

                case MentionedValueType.SingleNumeric:
                case MentionedValueType.NumericRange:
                    // Вредност експлицитно идентификувана како нумеричка треба да се примени само на нумеричка колона.
                    return GeneratorUtil.IsSqlNumericType(colType);

                case MentionedValueType.SingleDate:
                case MentionedValueType.DateRange:
                case MentionedValueType.TemporalExpression:
                    return colType.Contains("datetime");

                default:
                    return false;
            }
        }



        //        public static async Task<(string? CanonicalValue, double Score, ValueDefinition? RawValueDefinition)> FindCanonicalValueInVDBWithScore(
        //            IVectorStoreRecordCollection<ulong, ValueDefinition> collection,
        //            string searchTerm,
        //            IEmbeddingGenerator<string, Embedding<float>> generator,
        //            VectorSearchOptions<ValueDefinition> options,
        //            double threshold,
        //            ILogger logger)
        //        {
        //            try
        //            {
        //                var queryVector = await generator.GenerateVectorAsync(searchTerm, null);
        //                var searchResult = await collection.VectorizedSearchAsync(queryVector, options);

        //                await foreach (var result in searchResult.Results)
        //                {
        //                    if (1 - result.Score < threshold)
        //                    {
        //                        return (result.Domain?.ValueStringified, 1 - result.Score, result.Domain);
        //                    }
        //                    else
        //                    {
        //                        logger.LogDebug("      ... rejected match '{Value}' with score {Score:F4} (threshold: {Threshold:F4})", result.Domain?.ValueStringified, 1 - result.Score, threshold);
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                logger.LogError(ex, "Error during vector search for term '{SearchTerm}'.", searchTerm);
        //            }
        //            return (null, 0, null);
        //        }
        //    }
        //}


        public static float[] Normalize(float[] vector)
        {
            float sumOfSquares = 0f;
            foreach (float val in vector)
            {
                sumOfSquares += val * val;
            }
            float magnitude = (float)Math.Sqrt(sumOfSquares);

            if (magnitude == 0f)
            {
                return vector; // Не може да се нормализира нулти вектор
            }

            float[] normalizedVector = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalizedVector[i] = vector[i] / magnitude;
            }
            return normalizedVector;
        }


        // Placeholder for a proper cosine similarity function.
        //Your vector library might provide this, or you can implement it.
        public static double CosineSimilarity(ReadOnlySpan<float> vec1, ReadOnlySpan<float> vec2)
        {
            if (vec1.Length != vec2.Length) throw new ArgumentException("Vectors must have the same dimension.");
            double dotProduct = 0.0;
            double mag1 = 0.0;
            double mag2 = 0.0;
            for (int i = 0; i < vec1.Length; i++)
            {
                dotProduct += vec1[i] * vec2[i];
                mag1 += vec1[i] * vec1[i];
                mag2 += vec2[i] * vec2[i];
            }
            if (mag1 == 0.0 || mag2 == 0.0) return 0.0; // Avoid division by zero
            return dotProduct / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        }


        public static string DetermineSqlOperatorFromHintEnum(WhereHint hint, bool isSet)
        {
            // Default operators based on whether we're dealing with a set
            if (hint == WhereHint.Unknown)
                return isSet ? "IN" : "=";

            switch (hint)
            {
                case WhereHint.NotEquals:
                    return "!=";
                case WhereHint.NotInList:
                    return "NOT IN";
                case WhereHint.Greater:
                    return ">";
                case WhereHint.GreaterOrEqual:
                    return ">=";
                case WhereHint.Less:
                    return "<";
                case WhereHint.LessOrEqual:
                    return "<=";
                case WhereHint.BetweenInclusive:
                    return "BETWEEN"; // Will need additional logic for the AND part
                case WhereHint.BetweenExclusive:
                    return ">"; // Will need additional logic for the AND < part
                case WhereHint.Like:
                    return "LIKE";
                case WhereHint.NotLike:
                    return "NOT LIKE";
                case WhereHint.InList:
                    return "IN";
                case WhereHint.Equals:
                    return "=";
                default:
                    return isSet ? "IN" : "=";
            }
        }

        public static bool IsSqlNumericType(string sqlType)
        {
            // A helper to identify common SQL numeric types.
            string type = sqlType.ToLowerInvariant();
            return type.Contains("int") ||      // tinyint, smallint, int, bigint
                   type.Contains("decimal") ||
                   type.Contains("numeric") ||
                   type.Contains("float") ||
                   type.Contains("real") ||
                   type.Contains("money") ||
                   type.Contains("bit");
        }

        public static string FormatSqlValue(string value, ColumnDefinition column)
        {
            string colTypeLower = column.Type.ToLowerInvariant();

            // Check for numeric types first to avoid quoting them.
            if (IsSqlNumericType(colTypeLower))
            {
                // For numeric types, we shouldn't add quotes.
                // We assume the value is a valid number string.
                // We still escape single quotes just in case, though unlikely for numbers.
                return value.Replace("'", "''");
            }

            // For all other types (strings, dates, etc.), we add quotes.
            // For Unicode string types, we add the 'N' prefix.
            bool isUnicode = colTypeLower.StartsWith("n") || colTypeLower == "xml";
            string prefix = isUnicode ? "N" : "";
            return $"{prefix}'{value.Replace("'", "''")}'";
        }


        /// <summary>
        /// Converts a string from various formats (camelCase, snake_case, kebab-case) to PascalCase.
        /// This version correctly handles acronyms and mixed-case strings.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <returns>The PascalCase version of the string.</returns>
        public static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Split on underscores, spaces, or hyphens. Also, split before an uppercase letter that is not at the start.
            var parts = Regex.Split(input, @"(?<=[a-z])(?=[A-Z])|[_\s\-]+");
            return string.Concat(parts.Select(p =>
                p.Length > 0
                ? char.ToUpperInvariant(p[0]) + p.Substring(1) // Don't force the rest to lower case
                : ""
            ));
        }

    }
}
