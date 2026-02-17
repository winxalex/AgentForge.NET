using System.Text;
using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    public class FunctionParameter
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("default")]
        public object DefaultValue { get; set; }
        [JsonPropertyName("type")]
        public string Type { get;set; }
    }

    public class FunctionReturnColumn
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class FunctionDefinition
    {
        public ulong Id { get; set; }
        public string FullQualifiedFunctionName { get; set; }
        public string FunctionSignature { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("typicalUsage")]
        public string TypicalUsage { get; set; }
        public string RawJsonDescription { get; set; }
        [JsonPropertyName("parameters")]
        public Dictionary<string, FunctionParameter> Parameters { get; set; } = new Dictionary<string, FunctionParameter>();
        [JsonPropertyName("returns")]
        public Dictionary<string, FunctionReturnColumn> Returns { get; set; } = new Dictionary<string, FunctionReturnColumn>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            // Determine if it's a function or procedure based on what's available
            sb.AppendLine($"Object: {FunctionSignature ?? FullQualifiedFunctionName}");
            sb.AppendLine($"  Description: {Description}");
            if (Parameters.Any())
            {
                sb.AppendLine("  Parameters:");
                foreach (var p in Parameters)
                {
                    sb.AppendLine($"    - {p.Key}:({p.Value.Type}) {p.Value.Description} (Default: {p.Value.DefaultValue})");
                }
            }
            if (Returns.Any())
            {
                sb.AppendLine("  Returns:");
                foreach (var r in Returns)
                {
                    sb.AppendLine($"    - {r.Key}:({r.Value.Type}) {r.Value.Description}");
                }
            }
            sb.AppendLine($"  Typical Usage: {TypicalUsage}");
            return sb.ToString();
        }
    }
}
