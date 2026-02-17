// TableDefinition.cs
using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chat2Report.Models
{
    /// <summary>
    /// Represents the definition of a database table, including its metadata and vector embedding.
    /// </summary>
    public class TableDefinition
    {
        [VectorStoreRecordKey]
        public ulong Id { get; set; }

        /// <summary>
        /// The fully qualified name of the table (e.g., "dbo.Products").
        /// </summary>
        public string FullQualifiedTableName { get; set; } = string.Empty;

        /// <summary>
        /// The schema of the table.
        /// </summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>
        /// The name of the table.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A description of the table's purpose or content, likely from extended properties.
        /// </summary>
        public string? Description { get; set; }

        public List<string> BelongsToDomains { get; set; } = new List<string>();

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}