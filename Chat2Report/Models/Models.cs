// --- START OF FILE Models.cs ---
using Chat2Report.Converter;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    /// <summary>
    /// Глобална конфигурација за целиот систем на агенти.
    /// </summary>
    public class AgentsConfiguration
    {
        [JsonPropertyName("Clients")]
        public Dictionary<string, ClientConfig> Clients { get; set; } = new();
        public Dictionary<string, StepDefinition> StepDefinitions { get; set; } = new();
        public Dictionary<string, WorkflowDefinition> Workflows { get; set; } = new();

        public ClientTimeoutOptions? DefaultTimeoutOptions { get; set; }
    }


    public class StepDefinition
    {
        public string? Description { get; set; }
        public JsonElement IOContract { get; set; }
    }


    public class WorkflowDefinition
    {
       
        public Dictionary<string, AgentDefinition> Agents { get; set; } = new();
    }

    /// <summary>
    /// Конфигурација за еден единствен агент. Дефинира кој е, што прави (Steps) и како рутира (Rules).
    /// </summary>
    [Serializable]
    public class AgentDefinition
    {

        public string TopicId { get; set; }
        public string Description { get; set; }

        [JsonRequired]
        public List<WorkflowStepDefinition> Steps { get; set; }

        public List<RoutingRule> Routes { get; set; }
    }


    public class JsonElementConverter : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.Clone();
        }

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            value.WriteTo(writer);
        }
    }



    /// <summary>
    /// Конфигурација за еден чекор во работниот тек на агентот.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(WorkflowStepJsonConverter))]
    public class WorkflowStep
    {
        [JsonRequired]
        public string Type { get; set; }


        //public JsonElement Config { get; set; }
        [JsonConverter(typeof(JsonElementConverter))]
        
        public JsonElement Config { get; set; }
    }

    /// <summary>
    /// Глобална конфигурација за поврзување со клиент (LLM, SQL, итн.).
    /// Не содржи параметри за однесување како Temperature.
    /// </summary>
    [Serializable]
    public class ClientConfig
    {
        [JsonRequired]
        public string Type { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public bool Log { get; set; }
        [JsonRequired]
        public string ModelId { get; set; }
        
        public bool ThrowOnError { get; set; } = true;

      
        public ChatOptions Options { get; set; }
    }

   

    [Serializable]
    public class ResponseFormat
    {
        [JsonRequired]
        public ResponseFormatType Type { get; set; }
        [JsonRequired]
        public string Format { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ResponseFormatType { Text, Json, Type }

    [Serializable]
    public class ToolConfig
    {
        [JsonRequired]
        public string AssemblyQualifiedName { get; set; }
        [JsonRequired]
        public string Method { get; set; }
        public string? Description { get; set; }
        public string? DescriptiveFunctionName { get; set; }
    }

    [Serializable]
    public class RoutingRule
    {
        public string Condition { get; set; }
        [JsonRequired]
        public List<string> Receivers { get; set; }
        public TransformOptions Transform { get; set; }

        [JsonPropertyName("cleanupPolicy")]
        public CleanupPolicy? CleanupPolicy { get; set; }
    }




    public class CleanupPolicy
    {
        [JsonPropertyName("removeKeys")]
        public List<string>? RemoveKeys { get; set; }

        [JsonPropertyName("removeKeysWhen")]
        public Dictionary<string, string>? RemoveKeysWhen { get; set; }
    }

    [Serializable]
    public class TransformOptions
    {
        public string AssemblyQualifiedName { get; set; }
        public string Method { get; set; }
        public string Expression { get; set; }
        public string ServiceType { get; set; }
    }


    [Serializable]
    public class ClientTimeoutOptions
    {
        public int? RequestTimeoutMinutes { get; set; }
        public int? StreamingInactivityTimeoutMinutes { get; set; }
    }

   /// <summary>
    /// Конфигурација за складирање на историјата на работниот тек.
    /// </summary>
    [Serializable]
    public class HistoryStoreSettings
    {
        /// <summary>
        /// Патеката до основниот фолдер каде што ќе се зачувуваат датотеките со историја.
        /// </summary>
        public string BasePath { get; set; }
    }

}
// --- END OF FILE Models.cs ---