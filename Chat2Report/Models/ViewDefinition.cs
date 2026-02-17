using Microsoft.Extensions.VectorData;
using System.Collections.Generic;
using System;

namespace Chat2Report.Models
{
    /// <summary>
    /// Represents the definition of a database view, including its metadata and domain associations.
    /// </summary>
    public class ViewDefinition
    {
        /// <summary>
        /// A unique, deterministically generated ID for the view definition.
        /// </summary>
        [VectorStoreRecordKey]
        public ulong Id { get; set; }

        /// <summary>
        /// The fully qualified name of the view (e.g., "dbo.ActiveCasesView").
        /// </summary>
        public string FullQualifiedViewName { get; set; }

        /// <summary>
        /// The schema the view belongs to (e.g., "dbo").
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// The name of the view (e.g., "ActiveCasesView").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The business description of the view, parsed from extended properties.
        /// </summary>
        [VectorStoreRecordData]
        public string Description { get; set; }

        /// <summary>
        /// A list of business domains this view is associated with, parsed from extended properties.
        /// </summary>
        [VectorStoreRecordData(IsFilterable = true)]
        public List<string> BelongsToDomains { get; set; } = new List<string>();

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}