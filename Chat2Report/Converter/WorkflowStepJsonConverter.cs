//// --- START OF FILE WorkflowStepJsonConverter.cs ---
using Chat2Report.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chat2Report.Converter
{
    public class WorkflowStepJsonConverter : JsonConverter<WorkflowStep>
    {
        public override WorkflowStep Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new WorkflowStep();
        }

        public override void Write(Utf8JsonWriter writer, WorkflowStep value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
//            if (reader.TokenType != JsonTokenType.StartObject)
//            {
//                throw new JsonException("Expected StartObject token");
//            }

//            var WorkflowStep = new WorkflowStep();

//            // Клонирај го читачот за да можеме да го извлечеме целиот JSON објект
//            // за 'Config' полето без да го потрошиме оригиналниот читач.
//            var readerClone = reader;

//            while (reader.Read())
//            {
//                if (reader.TokenType == JsonTokenType.EndObject)
//                {
//                    return WorkflowStep;
//                }

//                if (reader.TokenType == JsonTokenType.PropertyName)
//                {
//                    var propertyName = reader.GetString();
//                    reader.Read(); // Помести се до вредноста на својството

//                    // Користиме споредба без разлика на големина на букви
//                    if (string.Equals(propertyName, "Type", StringComparison.OrdinalIgnoreCase))
//                    {
//                        WorkflowStep.Type = reader.GetString();
//                    }
//                    else if (string.Equals(propertyName, "Config", StringComparison.OrdinalIgnoreCase))
//                    {
//                        // Најважниот дел: го парсираме Config објектот како JsonElement
//                        // и го зачувуваме.
//                        WorkflowStep.Config = JsonElement.ParseValue(ref reader);
//                    }
//                    else
//                    {
//                        // Игнорирај ги другите непознати својства
//                        reader.Skip();
//                    }
//                }
//            }

//            throw new JsonException("JSON payload is not completed.");
//        }

//        public override void Write(Utf8JsonWriter writer, WorkflowStep value, JsonSerializerOptions options)
//        {
//            // Не ни треба Write функционалност за сега, но мора да се имплементира.
//            writer.WriteStartObject();
//            writer.WriteString("Type", value.Type);

//            writer.WritePropertyName("Config");
//            value.Config.WriteTo(writer);

//            writer.WriteEndObject();
//        }
//    }
//}
//// --- END OF FILE WorkflowStepJsonConverter.cs ---