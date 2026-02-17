using System.Text.Json.Serialization;
using System.Text.Json;
using Chat2Report.Converter;

namespace Chat2Report.Models
{
    // Класи за конфигурација на InstanceConfiguration
    public class OutputDefinition
    {
        [JsonPropertyName("key")]
        public required string Key { get; set; }

        [JsonPropertyName("scope")]
        // Мора да го поддржува ова. Ќе дефинираме enum.
        public required StateScope Scope { get; set; }

        // Останатите пропертиа од старата конфигурација
        // [JsonPropertyName("description")] 
        // public string? Description { get; set; }
    }



    public enum StateScope
    {
        Unspecified = 0,

        // Живее само за следниот чекор, Engine-от го брише на стартот на чекор А+2.
        Step,

        // Живее додека трае инвокацијата на агентот. Engine-от го брише на крај на HandleAsync.
        Agent,

        // Патува помеѓу агенти. Се брише само со експлицитен cleanupPolicy.
        Workflow
    }


}
