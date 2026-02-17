using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    /// <summary>
    /// Represents a resolved JOIN condition between two columns, as identified during query analysis.
    /// This is an intermediate representation used to build the final SQL query.
    /// </summary>
    public class ResolvedJoinCondition
    {
        /// <summary>
        /// The definition of the column on the "left" side of the join (e.g., from the 'FROM' table).
        /// </summary>
        [JsonIgnore] // This is a resolved object, not part of the initial LLM JSON output.
        public ColumnDefinition FromColumn { get; set; } = null!;

        /// <summary>
        /// The definition of the column on the "right" side of the join (e.g., from the 'JOIN' table).
        /// </summary>
        [JsonIgnore] // This is a resolved object, not part of the initial LLM JSON output.
        public ColumnDefinition ToColumn { get; set; } = null!;

        
    }
}