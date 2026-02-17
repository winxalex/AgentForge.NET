using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chat2Report.Models;

namespace Chat2Report.Converter
{
    /// <summary>
    /// A custom JSON converter for enums that defaults to an 'Unknown' value
    /// if the string from the JSON does not match any of the enum members.
    /// This prevents deserialization errors for unexpected enum values.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to convert.</typeparam>
    public class JsonStringEnumConverterWithUnknown<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        private readonly JsonNamingPolicy _namingPolicy;
        private readonly TEnum _unknownValue;

        /// <summary>
        /// Initializes a new instance of the converter.
        /// It assumes 'Unknown' is a member of the enum and has a value of 0.
        /// </summary>
        /// <param name="namingPolicy">Optional naming policy for serialization.</param>
        public JsonStringEnumConverterWithUnknown(JsonNamingPolicy? namingPolicy = null)
        {
            _namingPolicy = namingPolicy ?? JsonNamingPolicy.CamelCase;

            // Assume the default 'Unknown' value is at index 0.
            if (!Enum.IsDefined(typeof(TEnum), (TEnum)(object)0))
            {
                throw new InvalidOperationException($"The enum {typeof(TEnum).Name} must have a member with value 0, typically named 'Unknown'.");
            }
            _unknownValue = (TEnum)(object)0;
        }

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                return _unknownValue;
            }

            string? enumString = reader.GetString();

            // Try to parse, ignoring case. If it fails, return the unknown value.
            if (Enum.TryParse<TEnum>(enumString, ignoreCase: true, out var parsedEnum))
            {
                return parsedEnum;
            }

            return _unknownValue;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(_namingPolicy.ConvertName(value.ToString()));
        }
    }

    /// <summary>
    /// Concrete implementation of the generic enum converter for the <see cref="WhereHint"/> enum.
    /// </summary>
    public class WhereHintConverter : JsonStringEnumConverterWithUnknown<WhereHint>
    {
    }

    /// <summary>
    /// Concrete implementation of the generic enum converter for the <see cref="MentionedValueType"/> enum.
    /// </summary>
    public class MentionedValueTypeConverter : JsonStringEnumConverterWithUnknown<MentionedValueType>
    {
    }
}