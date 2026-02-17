using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Chat2Report.Models
{
    /// <summary>
    /// Represents structured tabular data content.
    /// </summary>
    public class TableContent : AIContent
    {
        /// <summary>
        /// Gets or sets the rows of the table. Each row is represented as a dictionary
        /// where the key is the column name (header) and the value is the cell content.
        /// </summary>
        [JsonPropertyName("rows")]
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }

        public TableContent(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        {
            Rows = rows;
        }
    }
}