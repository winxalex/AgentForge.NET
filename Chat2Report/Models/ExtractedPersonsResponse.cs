using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    public class Person
    {
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class ExtractedPersonsResponse
    {
        [JsonPropertyName("extracted_persons")]
        public List<Person> ExtractedPersons { get; set; } = new List<Person>();

        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; } = string.Empty;
    }
}