﻿using Cloud.Unum.USearch;

namespace Chat2Report.Options
{
    /// <summary>
    /// Represents the settings for the vector store, including connection details and index configuration.
    /// This class maps to the `VectorStoreSettings` section in `appsettings.json`.
    /// </summary>
    public class VectorStoreSettings
    {
        /// <summary>
        /// Gets or sets the connection string for the underlying data store (e.g., SQLite).
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the configuration for the USearch index.
        /// </summary>
        public IndexOptions IndexSettings { get; set; }
    }

    /// <summary>
    /// Configuration options for the USearch index, corresponding to the `IndexSettings` subsection.
    /// </summary>
    public class IndexOptions
    {
        /// <summary>
        /// Gets or sets the path for persisting the USearch index files. Required.
        /// </summary>
        public string IndexPersistencePath { get; set; }

        /// <summary>
        /// Gets or sets the dimensionality of the vectors stored. Required. Must be greater than 0.
        /// </summary>
        public int VectorDimension { get; set; }

        /// <summary>
        /// Gets or sets the metric kind for distance calculation (e.g., Cosine, L2sq).
        /// </summary>
        public MetricKind MetricKind { get; set; } = MetricKind.Cos;

        /// <summary>
        /// Gets or sets the quantization method used for memory optimization.
        /// </summary>
        public ScalarKind Quantization { get; set; } = ScalarKind.Float32;

        /// <summary>
        /// Gets or sets the connectivity parameter for the HNSW graph construction.
        /// </summary>
        public ulong Connectivity { get; set; } = 16;

        /// <summary>
        /// Gets or sets the expansion factor during index construction (add operations).
        /// </summary>
        public ulong ExpansionAdd { get; set; } = 128;

        /// <summary>
        /// Gets or sets the expansion factor during search operations.
        /// </summary>
        public ulong ExpansionSearch { get; set; } = 64;

        /// <summary>
        /// Gets or sets the multiplier applied to (Top + Skip) to determine
        /// the initial number of candidates to fetch from the vector index during search.
        /// Fetches more results initially to account for subsequent filtering.
        /// </summary>
        public int SearchFetchMultiplier { get; set; } = 5;

        /// <summary>
        /// Gets or sets the minimum base number of candidates to fetch from the
        /// vector index, regardless of Top/Skip values. Ensures a reasonable pool
        /// size for filtering even when Top/Skip are small.
        /// </summary>
        public int SearchBaseFetchCount { get; set; } = 20;
    }
}
