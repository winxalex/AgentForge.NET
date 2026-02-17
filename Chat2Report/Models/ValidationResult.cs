using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    /// <summary>
    /// Претставува структуриран резултат од 'QueryValidationAgent'.
    /// Овој објект се десеријализира од JSON одговорот на LLM-от.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Укажува дали корисничкиот упит е валиден (read-only и тематски соодветен).
        /// LLM-от го пополнува ова како `is_valid` во JSON-от.
        /// </summary>
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        /// <summary>
        /// Кратко образложение на македонски јазик за одлуката.
        /// Ако упитот е невалиден, оваа причина може да му се прикаже на корисникот.
        /// LLM-от го пополнува ова како `reason` во JSON-от.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        // Иако не е експлицитно во промптот, LLM-от може да врати и категорија.
        // Додавањето на ова проперти го прави моделот поотпорен на мали варијации.
        // [JsonPropertyName("category")]
        // public string? Category { get; set; }
    }
}