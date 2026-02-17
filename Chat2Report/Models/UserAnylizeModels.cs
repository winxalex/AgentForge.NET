// In Program.cs
using Chat2Report.Converter;
using Chat2Report.Utilities;
using System.Text.Json.Serialization;


namespace Chat2Report.Models
{

    public record ResolvedMatch(string CanonicalValue, double Score, ValueDefinition? RawValueDefinition);

   
   
    public class ResolvedValueCondition
    {
        public ColumnDefinition TargetColumn { get; set; }
        public List<string> CanonicalValues { get; set; } // Листа на канонски вредности
        public WhereHint OriginalHint { get; set; }       // Оригиналниот hint од LLM анализата
        public WhereHint EffectiveHint { get; set; }      // Финалниот hint (може да се смени од NotIn во In ако се користат антоними)
        public MentionedValueType OriginalType { get; set; }
        public MentionedValueType EffectiveType { get; set; }

        // Го чува точниот текст од корисничкото барање кој е разрешен.
        public string OriginalUserText { get; set; }

        public ValueDefinition? RawValueDefinition { get; set; }

        public bool IsFromAntonym { get; set; } = false;
        public double ValueMatchConfidence { get; set; } = 1.0; // Колку сме сигурни во ова совпаѓање на вредност (1.0 - score)



        public string ToWhereClauseString()
        {
            if (CanonicalValues == null || !CanonicalValues.Any()) return string.Empty;

            string colName = $"{TargetColumn.FullQualifiedTableName}.[{TargetColumn.Name}]"; // Quote column name for safety
            string op = GeneratorUtil.DetermineSqlOperatorFromHintEnum(EffectiveHint, CanonicalValues.Count > 1);

            switch (EffectiveHint)
            {
                case WhereHint.Unknown:
                    return string.Empty;
                case WhereHint.InList:
                case WhereHint.NotInList:
                    string inList = string.Join(", ", CanonicalValues.Select(cv => GeneratorUtil.FormatSqlValue(cv, TargetColumn)));
                    return $"{colName} {op} ({inList})";

                case WhereHint.BetweenInclusive:
                    if (CanonicalValues.Count == 2)
                    {
                        string val1 = GeneratorUtil.FormatSqlValue(CanonicalValues[0], TargetColumn);
                        string val2 = GeneratorUtil.FormatSqlValue(CanonicalValues[1], TargetColumn);
                        return $"{colName} {op} {val1} AND {val2}";
                    }
                    break; // Fallback to empty if not 2 values

                case WhereHint.BetweenExclusive:
                    if (CanonicalValues.Count == 2)
                    {
                        string val1 = GeneratorUtil.FormatSqlValue(CanonicalValues[0], TargetColumn);
                        string val2 = GeneratorUtil.FormatSqlValue(CanonicalValues[1], TargetColumn);
                        return $"({colName} > {val1} AND {colName} < {val2})";
                    }
                    break; // Fallback to empty if not 2 values

                case WhereHint.Contains:
                case WhereHint.StartsWith:
                case WhereHint.EndsWith:
                case WhereHint.Like:
                case WhereHint.NotLike:
                    string pattern = CanonicalValues.First();
                    if (EffectiveHint == WhereHint.Contains) pattern = $"%{pattern}%";
                    else if (EffectiveHint == WhereHint.StartsWith) pattern = $"{pattern}%";
                    else if (EffectiveHint == WhereHint.EndsWith) pattern = $"%{pattern}";
                    // For 'Like', the pattern is assumed to be provided as-is.
                    return $"{colName} {op} {GeneratorUtil.FormatSqlValue(pattern, TargetColumn)}";

                default: // Handles Equals, NotEquals, Greater, Less, etc.
                    if (CanonicalValues.Any())
                    {
                        return $"{colName} {op} {GeneratorUtil.FormatSqlValue(CanonicalValues.First(), TargetColumn)}";
                    }
                    break;
            }
            return string.Empty;
        }
    }


    [JsonConverter(typeof(WhereHintConverter))]
    public enum WhereHint
    {
        Unknown,
        Equals,
        NotEquals,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        BetweenInclusive,
        BetweenExclusive,
        InList,
        NotInList,
        Contains,
        StartsWith,
        EndsWith,
        Like, // Generic LIKE for complex patterns
        NotLike
    }


    [JsonConverter(typeof(MentionedValueTypeConverter))]
    public enum MentionedValueType
    {
        Unknown,
        SingleEnum,         // Единечна вредност од предефиниран сет (пр. статус "отворен")
        SingleDate,         // Единечна дата или релативен временски израз (пр. "вчера", "2023-01-15")
        SingleText,         // Единечна текстуална вредност која не е дел од предефиниран сет (пр. име "Марко", фраза "проблем со...")
        DateRange,          // Опсег од дати (пр. "минатата недела", "од 01.01 до 01.03.2023")
        NumericRange,       // Нумерички опсег (пр. "помеѓу 100 и 500", "над 250")
        EnumSet,            // Сет од повеќе enum вредности (пр. приоритет "висок ИЛИ многу висок")
        TextSet,            // Сет од повеќе текстуални вредности (пр. имиња на производи ['лаптоп', 'монитор'])
        //Like,               // За LIKE операции, каде вредноста содржи wildcards (пр. '%тест%')
        SingleNumeric,      // Единечна нумеричка вредност (пр. број на случај 12345)
        TemporalExpression, // Релативни временски изрази (пр. 'вчера","следната недела", "минатиот месец")
        // NumericSet      // (ПРЕДЛОГ ЗА ВО ИДНИНА) Сет од повеќе нумерички вредности (пр. броеви на случаи [101, 205, 300])
    }


   

    /// <summary>
    /// Represents the value associated with an extracted concept.
    /// Corresponds to the 'value' object in the JSON.
    /// </summary>
    public class ConceptValue
    {
        /// <summary>
        /// The extracted data, normalized as a list of strings.
        /// The source JSON might contain a single string, a number, or an array.
        /// A custom JsonConverter might be needed to handle non-array inputs gracefully.
        /// </summary>
        [JsonPropertyName("data")]
        [JsonConverter(typeof(FlexibleStringListConverter))]
        public List<string> Data { get; set; } = new List<string>();

        /// <summary>
        /// The type of the mentioned value.
        /// </summary>
        [JsonPropertyName("type")]
        // The converter is now applied at the enum level
        public MentionedValueType? Type { get; set; }

        /// <summary>
        /// The exact text from the user's query that corresponds to this value.
        /// </summary>
        [JsonPropertyName("original_user_text")]
        public string OriginalUserText { get; set; }

        /// <summary>
        /// A hint for constructing the WHERE clause in a SQL query.
        /// </summary>
        [JsonPropertyName("where_hint")]
        // The converter is now applied at the enum level
        public WhereHint? Hint { get; set; }

        /// <summary>
        /// Optional list of antonyms for the value.
        /// </summary>
        [JsonPropertyName("antonyms")]
        public List<string> Antonyms { get; set; }

        [JsonPropertyName("resolved")]
        public List<string> Resolved { get; set; } = new List<string>();


        public override string ToString()
        {

            return $"ConceptValue: Type={Type}, Data={string.Join(",",Data)}, Hint={Hint}, OriginalText='{OriginalUserText}'" +
                   (Antonyms != null && Antonyms.Any() ? $", AntonymsCount={Antonyms.Count}" : "");
        }
    }

    /// <summary>
    /// Represents a single extracted concept from the user query.
    /// Corresponds to an object in the 'extracted_concepts' array.
    /// </summary>
    public class ExtractedAttribute
    {
        /// <summary>
        /// The name of the concept, attribute, or metric.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

         /// <summary>
        
        /// </summary>
        [JsonPropertyName("schema_references")]
        public List<string> SchemaReferences { get; set; }

        /// <summary>
        /// The context of the concept.
        /// </summary>
        [JsonPropertyName("context")]
        public string Context { get; set; }

        /// <summary>
        /// The inferred intent for this concept (e.g., "Select", "Filter").
        /// </summary>
        [JsonPropertyName("intent")]
        public string Intent { get; set; }

        /// <summary>
        /// The value associated with this concept, if any. Can be null.
        /// </summary>
        [JsonPropertyName("value")]
        public ConceptValue? Value { get; set; }
    }

    /// <summary>
    /// Represents the root object of the user query analysis.
    /// </summary>
    public class UserQueryAnalysis
    {
        [JsonPropertyName("user_query")]
        public string UserQuery { get; set; }

        [JsonPropertyName("explicit_user_query" +
            "")]
        public string ExplicitUserQuery { get; set; }

        [JsonPropertyName("extracted_attributes")]
        public List<ExtractedAttribute> ExtractedAttributes { get; set; } = new List<ExtractedAttribute>();

        // These properties are for internal processing after initial deserialization.
        // They are ignored during JSON serialization/deserialization.
       
        [JsonIgnore]
        public List<ResolvedValueCondition> ResolvedConditions { get; set; } = new List<ResolvedValueCondition>();
        [JsonIgnore]
        public Dictionary<string, List<ColumnDefinition>> RelevantColumnsPerTableOrView { get; set; } = new Dictionary<string, List<ColumnDefinition>>();

        [JsonIgnore]
        public List<FunctionDefinition> RelevantFunctions { get; internal set; }

        
        /// <summary>
        /// Gets or sets a list of all relevant columns identified during analysis,
        /// formatted as dictionaries with 'name' and 'description' keys.
        /// </summary>
        public List<Dictionary<string, object>>? RelevantColumns { get; internal set; }

         /// <summary>
        /// A list of explicit JOIN conditions identified by the LLM and resolved against the schema.
        /// </summary>
        [JsonIgnore]
        public List<ResolvedJoinCondition> ResolvedJoins { get; set; } = new List<ResolvedJoinCondition>();


        // This property is for internal processing after initial deserialization.
        [JsonIgnore]
        public List<object> RelevantDomains { get; set; }

        [JsonIgnore]
        public List<object> RelevantViews { get; internal set; }

        [JsonIgnore]
        public List<object> RelevantTables { get; internal set; }


    }
}
