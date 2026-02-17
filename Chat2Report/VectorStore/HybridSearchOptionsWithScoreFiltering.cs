// --- START OF FILE HybridSearchOptionsWithScoreFiltering.cs ---
using System;
using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;

namespace Chat2Report.VectorStore
{
    /// <summary>
    /// Extends HybridSearchOptions to add an explicit filter for the vector search score.
    /// </summary>
    /// <typeparam name="TRecord">The Type of the data record.</typeparam>
    public class HybridSearchOptionsWithScoreFiltering<TRecord> : HybridSearchOptions<TRecord>
    {
        /// <summary>
        /// Gets or sets an optional expression to filter results based on their vector search score (distance).
        /// This filter is applied *after* any keyword or TRecord-based filtering.
        /// Example: score => score < 0.5f
        /// </summary>
        public Expression<Func<float, bool>>? ScoreFilter { get; set; }
    }
}
// --- END OF FILE HybridSearchOptionsWithScoreFiltering.cs ---