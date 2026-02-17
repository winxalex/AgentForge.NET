using System.Text.Json.Serialization;
using System.Text.Json;
using Chat2Report.Converter;

namespace Chat2Report.Models
{
    
    public class WorkflowStepDefinition
    {
        [JsonPropertyName("Type")]
        public required string Type { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }

     
        [JsonPropertyName("Inputs")]
        // Key е името на локалниот параметар (на пр. "embedding"), 
        // Value е клучот во WorkflowState (на пр. "embedding_vector").
        public Dictionary<string, string> Inputs { get; set; } = new();

        [JsonPropertyName("Outputs")]
        // Key е името на локалниот излез од IOContract, Value е дефиницијата на Output
        public Dictionary<string, OutputDefinition> Outputs { get; set; } = new();

        // Останатите параметри специфични за старата Config секција.
        [JsonPropertyName("Config")]
        public JsonElement Config { get; set; }
    }


}
