﻿// MSSQLDataSchemaService.cs
using System.IO;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;



namespace Chat2Report.Services
{
    /// <summary>
    /// Implements IDataSchemaService for Microsoft SQL Server.
    /// Includes error handling for database operations.
    /// </summary>
    public class MSSQLDataSchemaService : IDataSchemaService
    {
        private readonly string _connectionString;
        private readonly SchemaProcessingSettings _settings;

        // Add a logger field to MSSQLDataSchemaService if not already present
        private readonly ILogger<MSSQLDataSchemaService> _logger;

        // Cache for database relationships, populated by InitializeRelationshipCacheAsync.
        private List<TableRelationship> _relationshipsCache;
        private readonly object _cacheLock = new object();
        private bool _isCacheInitialized = false;

        // And initialize in constructor: _logger = loggerFactory.CreateLogger<MSSQLDataSchemaService>();

        public MSSQLDataSchemaService(
            IOptions<DataStoreSettings> options, 
            IOptions<SchemaProcessingSettings> schemaSettings,
            ILogger<MSSQLDataSchemaService> logger = null)
        {
            _logger = logger ?? LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<MSSQLDataSchemaService>();
            options = options ?? throw new ArgumentNullException(nameof(options));
            _settings = schemaSettings?.Value ?? throw new ArgumentNullException(nameof(schemaSettings));
            var dataStoreSettings = options.Value ?? throw new ArgumentNullException(nameof(options.Value));
            _connectionString = dataStoreSettings.ConnectionString;
        }

        private void ParseTableWithSchema(string tableWithSchema, out string schema, out string tableName)
        {
            var parts = tableWithSchema.Split(new[] { '.' }, 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException("Table name must be in the format 'schema.TableName'", nameof(tableWithSchema));
            }
            schema = parts[0];
            tableName = parts[1];
        }

        /// <summary>
        /// Proactively fetches and caches database relationships at startup.
        /// This method should be called once when the application initializes.
        /// </summary>
        /// <param name="tableScope">An optional list of table names to limit the scope of the cache. If null or empty, all relationships in the DB are fetched.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task InitializeRelationshipCacheAsync(List<string> tableScope = null, CancellationToken cancellationToken = default)
        {
            
            if(_isCacheInitialized) return;

            _logger.LogInformation("Initializing relationship cache. Scope: {Scope}", tableScope == null || !tableScope.Any() ? "All Tables" : $"{tableScope.Count} tables");

            var fkColumnDetails = new List<dynamic>();
            var relations = new List<TableRelationship>();

            try
            {
                var queryBuilder = new StringBuilder(@"
                SELECT 
                    fk.name AS ForeignKeyName,
                    sch_from.name AS FromSchema,
                    tp.name AS FromTable,
                    cp.name AS FromColumn,
                    st_from.name AS FromColumnDataType,
                    sch_to.name AS ToSchema,
                    tr.name AS ToTable,
                    cr.name AS ToColumn,
                    st_to.name AS ToColumnDataType,
                    pk_to_col.is_primary_key AS ToColumnIsPartOfPK, 
                    pk_from_col.is_primary_key AS FromColumnIsPartOfPK 
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables AS tp ON fk.parent_object_id = tp.object_id
                INNER JOIN sys.schemas AS sch_from ON tp.schema_id = sch_from.schema_id
                INNER JOIN sys.columns AS cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                INNER JOIN sys.systypes st_from ON cp.user_type_id = st_from.xusertype
                INNER JOIN sys.tables AS tr ON fk.referenced_object_id = tr.object_id
                INNER JOIN sys.schemas AS sch_to ON tr.schema_id = sch_to.schema_id
                INNER JOIN sys.columns AS cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
                INNER JOIN sys.systypes st_to ON cr.user_type_id = st_to.xusertype
                LEFT JOIN (SELECT ic.object_id, ic.column_id, i.is_primary_key FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) AS pk_to_col ON cr.object_id = pk_to_col.object_id AND cr.column_id = pk_to_col.column_id
                LEFT JOIN (SELECT ic.object_id, ic.column_id, i.is_primary_key FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) AS pk_from_col ON cp.object_id = pk_from_col.object_id AND cp.column_id = pk_from_col.column_id
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    if (tableScope != null && tableScope.Any())
                    {
                        var paramNames = new List<string>();
                        for (int i = 0; i < tableScope.Count; i++)
                        {
                            var paramName = $"@Table{i}";
                            paramNames.Add(paramName);
                            cmd.Parameters.AddWithValue(paramName, tableScope[i]);
                        }
                        // This gets all relationships where AT LEAST ONE of the tables is in the scope.
                        queryBuilder.AppendLine($"WHERE (sch_from.name + '.' + tp.name) IN ({string.Join(",", paramNames)}) OR (sch_to.name + '.' + tr.name) IN ({string.Join(",", paramNames)})");
                    }

                    queryBuilder.AppendLine("ORDER BY ForeignKeyName, fkc.constraint_column_id;");
                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            fkColumnDetails.Add(new
                            {
                                ForeignKeyName = reader["ForeignKeyName"].ToString() ?? string.Empty,
                                FromSchema = reader["FromSchema"].ToString() ?? string.Empty,
                                FromTable = reader["FromTable"].ToString() ?? string.Empty,
                                FromColumn = reader["FromColumn"].ToString() ?? string.Empty,
                                FromColumnDataType = reader["FromColumnDataType"].ToString() ?? "unknown",
                                ToSchema = reader["ToSchema"].ToString() ?? string.Empty,
                                ToTable = reader["ToTable"].ToString() ?? string.Empty,
                                ToColumn = reader["ToColumn"].ToString() ?? string.Empty,
                                ToColumnDataType = reader["ToColumnDataType"].ToString() ?? "unknown",
                                ToColumnIsPartOfPK = reader["ToColumnIsPartOfPK"] != DBNull.Value && (bool)reader["ToColumnIsPartOfPK"],
                                FromColumnIsPartOfPK = reader["FromColumnIsPartOfPK"] != DBNull.Value && (bool)reader["FromColumnIsPartOfPK"]
                            });
                        }
                    }

                    // Group by ForeignKeyName to handle composite keys
                    var groupedByFkName = fkColumnDetails.GroupBy(c => c.ForeignKeyName);

                    foreach (var group in groupedByFkName)
                    {
                        var firstColInGroup = group.First();
                        string fromSchemaTable = $"{firstColInGroup.FromSchema}.{firstColInGroup.FromTable}";
                        string toSchemaTable = $"{firstColInGroup.ToSchema}.{firstColInGroup.ToTable}";
                        var keyColumnPairs = group.Select(c => new KeyColumnPair(c.FromColumn, c.ToColumn)).ToList();
                        bool allToColumnsArePKParts = group.All(c => c.ToColumnIsPartOfPK);
                        bool allFromColumnsArePKParts = group.All(c => c.FromColumnIsPartOfPK);
                        int fromTablePKColumnCount = GetPrimaryKeyColumnCount(firstColInGroup.FromSchema, firstColInGroup.FromTable, conn);
                        var relationType = allFromColumnsArePKParts && allToColumnsArePKParts && group.Count() == fromTablePKColumnCount && fromTablePKColumnCount > 0
                                              ? RelationshipType.OneToOne
                                              : RelationshipType.OneToMany;

                        relations.Add(new TableRelationship
                        {
                            ForeignKeyName = group.Key,
                            FromTable = fromSchemaTable,
                            ToTable = toSchemaTable,
                            KeyColumns = keyColumnPairs,
                            RelationType = relationType
                        });
                    }
                }

                // If a scope was provided, the SQL query has already fetched all relationships where at least
                // one of the tables is in the scope. We do not filter this further, because we need to
                // identify foreign keys from a scoped table to a potentially un-scoped table.
                // The previous, more restrictive filtering has been removed.
                
                lock (_cacheLock)
                {
                    _relationshipsCache = relations;
                    _isCacheInitialized = true;
                }
                _logger.LogInformation("Relationship cache successfully initialized with {Count} relationships.", relations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize relationship cache.");
                lock (_cacheLock)
                {
                    _relationshipsCache = new List<TableRelationship>();
                    _isCacheInitialized = true; // Mark as initialized to avoid retries
                }
            }
        }

        /// <summary>
        /// Proactively fetches and caches database relationships for all tables within a specific schema.
        /// This method should be called once when the application initializes.
        /// </summary>
        /// <param name="schema">The database schema to scope the cache to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task InitializeRelationshipCacheForSchemaAsync(string schema, CancellationToken cancellationToken = default)
        {
            if (_isCacheInitialized) return;

            if (string.IsNullOrWhiteSpace(schema))
            {
                throw new ArgumentException("Schema name cannot be null or empty.", nameof(schema));
            }

            _logger.LogInformation("Initializing relationship cache. Scope: Schema '{SchemaName}'", schema);

            var fkColumnDetails = new List<dynamic>();
            var relations = new List<TableRelationship>();

            try
            {
                var queryBuilder = new StringBuilder(@"
                SELECT 
                    fk.name AS ForeignKeyName,
                    sch_from.name AS FromSchema,
                    tp.name AS FromTable,
                    cp.name AS FromColumn,
                    st_from.name AS FromColumnDataType,
                    sch_to.name AS ToSchema,
                    tr.name AS ToTable,
                    cr.name AS ToColumn,
                    st_to.name AS ToColumnDataType,
                    pk_to_col.is_primary_key AS ToColumnIsPartOfPK, 
                    pk_from_col.is_primary_key AS FromColumnIsPartOfPK 
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables AS tp ON fk.parent_object_id = tp.object_id
                INNER JOIN sys.schemas AS sch_from ON tp.schema_id = sch_from.schema_id
                INNER JOIN sys.columns AS cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                INNER JOIN sys.systypes st_from ON cp.user_type_id = st_from.xusertype
                INNER JOIN sys.tables AS tr ON fk.referenced_object_id = tr.object_id
                INNER JOIN sys.schemas AS sch_to ON tr.schema_id = sch_to.schema_id
                INNER JOIN sys.columns AS cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
                INNER JOIN sys.systypes st_to ON cr.user_type_id = st_to.xusertype
                LEFT JOIN (SELECT ic.object_id, ic.column_id, i.is_primary_key FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) AS pk_to_col ON cr.object_id = pk_to_col.object_id AND cr.column_id = pk_to_col.column_id
                LEFT JOIN (SELECT ic.object_id, ic.column_id, i.is_primary_key FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) AS pk_from_col ON cp.object_id = pk_from_col.object_id AND cp.column_id = pk_from_col.column_id
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    // This WHERE clause efficiently fetches only relationships BETWEEN tables of the specified schema.
                    queryBuilder.AppendLine("WHERE sch_from.name = @Schema AND sch_to.name = @Schema");
                    cmd.Parameters.AddWithValue("@Schema", schema);

                    queryBuilder.AppendLine("ORDER BY ForeignKeyName, fkc.constraint_column_id;");
                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            fkColumnDetails.Add(new { ForeignKeyName = reader["ForeignKeyName"].ToString() ?? string.Empty, FromSchema = reader["FromSchema"].ToString() ?? string.Empty, FromTable = reader["FromTable"].ToString() ?? string.Empty, FromColumn = reader["FromColumn"].ToString() ?? string.Empty, FromColumnDataType = reader["FromColumnDataType"].ToString() ?? "unknown", ToSchema = reader["ToSchema"].ToString() ?? string.Empty, ToTable = reader["ToTable"].ToString() ?? string.Empty, ToColumn = reader["ToColumn"].ToString() ?? string.Empty, ToColumnDataType = reader["ToColumnDataType"].ToString() ?? "unknown", ToColumnIsPartOfPK = reader["ToColumnIsPartOfPK"] != DBNull.Value && (bool)reader["ToColumnIsPartOfPK"], FromColumnIsPartOfPK = reader["FromColumnIsPartOfPK"] != DBNull.Value && (bool)reader["FromColumnIsPartOfPK"] });
                        }
                    }

                    var groupedByFkName = fkColumnDetails.GroupBy(c => c.ForeignKeyName);

                    foreach (var group in groupedByFkName)
                    {
                        var firstColInGroup = group.First();
                        string fromSchemaTable = $"{firstColInGroup.FromSchema}.{firstColInGroup.FromTable}";
                        string toSchemaTable = $"{firstColInGroup.ToSchema}.{firstColInGroup.ToTable}";
                        var keyColumnPairs = group.Select(c => new KeyColumnPair(c.FromColumn, c.ToColumn)).ToList();
                        bool allToColumnsArePKParts = group.All(c => c.ToColumnIsPartOfPK);
                        bool allFromColumnsArePKParts = group.All(c => c.FromColumnIsPartOfPK);
                        int fromTablePKColumnCount = GetPrimaryKeyColumnCount(firstColInGroup.FromSchema, firstColInGroup.FromTable, conn);
                        var relationType = allFromColumnsArePKParts && allToColumnsArePKParts && group.Count() == fromTablePKColumnCount && fromTablePKColumnCount > 0
                                              ? RelationshipType.OneToOne
                                              : RelationshipType.OneToMany;
                        relations.Add(new TableRelationship { ForeignKeyName = group.Key, FromTable = fromSchemaTable, ToTable = toSchemaTable, KeyColumns = keyColumnPairs, RelationType = relationType });
                    }
                }

                lock (_cacheLock)
                {
                    _relationshipsCache = relations;
                    _isCacheInitialized = true;
                }
                _logger.LogInformation("Relationship cache successfully initialized for schema '{SchemaName}' with {Count} relationships.", schema, relations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize relationship cache for schema '{SchemaName}'.", schema);
                lock (_cacheLock) { _relationshipsCache = new List<TableRelationship>(); _isCacheInitialized = true; }
            }
        }

        /// <summary>
        /// Враќа листа на странски клучеви за дадена табела, користејќи го кешот.
        /// </summary>
        public Task<List<TableRelationship>> GetForeignKeyRelationshipsForTableAsync(string tableWithSchema)
        {
            if (!_isCacheInitialized)
            {
                _logger.LogWarning("GetForeignKeyRelationshipsForTableAsync called before relationship cache was initialized. Returning empty list.");
                return Task.FromResult(new List<TableRelationship>());
            }

            List<TableRelationship> allRelations;
            lock (_cacheLock)
            {
                allRelations = _relationshipsCache;
            }

            // Врати ги сите релации каде што дадената табела е "From" табела (има странски клуч)
            var relationships = allRelations
                .Where(r => r.FromTable.Equals(tableWithSchema, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult(relationships);
        }

     
        /// <summary>
        /// Враќа дефиниции за колоните кои се примарен клуч за дадена табела.
        /// </summary>
        public async Task<List<ColumnDefinition>> GetPrimaryKeyColumnsAsync(string tableWithSchema)
        {
           
            var allColumns = await GetColumnDefinitionsAsync(tableWithSchema);
            return allColumns.Where(c => c.KeyType.HasFlag(KeyType.Primary)).ToList();
        }

        public async Task<List<ColumnDefinition>> GetColumnDefinitionsAsync(
           string tableOrViewWithSchema,
           string extendedPropertyName = null, CancellationToken cancellationToken = default)
        {
            var columnDefinitions = new List<ColumnDefinition>();
            string objectType = null; // To store whether it's a TABLE or VIEW
            try
            {
                ParseTableWithSchema(tableOrViewWithSchema, out string schema, out string objectName);

                string query = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    c.name AS ColumnName,
    st.name AS DataType,
    c.max_length AS MaxLength, 
    c.precision AS Precision, 
    c.scale AS Scale,         
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    dc.definition AS DefaultValue,
    ep.value AS ColumnDescription,
    pk.is_primary_key AS IsPrimaryKey,
    ep_embed.value AS Embed
FROM sys.objects o
JOIN sys.schemas s ON o.schema_id = s.schema_id
JOIN sys.columns c ON c.object_id = o.object_id
JOIN sys.systypes st ON c.user_type_id = st.xusertype 
LEFT JOIN sys.extended_properties ep 
    ON ep.major_id = c.object_id 
    AND ep.minor_id = c.column_id 
    AND ep.name = @ExtendedPropertyName
LEFT JOIN sys.extended_properties ep_embed
    ON ep_embed.major_id = o.object_id
    AND ep_embed.minor_id = c.column_id
    AND ep_embed.name = 'Embed'
LEFT JOIN sys.default_constraints dc
    ON c.default_object_id = dc.object_id
LEFT JOIN (
    SELECT ic.object_id, ic.column_id, i.is_primary_key
    FROM sys.index_columns ic
    JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) AS pk ON o.object_id = pk.object_id AND c.column_id = pk.column_id
WHERE s.name = @SchemaName 
  AND o.name = @ObjectName
  AND o.type IN ('U', 'V')
  AND (
        (@ExtendedPropertyName IS NULL OR @ExtendedPropertyName = '')
        OR (ep.major_id IS NOT NULL)
  )
ORDER BY c.column_id;";

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SchemaName", schema);
                    cmd.Parameters.AddWithValue("@ObjectName", objectName);
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", (object)extendedPropertyName ?? DBNull.Value);

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            if (objectType == null)
                            {
                                objectType = reader["ObjectType"]?.ToString()?.Trim();
                            }

                            string typeName = reader["DataType"].ToString() ?? "unknown";
                            if (typeName.Contains("char") || typeName.Contains("binary"))
                            {
                                short maxLength = Convert.ToInt16(reader["MaxLength"]);
                                typeName += maxLength == -1 ? "(MAX)" : $"({maxLength})";
                            }
                            else if (typeName.Equals("decimal", StringComparison.OrdinalIgnoreCase) || typeName.Equals("numeric", StringComparison.OrdinalIgnoreCase))
                            {
                                byte precision = Convert.ToByte(reader["Precision"]);
                                byte scale = Convert.ToByte(reader["Scale"]);
                                typeName += $"({precision},{scale})";
                            }

                            bool embed = false;
                            var embedObj = reader["Embed"];
                            if (embedObj != DBNull.Value && embedObj != null)
                            {
                                if (embedObj is bool b) embed = b;
                                else if (bool.TryParse(embedObj.ToString(), out var parsed)) embed = parsed;
                                else if (embedObj is int i) embed = i != 0;
                            }

                            string descriptionTemplate = reader["ColumnDescription"] as string;
                            string description = descriptionTemplate;
                            string objectNameFromDb = reader["ObjectName"].ToString();
                            if (!string.IsNullOrWhiteSpace(descriptionTemplate) && descriptionTemplate.Contains("{0}"))
                            {
                                var sampleValues = await GetSampleColumnValuesAsync(
                                    reader["SchemaName"].ToString(),
                                    objectNameFromDb,
                                    reader["ColumnName"].ToString(),
                                    5
                                );
                                string commaSeparated = string.Join(", ", sampleValues);
                                description = string.Format(descriptionTemplate, commaSeparated);
                            }

                            columnDefinitions.Add(new ColumnDefinition
                            {
                                Id = GeneratorUtil.GenerateDeterministicUlongId($"{reader["SchemaName"]}.{objectNameFromDb}.{reader["ColumnName"]}", true),
                                FullQualifiedTableName = $"{reader["SchemaName"]}.{objectNameFromDb}",
                                Name = reader["ColumnName"].ToString(),
                                DescriptiveName = string.Empty,
                                Description = description,
                                Type = typeName,
                                IsNullable = (bool)reader["IsNullable"],
                                IsAutoIncrement = (bool)reader["IsIdentity"],
                                DefaultValue = reader["DefaultValue"] as string,
                                IsPrimaryKey = reader["IsPrimaryKey"] != DBNull.Value && (bool)reader["IsPrimaryKey"],
                                IsEnumLikeColumn = embed,
                                KeyType = KeyType.None
                            });
                        }
                    }
                }

                // If it's a table, process relationships and primary key details.
                // This logic is specific to tables and will not apply to views.
                if ("USER_TABLE".Equals(objectType, StringComparison.OrdinalIgnoreCase))
                {
                    // After retrieving the columns, integrate foreign key information.
                    try
                    {
                        var relationships = await GetEntityRelationshipDiagramAsync(tableOrViewWithSchema, null, cancellationToken);
                        foreach (var rel in relationships)
                        {
                            if (rel.FromTable.Equals(tableOrViewWithSchema, StringComparison.OrdinalIgnoreCase))
                            {
                                var fkCols = rel.KeyColumns.Select(c => c.FromColumn);
                                foreach (var fk in fkCols)
                                {
                                    var col = columnDefinitions.FirstOrDefault(c => c.Name.Equals(fk, StringComparison.OrdinalIgnoreCase));
                                    if (col != null)
                                    {
                                        col.KeyType |= KeyType.Foreign;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception relEx)
                    {
                        _logger.LogError(relEx, "Error retrieving or processing foreign key relationships for {TableWithSchema}", tableOrViewWithSchema);
                    }

                    // Process primary keys to detect composite keys.
                    var pkColumns = columnDefinitions.Where(c => c.IsPrimaryKey).ToList();
                    if (pkColumns.Count > 1)
                    {
                        foreach (var col in pkColumns)
                        {
                            col.KeyType |= KeyType.Primary | KeyType.Composite;
                        }
                    }
                    else if (pkColumns.Count == 1)
                    {
                        pkColumns.First().KeyType |= KeyType.Primary;
                    }
                }

                return columnDefinitions;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL Error in GetColumnDefinitionsAsync for {TableOrView}", tableOrViewWithSchema);
                return new List<ColumnDefinition>();
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Argument Error in GetColumnDefinitionsAsync for {TableOrView}", tableOrViewWithSchema);
                return new List<ColumnDefinition>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Error in GetColumnDefinitionsAsync for {TableOrView}", tableOrViewWithSchema);
                return new List<ColumnDefinition>();
            }
        }

        public async Task<List<TableRelationship>> GetEntityRelationshipDiagramAsync(string startTable, List<string> endTables = null, CancellationToken cancellationToken = default)
        {
            // Check if the cache is initialized. If not, initialize it on-the-fly.
            // This makes the method more robust, but for optimal performance,
            // it's still recommended to call InitializeRelationshipCacheAsync explicitly at startup.
            if (!_isCacheInitialized)
            {
                _logger.LogWarning("GetEntityRelationshipDiagramAsync was called before the relationship cache was initialized. " +
                                 "Initializing with a full database scope now. For better performance, call InitializeRelationshipCacheAsync explicitly at application startup.");
                // Initialize with the broadest scope (all tables).
                await InitializeRelationshipCacheAsync(new List<string>(), cancellationToken);
            }

            List<TableRelationship> allRelations;
            lock (_cacheLock)
            {
                allRelations = _relationshipsCache;
            }

            // If no endTables are specified, return all direct relationships for the startTable.
            if (endTables == null || !endTables.Any())
            {
                return allRelations.Where(r => r.FromTable.Equals(startTable, StringComparison.OrdinalIgnoreCase) ||
                                                r.ToTable.Equals(startTable, StringComparison.OrdinalIgnoreCase))
                                   .ToList();
            }

            // --- Shortest Path Finding Logic (BFS) ---
            // This part can be computationally intensive, so we run it on a background thread
            // to avoid blocking the async method's main thread, especially if the graph is large.
            return await Task.Run(() =>
            {
                var adjacencyList = new Dictionary<string, List<TableRelationship>>(StringComparer.OrdinalIgnoreCase);
                foreach (var rel in allRelations)
                {
                    if (!adjacencyList.ContainsKey(rel.FromTable)) adjacencyList[rel.FromTable] = new List<TableRelationship>();
                    adjacencyList[rel.FromTable].Add(rel);
                    if (!adjacencyList.ContainsKey(rel.ToTable)) adjacencyList[rel.ToTable] = new List<TableRelationship>();
                    adjacencyList[rel.ToTable].Add(rel);
                }

                var pathsFound = new HashSet<TableRelationship>();
                var endTablesToFind = new HashSet<string>(endTables, StringComparer.OrdinalIgnoreCase);

                foreach (var endTable in endTablesToFind)
                {
                    if (startTable.Equals(endTable, StringComparison.OrdinalIgnoreCase)) continue;

                    var queue = new Queue<(string currentTable, List<TableRelationship> path)>();
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startTable };

                    queue.Enqueue((startTable, new List<TableRelationship>()));

                    while (queue.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var (currentTable, path) = queue.Dequeue();

                        if (currentTable.Equals(endTable, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var p in path) pathsFound.Add(p);
                            break; // Found the shortest path to this endTable, move to the next.
                        }

                        if (!adjacencyList.ContainsKey(currentTable)) continue;

                        foreach (var relation in adjacencyList[currentTable])
                        {
                            string neighborTable = relation.FromTable.Equals(currentTable, StringComparison.OrdinalIgnoreCase)
                                                   ? relation.ToTable
                                                   : relation.FromTable;

                            if (!visited.Contains(neighborTable))
                            {
                                visited.Add(neighborTable);
                                var newPath = new List<TableRelationship>(path) { relation };
                                queue.Enqueue((neighborTable, newPath));
                            }
                        }
                    }
                }

                return pathsFound.ToList();
            }, cancellationToken);
        }

        private int GetPrimaryKeyColumnCount(string schema, string tableName, SqlConnection conn)
        {
            // This helper assumes the connection 'conn' is already open and managed by the caller.
            string pkColCountQuery = @"
                SELECT COUNT(ic.column_id)
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.tables AS t ON i.object_id = t.object_id
                INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id
                WHERE i.is_primary_key = 1 AND s.name = @Schema AND t.name = @Table";
            using (var cmd = new SqlCommand(pkColCountQuery, conn)) // Reuses the existing open connection
            {
                cmd.Parameters.AddWithValue("@Schema", schema);
                cmd.Parameters.AddWithValue("@Table", tableName);
                object result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
        }

        public async Task<List<Dictionary<string, object>>> GetSampleDataAsync(
             string tableWithSchema,
             List<string> selectedColumns, // Now non-nullable
             int rowCount = 5,
             CancellationToken cancellationToken = default)
        {
            var sampleData = new List<Dictionary<string, object>>();
            if (selectedColumns == null || !selectedColumns.Any())
            {
                // If no columns are selected, it's ambiguous. Either throw or return empty.
                _logger.LogWarning("GetSampleDataAsync called with no selected columns for {TableWithSchema}.", tableWithSchema);
                return sampleData; // Or throw new ArgumentException("selectedColumns cannot be null or empty.");
            }

            try
            {
                ParseTableWithSchema(tableWithSchema, out string schema, out string tableName);

                // Sanitize column names
                var sanitizedColumns = selectedColumns
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c) && IsValidSqlIdentifier(c)) // Use a stricter validator
                    .Select(c => $"[{c.Replace("]", "]]")}]")
                    .ToList();

                if (!sanitizedColumns.Any())
                {
                    _logger.LogWarning("GetSampleDataAsync: No valid columns after sanitization for {TableWithSchema}. Original: {OriginalColumns}", tableWithSchema, string.Join(",", selectedColumns));
                    return sampleData; // Or throw
                }
                string columnsToSelect = string.Join(", ", sanitizedColumns);
                string whereClause = string.Join(" AND ", sanitizedColumns.Select(c => $"{c} IS NOT NULL"));

                string query;
                if (rowCount == int.MaxValue) // Special value to fetch all rows
                {
                    query = $"SELECT {columnsToSelect} FROM [{schema}].[{tableName}] WHERE {whereClause};";
                }
                else if (rowCount > 0)
                {
                    query = $"SELECT TOP (@RowCount) {columnsToSelect} FROM [{schema}].[{tableName}] WHERE {whereClause};";
                }
                else // rowCount <= 0 and not int.MaxValue means no rows or default.
                {
                    _logger.LogInformation("GetSampleDataAsync called with rowCount <= 0 for {TableWithSchema}. Returning empty data.", tableWithSchema);
                    return sampleData; // No rows requested
                }


                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        if (rowCount > 0 && rowCount != int.MaxValue)
                        {
                            cmd.Parameters.AddWithValue("@RowCount", rowCount);
                        }

                        await conn.OpenAsync(cancellationToken);
                        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                        {
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                sampleData.Add(row);
                            }
                        }
                    }
                }
                return sampleData;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL Error in GetSampleDataAsync for {TableWithSchema}", tableWithSchema);
                return new List<Dictionary<string, object>>();
            }
            catch (ArgumentException ex) // Catches from ParseTableWithSchema or sanitization issues
            {
                _logger.LogError(ex, "Argument Error in GetSampleDataAsync for {TableWithSchema}", tableWithSchema);
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Error in GetSampleDataAsync for {TableWithSchema}", tableWithSchema);
                return new List<Dictionary<string, object>>();
            }
        }


        private bool IsValidSqlIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return false;
            // Basic check: ensures it doesn't contain obvious SQL injection characters for column/table names.
            // SQL Server identifiers can be complex (e.g. with spaces if bracketed).
            // This check is a safeguard for dynamic query building parts. Bracketing is the main defense.
            return !identifier.Any(ch => ch == ';' || ch == '\'' || ch == '-' && identifier.Contains("--"));
        }


        // filepath: [MSSQLDataSchemaService.cs](http://_vscodecontentref_/1)
        // ...existing code...
        public async Task<List<string>> GetTableNamesAsync(List<string> tables = null, CancellationToken cancellationToken = default)
        {
            var tableNames = new List<string>();
            try
            {
                string query;
                if (tables != null && tables.Any())
                {
                    // Prepare parameterized query for specific tables
                    var paramNames = tables.Select((t, i) => $"@Table{i}").ToList();
                    query = $@"
                        SELECT s.name + '.' + t.name AS FullTableName
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE (s.name + '.' + t.name) IN ({string.Join(",", paramNames)})
                        ORDER BY s.name, t.name;";
                }
                else
                {
                    query = @"
                        SELECT s.name + '.' + t.name AS FullTableName
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        ORDER BY s.name, t.name;";
                }

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    if (tables != null && tables.Any())
                    {
                        for (int i = 0; i < tables.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@Table{i}", tables[i]);
                        }
                    }
                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            tableNames.Add(reader["FullTableName"].ToString() ?? string.Empty);
                        }
                    }
                }
                return tableNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTableNamesAsync");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetRelevantTableNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var tableNames = new List<string>();
            if (schemas == null || !schemas.Any() || string.IsNullOrWhiteSpace(extendedPropertyName))
            {
                _logger.LogWarning("GetRelevantTableNamesAsync called with no schemas or no extended property name.");
                return tableNames;
            }

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT
                        s.name + '.' + t.name AS FullTableName
                    FROM
                        sys.tables t
                    JOIN
                        sys.schemas s ON t.schema_id = s.schema_id
                    JOIN
                        sys.extended_properties ep ON ep.major_id = t.object_id
                    WHERE
                        ep.minor_id = 0 -- Specifies the object itself (the table)
                        AND ep.name = @ExtendedPropertyName
                        AND ep.value IS NOT NULL
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", extendedPropertyName);

                    var paramNames = new List<string>();
                    int i = 0;
                    foreach (var schema in schemas)
                    {
                        var paramName = $"@Schema{i++}";
                        paramNames.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, schema);
                    }
                    queryBuilder.AppendLine($"AND s.name IN ({string.Join(",", paramNames)})");
                    queryBuilder.AppendLine("ORDER BY s.name, t.name;");

                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            tableNames.Add(reader["FullTableName"].ToString() ?? string.Empty);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRelevantTableNamesAsync");
            }
            return tableNames;
        }

        public async Task<List<string>> GetRelevantViewNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var viewNames = new List<string>();
            if (schemas == null || !schemas.Any() || string.IsNullOrWhiteSpace(extendedPropertyName))
            {
                _logger.LogWarning("GetRelevantViewNamesAsync called with no schemas or no extended property name.");
                return viewNames;
            }

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT
                        s.name + '.' + v.name AS FullViewName
                    FROM
                        sys.views v
                    JOIN
                        sys.schemas s ON v.schema_id = s.schema_id
                    JOIN
                        sys.extended_properties ep ON ep.major_id = v.object_id
                    WHERE
                        ep.minor_id = 0 -- Specifies the object itself (the view)
                        AND ep.name = @ExtendedPropertyName
                        AND ep.value IS NOT NULL
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", extendedPropertyName);

                    var paramNames = new List<string>();
                    int i = 0;
                    foreach (var schema in schemas)
                    {
                        var paramName = $"@Schema{i++}";
                        paramNames.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, schema);
                    }
                    queryBuilder.AppendLine($"AND s.name IN ({string.Join(",", paramNames)})");
                    queryBuilder.AppendLine("ORDER BY s.name, v.name;");

                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            viewNames.Add(reader["FullViewName"].ToString() ?? string.Empty);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRelevantViewNamesAsync");
            }
            return viewNames;
        }
        // ...existing code...


        /// <summary>
        /// Retrieves the row count for a specific table in the format "schema.tableName".
        /// </summary>
        /// <param name="tableWithSchema"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<long> GetTableRowCountAsync(string tableWithSchema, CancellationToken cancellationToken = default)
        {
            try
            {
                ParseTableWithSchema(tableWithSchema, out string schema, out string tableName);
                string query = $"SELECT COUNT(*) FROM [{schema}].[{tableName}];";
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync(cancellationToken);
                    var result = await cmd.ExecuteScalarAsync(cancellationToken);
                    return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTableRowCountAsync for {Table}", tableWithSchema);
                return 0;
            }
        }


        /// <summary>
        /// Retrieves a sample of values from a specific column in a table.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private async Task<List<string>> GetSampleColumnValuesAsync(string schema, string tableName, string columnName, int count = 5)
        {
            var values = new List<string>();
            string query = $"SELECT TOP (@Count) [{columnName.Replace("]", "]]")}] FROM [{schema}].[{tableName}] WHERE [{columnName.Replace("]", "]]")}] IS NOT NULL;";
            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Count", count);
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var val = reader.IsDBNull(0) ? null : reader.GetValue(0)?.ToString();
                        if (!string.IsNullOrWhiteSpace(val))
                            values.Add(val);
                    }
                }
            }
            return values;
        }


        /// Retrieves value definitions for a specific column in a table.
        public async Task<List<ValueDefinition>> GetValueDefinitionsAsync(
        ColumnDefinition columnDefinition,
        CancellationToken cancellationToken = default)
        {
            if (columnDefinition == null)
                throw new ArgumentNullException(nameof(columnDefinition));

            if (!columnDefinition.IsEnumLikeColumn)
            {
                throw new Exception("Column is not marked for embedding of its values.");
            }

            var valueDefinitions = new List<ValueDefinition>();

            try
            {
                // Parse schema and table from FullQualifiedTableName
                ParseTableWithSchema(columnDefinition.FullQualifiedTableName, out string schema, out string tableName);
                string columnName = columnDefinition.Name;

                if (!IsValidSqlIdentifier(columnName))
                    throw new ArgumentException($"Invalid column name: {columnName}");

                //we expect unque desrition text for unque codes/options/enums used in keys
                string query = $"SELECT [{columnName.Replace("]", "]]")}] FROM [{schema}].[{tableName}];";

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var val = reader.IsDBNull(0) ? null : reader.GetValue(0);
                            if (val == null) continue;


                            var description = val.ToString() ?? string.Empty;


                            valueDefinitions.Add(new ValueDefinition
                            {
                                SourceColumnFullQualifiedName = columnName,
                                SourceColumnType = columnDefinition.Type,
                                ValueStringified = description,
                                Tags = new List<string> { columnDefinition.FullQualifiedTableName, columnName },
                                IsNullable = columnDefinition.IsNullable
                            });
                        }
                    }
                }
                return valueDefinitions;
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"SQL Error in GetValueDefinitionsAsync for {columnDefinition.FullQualifiedTableName}.{columnDefinition.Name}: {ex.Message}");
                return new List<ValueDefinition>();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Argument Error in GetValueDefinitionsAsync: {ex.Message}");
                return new List<ValueDefinition>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error in GetValueDefinitionsAsync for {columnDefinition.FullQualifiedTableName}.{columnDefinition.Name}: {ex.Message}");
                return new List<ValueDefinition>();
            }
        }

        /// <summary>
        /// UPDATED: Detects the most likely root table using an improved scoring heuristic.
        /// The score considers outgoing relationships as primary indicators, and table row count as a secondary factor.
        /// Score = (Outgoing_FKs * 5) + (Incoming_FKs) + log10(RowCount)
        /// dbo.forumThreadsAI:
        ///        Скор: (4 излезни* 5) + 0 влезни + log10(5000 редови) ≈ 20 + 0 + 3.7 = 23.7
        /// dbo.StatusThreadAI:
        /// Скор: (0 излезни* 5) + 1 влезна + log10(5 редови) ≈ 0 + 1 + 0.7 = 1.7
        /// </summary>
        public async Task<string> DetectRootTableAsync(List<string> tableScope = null, CancellationToken cancellationToken = default)
        {
            if (!_isCacheInitialized)
            {
                // Initialize for the entire schema if not already done.
                await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            }

            var relationships = _relationshipsCache;

            // Determine the full set of tables to consider, either from the scope or all tables in relationships.
            HashSet<string> tablesToConsider;
            if (tableScope != null && tableScope.Any())
            {
                tablesToConsider = new HashSet<string>(tableScope, StringComparer.OrdinalIgnoreCase);
                // Filter relationships to only those where both tables are in the scope.
                relationships = relationships
                    .Where(r => tablesToConsider.Contains(r.FromTable) && tablesToConsider.Contains(r.ToTable))
                    .ToList();
            }
            else
            {
                // If no scope, consider all tables involved in any relationship.
                tablesToConsider = relationships
                    .SelectMany(r => new[] { r.FromTable, r.ToTable })
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (!tablesToConsider.Any())
            {
                _logger.LogWarning("No tables found in the given scope to detect a root table.");
                return null;
            }

            // Use a dictionary to store detailed scores for each table.
            var tableScores = new Dictionary<string, (int outgoing, int incoming, long rowCount)>(StringComparer.OrdinalIgnoreCase);

            // Initialize scores for all tables in scope.
            foreach (var table in tablesToConsider)
            {
                tableScores[table] = (0, 0, 0);
            }

            // Calculate incoming and outgoing relationship counts.
            foreach (var rel in relationships)
            {
                if (tableScores.ContainsKey(rel.ToTable))
                {
                    var score = tableScores[rel.ToTable];
                    score.incoming++;
                    tableScores[rel.ToTable] = score;
                }
                if (tableScores.ContainsKey(rel.FromTable))
                {
                    var score = tableScores[rel.FromTable];
                    score.outgoing++;
                    tableScores[rel.FromTable] = score;
                }
            }

            // Fetch row counts for all considered tables. This can be done in parallel.
            var rowCountTasks = tablesToConsider.Select(async table => new
            {
                TableName = table,
                Count = await GetTableRowCountAsync(table, cancellationToken)
            });
            var rowCounts = await Task.WhenAll(rowCountTasks);

            // Update scores with row counts.
            foreach (var rowCountInfo in rowCounts)
            {
                if (tableScores.ContainsKey(rowCountInfo.TableName))
                {
                    var score = tableScores[rowCountInfo.TableName];
                    score.rowCount = rowCountInfo.Count;
                    tableScores[rowCountInfo.TableName] = score;
                }
            }

            // Calculate the final heuristic score and find the best candidate.
            var finalScores = tableScores.Select(kvp => {
                var (outgoing, incoming, rowCount) = kvp.Value;
                // The heuristic:
                // - Outgoing FKs are very important (weight 5).
                // - Incoming FKs are less important (weight 1).
                // - Row count is a tie-breaker, using log10 to prevent huge tables from dominating.
                //   Add 1 to rowCount to avoid log10(0).
                double finalScore = outgoing * 5 + incoming + Math.Log10(rowCount + 1);

                _logger.LogDebug("Scoring for table '{Table}': Out={out}, In={in}, Rows={rows} -> Final Score={score}",
                    kvp.Key, outgoing, incoming, rowCount, finalScore);

                return new { TableName = kvp.Key, Score = finalScore };
            });

            if (!finalScores.Any())
            {
                _logger.LogWarning("Could not calculate final scores for any table.");
                // Fallback to the first table in the original scope if available
                return tablesToConsider.FirstOrDefault();
            }

            // Find the table with the highest final score.
            var bestCandidate = finalScores.OrderByDescending(s => s.Score).First();

            _logger.LogInformation("Detected root table: {TableName} with a score of {Score:F2}", bestCandidate.TableName, bestCandidate.Score);

            return bestCandidate.TableName;
        }


        /// <summary>
        /// NEW & IMPROVED: Generates an ontological JSON for a specified scope of tables.
        /// This method now handles disconnected graphs by creating multiple root-level objects in the JSON if necessary.
        /// This is the primary public method to use.
        /// </summary>
        /// <param name="tableScope">A list of fully qualified table names (e.g., "dbo.Orders") to include in the ontology. If null or empty, the entire database schema might be used.</param>
        /// <param name="rootTableName">Optional. The preferred starting table for the JSON hierarchy. If null, a root table will be automatically detected within the scope.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A formatted JSON string representing the database ontology.</returns>
        public async Task<string> GenerateTableOntologyAsJsonAsync(
            List<string> tableScope,
            string rootTableName = null,
            CancellationToken cancellationToken = default)
        {
            // 1. Иницијализација на кешот за релации, ако веќе не е иницијализиран.
            if (!_isCacheInitialized)
            {
                _logger.LogInformation("Relationship cache not initialized. Initializing for the entire database.");
                await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            }

            // 2. Филтрирање на релациите според дадениот опсег на табели (tableScope).
            var relationshipsInScope = _relationshipsCache;
            var tablesToProcess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (tableScope != null && tableScope.Any())
            {
                var scopeSet = new HashSet<string>(tableScope, StringComparer.OrdinalIgnoreCase);
                relationshipsInScope = _relationshipsCache
                    .Where(r => scopeSet.Contains(r.FromTable) && scopeSet.Contains(r.ToTable))
                    .ToList();
                tablesToProcess.UnionWith(tableScope);
                _logger.LogInformation("Ontology generation scoped to {Count} tables, resulting in {RelCount} relationships.", scopeSet.Count, relationshipsInScope.Count);
            }
            else
            {
                // Ако нема опсег, ги земаме сите табели од кешот.
                tablesToProcess.UnionWith(relationshipsInScope.Select(r => r.FromTable));
                tablesToProcess.UnionWith(relationshipsInScope.Select(r => r.ToTable));
                _logger.LogInformation("No table scope provided. Using all {RelCount} relationships from the cache.", relationshipsInScope.Count);
            }

            if (!tablesToProcess.Any())
            {
                _logger.LogWarning("No tables to process for ontology generation.");
                return "{}";
            }

            // 3. Главна логика за градење на онтологијата
            var ontology = new Dictionary<string, object>();
            var visitedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 3.1. Одредување на редослед на обработка. Почнуваме од главниот корен ако е дефиниран/детектиран.
            string primaryRoot = rootTableName;
            if (string.IsNullOrWhiteSpace(primaryRoot))
            {
                _logger.LogInformation("Root table not specified. Attempting to auto-detect...");
                primaryRoot = await DetectRootTableAsync(tablesToProcess.ToList(), cancellationToken);
            }

            var processingOrder = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryRoot) && tablesToProcess.Contains(primaryRoot))
            {
                processingOrder.Add(primaryRoot);
            }
            // Ги додаваме и останатите табели, за да се осигураме дека сите се обработени.
            processingOrder.AddRange(tablesToProcess.Where(t => !t.Equals(primaryRoot, StringComparison.OrdinalIgnoreCase)));

            // 4. Итерација низ сите табели за обработка, градејќи стебло за секој „непосетен“ корен.
            foreach (var currentTable in processingOrder)
            {
                // Ако табелата е веќе посетена како дел од друго стебло, прескокни ја.
                if (visitedTables.Contains(currentTable))
                {
                    continue;
                }

                // Оваа табела е нов корен (или главниот, или корен на изолиран остров).
                _logger.LogInformation("Found new ontology root: {TableName}. Building its tree.", currentTable);
                var rootNodeObject = new Dictionary<string, object>();

                // Fetch and add the table's own description
                var tableDef = await GetTableDefinitionsAsync(new List<string> { currentTable }, _settings.TableDescriptionProperty, cancellationToken);
                if (tableDef != null && tableDef.Any() && !string.IsNullOrWhiteSpace(tableDef.First().Description))
                {
                    rootNodeObject["description"] = tableDef.First().Description;
                }

                ontology[currentTable] = rootNodeObject; // Додај го на главно ниво во JSON-от

                // Започни го рекурзивното градење од овој нов корен.
                await BuildOntologyNodeRecursiveAsync(
                    currentTable,
                    rootNodeObject,
                    visitedTables, // Го проследуваме истиот сет за да се ажурира
                    relationshipsInScope,
                    cancellationToken);
            }

            // 5. Серијализација на финалниот објект
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(ontology, jsonOptions);
        }

        public async Task<string> GenerateTableOntologyAsJsonAsync(
            List<object> tables,
            string rootTableName = null,
            CancellationToken cancellationToken = default)
        {
            if (tables == null || !tables.Any())
            {
                _logger.LogWarning("GenerateTableOntologyAsJsonAsync called with no tables.");
                return string.Empty;
            }

            // 1. Extract table info and create a scope and a description map
            var tableInfoList = tables.OfType<Dictionary<string, object>>()
                .Select(dict => new
                {
                    Name = dict.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : throw new ArgumentException("Missing key 'name' in table dictionary"),
                    Description = dict.TryGetValue("description", out var descObj) ? descObj?.ToString() : null
                })
                .Where(t => !string.IsNullOrEmpty(t.Name))
                .ToList();

            var tableScope = tableInfoList.Select(t => t.Name).ToList();
            var descriptionMap = tableInfoList.ToDictionary(t => t.Name, t => t.Description, StringComparer.OrdinalIgnoreCase);

            // 2. Initialize relationship cache if not already done
            if (!_isCacheInitialized)
            {
                _logger.LogInformation("Relationship cache not initialized. Initializing for the entire database.");
                await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            }

            // 3. Filter relationships based on the provided table scope
            var scopeSet = new HashSet<string>(tableScope, StringComparer.OrdinalIgnoreCase);
            var relationshipsInScope = _relationshipsCache
                .Where(r => scopeSet.Contains(r.FromTable) && scopeSet.Contains(r.ToTable))
                .ToList();
            _logger.LogInformation("Ontology generation scoped to {Count} tables, resulting in {RelCount} relationships.", scopeSet.Count, relationshipsInScope.Count);

            // 4. Build the ontology, handling disconnected graphs
            var ontology = new Dictionary<string, object>();
            var visitedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Determine processing order, starting with the primary root if specified/detected
            string primaryRoot = rootTableName;
            if (string.IsNullOrWhiteSpace(primaryRoot))
            {
                _logger.LogInformation("Root table not specified. Attempting to auto-detect...");
                primaryRoot = await DetectRootTableAsync(tableScope, cancellationToken);
            }

            var processingOrder = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryRoot) && scopeSet.Contains(primaryRoot))
            {
                processingOrder.Add(primaryRoot);
            }
            processingOrder.AddRange(scopeSet.Where(t => !t.Equals(primaryRoot, StringComparison.OrdinalIgnoreCase)));

            // 5. Iterate and build the ontology tree(s)
            foreach (var currentTable in processingOrder)
            {
                if (visitedTables.Contains(currentTable)) continue;

                _logger.LogInformation("Found new ontology root: {TableName}. Building its tree.", currentTable);
                var rootNodeObject = new Dictionary<string, object>();
                ontology[currentTable] = rootNodeObject;

                // Add the table's own description from the pre-fetched map
                if (descriptionMap.TryGetValue(currentTable, out var description) && !string.IsNullOrWhiteSpace(description))
                {
                    rootNodeObject["description"] = description;
                }

                await BuildOntologyNodeRecursiveAsync(currentTable, rootNodeObject, visitedTables, relationshipsInScope, cancellationToken);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(ontology, jsonOptions);
        }

        public async Task<string> GenerateViewOntologyAsJsonAsync(List<object> views, CancellationToken cancellationToken = default)
        {
            if (views == null || !views.Any())
            {
                return string.Empty;
            }

            var ontology = new Dictionary<string, object>();

            // Extract view information from the input list of objects
            var viewInfoList = views.OfType<Dictionary<string, object>>()
                .Select(dict => new
                {
                    Name = dict.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null,
                    Description = dict.TryGetValue("description", out var descObj) ? descObj?.ToString() : null
                })
                .Where(v => !string.IsNullOrEmpty(v.Name))
                .ToList();

            if (!viewInfoList.Any()) return "{}";

            foreach (var viewInfo in viewInfoList)
            {
                var viewNodeObject = new Dictionary<string, object>();

                // Add the view's own description at the top level of its node
                if (!string.IsNullOrWhiteSpace(viewInfo.Description))
                {
                    viewNodeObject["description"] = viewInfo.Description;
                }

                var attributesNode = new Dictionary<string, object>();
                var columns = await GetColumnDefinitionsAsync(viewInfo.Name, _settings.ColumnDescriptionProperty, cancellationToken);

                foreach (var column in columns.OrderBy(c => c.Name)) // Sort for consistent output
                {
                    if (string.IsNullOrWhiteSpace(column.Description)) continue;

                    var columnObject = new Dictionary<string, object>
                    {
                        ["description"] = column.Description
                    };

                    if (column.IsEnumLikeColumn)
                    {
                        var sampleDataResult = await GetSampleDataAsync(viewInfo.Name, new List<string> { column.Name }, 50, cancellationToken);
                        var values = sampleDataResult
                            .Select(row => row.TryGetValue(column.Name, out var val) && val != null ? val.ToString() : null)
                            .Where(v => v != null).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
                        if (values.Any())
                        {
                            columnObject["sampleData"] = values;
                        }
                    }

                    string fullPathKey = $"{column.FullQualifiedTableName}.{column.Name}";
                    attributesNode[fullPathKey] = columnObject;
                }

                if (attributesNode.Any())
                {
                    viewNodeObject["attributes"] = attributesNode;
                }

                if (viewNodeObject.Any())
                {
                    ontology[viewInfo.Name] = viewNodeObject;
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(ontology, jsonOptions);
        }

        public async Task<string> GenerateViewOntologyAsJsonAsync(List<string> viewNames, CancellationToken cancellationToken = default)
        {
            if (viewNames == null || !viewNames.Any())
            {
                return "{}";
            }

            var ontology = new Dictionary<string, object>();

            // Get definitions for all views at once
            var viewDefinitions = await GetViewDefinitionsAsync(viewNames, _settings.ViewDescriptionProperty, cancellationToken);
            var viewDefinitionsMap = viewDefinitions.ToDictionary(v => v.FullQualifiedViewName, v => v, StringComparer.OrdinalIgnoreCase);

            foreach (var viewName in viewNames)
            {
                var viewNodeObject = new Dictionary<string, object>();

                // Add the view's own description at the top level of its node
                if (viewDefinitionsMap.TryGetValue(viewName, out var viewDef) && !string.IsNullOrWhiteSpace(viewDef.Description))
                {
                    viewNodeObject["description"] = viewDef.Description;
                }

                var attributesNode = new Dictionary<string, object>();
                var columns = await GetColumnDefinitionsAsync(viewName, _settings.ColumnDescriptionProperty, cancellationToken);

                foreach (var column in columns.OrderBy(c => c.Name)) // Sort for consistent output
                {
                    if (string.IsNullOrWhiteSpace(column.Description)) continue;

                    var columnObject = new Dictionary<string, object>
                    {
                        ["description"] = column.Description
                    };

                    if (column.IsEnumLikeColumn)
                    {
                        var sampleDataResult = await GetSampleDataAsync(viewName, new List<string> { column.Name }, 50, cancellationToken);
                        var values = sampleDataResult
                            .Select(row => row.TryGetValue(column.Name, out var val) && val != null ? val.ToString() : null)
                            .Where(v => v != null).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
                        if (values.Any())
                        {
                            columnObject["sampleData"] = values;
                        }
                    }

                    string fullPathKey = $"{column.FullQualifiedTableName}.{column.Name}";
                    attributesNode[fullPathKey] = columnObject;
                }

                if (attributesNode.Any())
                {
                    viewNodeObject["attributes"] = attributesNode;
                }

                if (viewNodeObject.Any())
                {
                    ontology[viewName] = viewNodeObject;
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(ontology, jsonOptions);
        }


        // Овој метод е приватен помошен метод и не треба да се повикува директно.
        public async Task<string> GenerateOntologyFromRelationshipsAsync(
            string rootTableName,
            IReadOnlyList<TableRelationship> relationships,
            CancellationToken cancellationToken)
        {
            // Овој метод сега е застарен бидејќи логиката е преместена во GenerateTableOntologyAsJsonAsync
            // Може да се избрише или да се остави како `private` ако има друга употреба.
            // За едноставност, ќе го оставиме но ќе го означиме дека не е за главна употреба.
            _logger.LogWarning("GenerateOntologyFromRelationshipsAsync is deprecated. Use GenerateOntologyAsJsonAsync instead.");

            var rootOntology = new Dictionary<string, object>();
            var rootTableObject = new Dictionary<string, object>();
            rootOntology[rootTableName] = rootTableObject;

            var visitedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await BuildOntologyNodeRecursiveAsync(
                rootTableName,
                rootTableObject,
                visitedTables,
                relationships,
                cancellationToken);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(rootOntology, jsonOptions);
        }

        /// <summary>
        /// - Uses full path as the key for columns.
        /// - Removes redundant inner 'fullpathshematablecolumn' property.
        /// - FIXED: Recursive call was mistyped.
        /// </summary>
        private async Task BuildOntologyNodeRecursiveAsync(
            string currentTableName,
            Dictionary<string, object> parentNode,
            HashSet<string> visitedTables,
            IReadOnlyList<TableRelationship> allRelationships,
            CancellationToken cancellationToken)
        {
            if (visitedTables.Contains(currentTableName)) return;
            visitedTables.Add(currentTableName); // Означи ја тековната табела како посетена

            var columns = await GetColumnDefinitionsAsync(currentTableName, _settings.ColumnDescriptionProperty, cancellationToken);
            var outgoingRelationships = allRelationships
                .Where(r => string.Equals(r.FromTable, currentTableName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var fkColumnNames = outgoingRelationships.SelectMany(r => r.KeyColumns.Select(kc => kc.FromColumn))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Process REGULAR columns (not foreign keys)
            foreach (var column in columns)
            {
                if (fkColumnNames.Contains(column.Name) || string.IsNullOrWhiteSpace(column.Description)) continue;

                var columnObject = new Dictionary<string, object>
                {
                    ["description"] = column.Description
                };

                if (column.IsEnumLikeColumn)
                {
                    var sampleDataResult = await GetSampleDataAsync(currentTableName, new List<string> { column.Name }, 50, cancellationToken);
                    var values = sampleDataResult
                        .Select(row => row.TryGetValue(column.Name, out var val) && val != null ? val.ToString() : null)
                        .Where(v => v != null).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
                    if (values.Any())
                    {
                        columnObject["sampleData"] = values;
                    }
                }

                string fullPathKey = $"{column.FullQualifiedTableName}.{column.Name}";
                parentNode[fullPathKey] = columnObject;
            }

            // 2. Process FOREIGN KEY relationships
            foreach (var relationship in outgoingRelationships)
            {
                var referencedTableName = relationship.ToTable;
                var childNode = new Dictionary<string, object>();
                parentNode[referencedTableName] = childNode;

                // **ПОПРАВКА:** Повикот беше кон `BuildNodeRecursiveAsync`, поправен е во `BuildOntologyNodeRecursiveAsync`
                await BuildOntologyNodeRecursiveAsync(
                    referencedTableName,
                    childNode,
                    visitedTables, // Го проследуваме истиот сет на посетени табели
                    allRelationships,
                    cancellationToken);
            }
        }

        private static (List<string> Domains, string Description) ParseTableDescription(string? rawDescription)
        {
            if (string.IsNullOrWhiteSpace(rawDescription) ||
                !rawDescription.Contains("Domain:", StringComparison.OrdinalIgnoreCase) && !rawDescription.Contains("Description:", StringComparison.OrdinalIgnoreCase))
            {
                return (new List<string>(), rawDescription ?? string.Empty);
            }

            var parsedDomains = new List<string>();
            string? parsedDescription = null;

            var parts = rawDescription.Split(';');
            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (trimmedPart.StartsWith("Domain:", StringComparison.OrdinalIgnoreCase))
                {
                    var domainValues = trimmedPart.Substring("Domain:".Length).Trim();
                    parsedDomains.AddRange(domainValues.Split(',')
                                             .Select(d => d.Trim())
                                             .Where(d => !string.IsNullOrWhiteSpace(d)));
                }
                else if (trimmedPart.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                {
                    parsedDescription = trimmedPart.Substring("Description:".Length).Trim();
                }
            }

            return (parsedDomains, parsedDescription ?? string.Empty);
        }

        public async Task<List<TableDefinition>> GetTableDefinitionsAsync(List<string>? tableNames, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var tableDefinitions = new List<TableDefinition>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    var queryBuilder = new StringBuilder(@"
                        SELECT
                            s.name AS SchemaName,
                            t.name AS TableName,
                            ep.value AS TableDescription
                        FROM sys.tables t
                        JOIN sys.schemas s ON t.schema_id = s.schema_id
                    ");

                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", (object?)extendedPropertyName ?? DBNull.Value);

                    if (tableNames != null && tableNames.Any())
                    {
                        // When specific tables are requested, use LEFT JOIN to get their description if it exists.
                        queryBuilder.AppendLine(@"
                            LEFT JOIN sys.extended_properties ep
                                ON ep.major_id = t.object_id
                                AND ep.minor_id = 0
                                AND ep.name = @ExtendedPropertyName
                        ");

                        var paramNames = new List<string>();
                        for (int i = 0; i < tableNames.Count; i++)
                        {
                            var paramName = $"@Table{i}";
                            paramNames.Add(paramName);
                            cmd.Parameters.AddWithValue(paramName, tableNames[i]);
                        }
                        queryBuilder.AppendLine($"WHERE (s.name + '.' + t.name) IN ({string.Join(",", paramNames)})");
                    }
                    else
                    {
                        // When no specific tables are requested (tableNames is null or empty),
                        // use INNER JOIN to return only tables that HAVE the extended property.
                        queryBuilder.AppendLine(@"
                            INNER JOIN sys.extended_properties ep
                                ON ep.major_id = t.object_id
                                AND ep.minor_id = 0
                                AND ep.name = @ExtendedPropertyName
                        ");
                    }

                    queryBuilder.AppendLine("ORDER BY s.name, t.name;");
                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schema = reader["SchemaName"].ToString() ?? string.Empty;
                            string name = reader["TableName"].ToString() ?? string.Empty;
                            string fullTableName = $"{schema}.{name}";
                            string rawDescription = reader["TableDescription"].ToString() ?? string.Empty;
                            var (domains, cleanDescription) = ParseTableDescription(rawDescription);

                            tableDefinitions.Add(new TableDefinition
                            {
                                Id = GeneratorUtil.GenerateDeterministicUlongId(fullTableName, true),
                                FullQualifiedTableName = fullTableName,
                                Schema = schema,
                                Name = name,
                                Description = cleanDescription,
                                BelongsToDomains = domains
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GetTableDefinitionsAsync :{ex?.Message ?? ex?.InnerException?.Message}");
                throw;
            }
            return tableDefinitions;
        }

        public async Task<List<ViewDefinition>> GetViewDefinitionsAsync(List<string> viewNames, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var viewDefinitions = new List<ViewDefinition>();
            try
            {
                var queryBuilder = new StringBuilder();
                var whereClauses = new List<string>();

                queryBuilder.AppendLine(@"
                    SELECT
                        s.name AS SchemaName,
                        v.name AS ViewName,
                        ep.value AS ViewDescription
                    FROM sys.views v
                    JOIN sys.schemas s ON v.schema_id = s.schema_id
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", (object)extendedPropertyName ?? DBNull.Value);
                    if (viewNames != null && viewNames.Any())
                    {
                        // Case: Specific view names provided
                        queryBuilder.AppendLine(@"
                            LEFT JOIN sys.extended_properties ep
                                ON ep.major_id = v.object_id
                                AND ep.minor_id = 0
                                AND ep.name = @ExtendedPropertyName
                        ");
                        var paramNames = viewNames.Select((t, i) => $"@View{i}").ToList();
                        for (int i = 0; i < viewNames.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], viewNames[i]);
                        whereClauses.Add($"(s.name + '.' + v.name) IN ({string.Join(",", paramNames)})");
                        whereClauses.Add("ep.value IS NOT NULL"); // Always filter by extended property value when specific views are requested
                    }
                    else
                    {
                        // Case: Empty list of view names, meaning "all views with this extended property"
                        queryBuilder.AppendLine(@"
                            INNER JOIN sys.extended_properties ep
                                ON ep.major_id = v.object_id
                                AND ep.minor_id = 0
                                AND ep.name = @ExtendedPropertyName
                        ");
                        whereClauses.Add("ep.value IS NOT NULL"); // Ensure the property value exists
                    }

                    if (whereClauses.Any())
                    {
                        queryBuilder.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
                    }

                    queryBuilder.AppendLine("ORDER BY s.name, v.name;");
                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schema = reader["SchemaName"].ToString() ?? string.Empty;
                            string name = reader["ViewName"].ToString() ?? string.Empty;
                            string fullViewName = $"{schema}.{name}";
                            string rawDescription = reader["ViewDescription"].ToString() ?? string.Empty;
                            var (domains, cleanDescription) = ParseTableDescription(rawDescription);

                            viewDefinitions.Add(new ViewDefinition
                            {
                                Id = GeneratorUtil.GenerateDeterministicUlongId(fullViewName, true),
                                FullQualifiedViewName = fullViewName,
                                Schema = schema,
                                Name = name,
                                Description = cleanDescription,
                                BelongsToDomains = domains
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetViewDefinitionsAsync");
            }
            return viewDefinitions;
        }

        public async Task<List<FunctionDefinition>> GetTableValueFunctionsAsync(string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var functionDefinitions = new List<FunctionDefinition>();

            if (string.IsNullOrWhiteSpace(extendedPropertyName))
            {
                throw new ArgumentException("Extended property name cannot be null or empty.", nameof(extendedPropertyName));
            }

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT
                        s.name AS SchemaName,
                        f.name AS FunctionName,
                        ep.value AS FunctionDescription
                    FROM sys.objects AS f
                    JOIN sys.schemas AS s ON f.schema_id = s.schema_id
                    LEFT JOIN sys.extended_properties AS ep
                        ON ep.major_id = f.object_id
                        AND ep.minor_id = 0
                        AND ep.name = @ExtendedPropertyName
                    WHERE ep.value IS NOT NULL AND f.type = 'IF'  -- 'IF' Inline Value Table Functions
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", extendedPropertyName);

                    queryBuilder.AppendLine("ORDER BY s.name, f.name;");
                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schemaFromDb = reader["SchemaName"].ToString() ?? string.Empty;
                            string nameFromDb = reader["FunctionName"].ToString() ?? string.Empty;
                            string fullFunctionNameFromDb = $"{schemaFromDb}.{nameFromDb}";
                            string jsonDescription = reader["FunctionDescription"].ToString() ?? string.Empty; ;

                            if (string.IsNullOrWhiteSpace(jsonDescription))
                            {
                                _logger.LogWarning("Function '{FunctionName}' has a null or empty description property. Skipping.", fullFunctionNameFromDb);
                                continue;
                            }

                            try
                            {
                                // The deserializer will use the [JsonPropertyName] attributes on the FunctionDefinition model.
                                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                                // Fix for potentially double-wrapped or malformed JSON from the database.
                                // This handles cases where the JSON string is accidentally wrapped in an extra set of curly braces.
                                string jsonToParse = jsonDescription.Trim();

                                // Deserialize directly into a dictionary where the value is a FunctionDefinition.
                                // This removes the need for the private FunctionDetails, ParameterDetail, and ReturnDetail models.
                                var parsedJson = JsonSerializer.Deserialize<Dictionary<string, FunctionDefinition>>(jsonToParse, jsonOptions);
                                if (parsedJson == null || !parsedJson.Any())
                                {
                                    _logger.LogWarning("Could not deserialize or found no entries in the JSON description for function '{FunctionName}'. JSON: {Json}", fullFunctionNameFromDb, jsonDescription);
                                    continue;
                                }

                                // The JSON has a single root key which is the function signature.
                                // The value is deserialized directly into our target FunctionDefinition object.
                                var (signature, functionDefinition) = parsedJson.First();

                                // Populate the remaining properties that are not part of the JSON description
                                // but are known from the database query context.
                                functionDefinition.Id = GeneratorUtil.GenerateDeterministicUlongId(fullFunctionNameFromDb, true);
                                functionDefinition.FullQualifiedFunctionName = fullFunctionNameFromDb;
                                functionDefinition.FunctionSignature = signature;
                                functionDefinition.Schema = schemaFromDb;
                                functionDefinition.Name = nameFromDb;
                                functionDefinition.RawJsonDescription = jsonDescription;

                                functionDefinitions.Add(functionDefinition);
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError(ex, "Failed to parse JSON description for function '{FunctionName}'. Raw JSON: {Json}", fullFunctionNameFromDb, jsonDescription);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "An unexpected error occurred while processing function '{FunctionName}'.", fullFunctionNameFromDb);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTableValueFunctionsAsync");
            }

            return functionDefinitions;
        }

        public async Task<List<FunctionDefinition>> GetStoredProceduresAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var spDefinitions = new List<FunctionDefinition>();
            if (schemas == null || !schemas.Any() || string.IsNullOrWhiteSpace(extendedPropertyName))
            {
                _logger.LogWarning("GetStoredProceduresAsync called with no schemas or no extended property name.");
                return spDefinitions;
            }

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT
                        s.name AS SchemaName,
                        p.name AS ProcedureName,
                        ep.value AS ProcedureDescription
                    FROM sys.procedures AS p
                    JOIN sys.schemas AS s ON p.schema_id = s.schema_id
                    JOIN sys.extended_properties AS ep
                        ON ep.major_id = p.object_id
                        AND ep.minor_id = 0
                        AND ep.name = @ExtendedPropertyName
                    WHERE ep.value IS NOT NULL
                ");

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand())
                {
                    cmd.Parameters.AddWithValue("@ExtendedPropertyName", extendedPropertyName);

                    var paramNames = new List<string>();
                    int i = 0;
                    foreach (var schema in schemas)
                    {
                        var paramName = $"@Schema{i++}";
                        paramNames.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, schema);
                    }
                    queryBuilder.AppendLine($"AND s.name IN ({string.Join(",", paramNames)})");
                    queryBuilder.AppendLine("ORDER BY s.name, p.name;");

                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Connection = conn;

                    await conn.OpenAsync(cancellationToken);
                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schemaFromDb = reader["SchemaName"].ToString() ?? string.Empty;
                            string nameFromDb = reader["ProcedureName"].ToString() ?? string.Empty;
                            string fullSpName = $"{schemaFromDb}.{nameFromDb}";
                            string jsonDescription = reader["ProcedureDescription"].ToString() ?? string.Empty;

                            if (string.IsNullOrWhiteSpace(jsonDescription))
                            {
                                _logger.LogWarning("Stored Procedure '{spName}' has a null or empty description property. Skipping.", fullSpName);
                                continue;
                            }

                            try
                            {
                                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                var spDefinition = JsonSerializer.Deserialize<FunctionDefinition>(jsonDescription, jsonOptions);

                                spDefinition.Id = GeneratorUtil.GenerateDeterministicUlongId(fullSpName, true);
                                spDefinition.FullQualifiedFunctionName = fullSpName;
                                spDefinition.Schema = schemaFromDb;
                                spDefinition.Name = nameFromDb;
                                spDefinition.FunctionSignature = $"{schemaFromDb}.{nameFromDb}"; // Set a basic signature
                                spDefinition.RawJsonDescription = jsonDescription;

                                spDefinitions.Add(spDefinition);
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError(ex, "Failed to parse JSON description for Stored Procedure '{spName}'. Raw JSON: {Json}", fullSpName, jsonDescription);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStoredProceduresAsync");
            }
            return spDefinitions;
        }

        public async Task<string> GenerateTableOntologyAsDDLAsync(List<string> tableNames, CancellationToken cancellationToken = default)
        {
            if (tableNames == null || !tableNames.Any())
            {
                return "-- No tables specified for DDL generation.";
            }

            var ddlBuilder = new StringBuilder();
            var tableDefinitions = await GetTableDefinitionsAsync(tableNames, _settings.TableDescriptionProperty, cancellationToken);

            foreach (var tableDef in tableDefinitions)
            {
                if (ddlBuilder.Length > 0)
                {
                    ddlBuilder.AppendLine("\n");
                }

                if (!string.IsNullOrWhiteSpace(tableDef.Description))
                {
                    ddlBuilder.AppendLine($"-- {tableDef.Description.Replace("\n", "\n-- ")}");
                }
                ddlBuilder.AppendLine($"CREATE TABLE {tableDef.FullQualifiedTableName} (");

                var columnDefinitions = await GetColumnDefinitionsAsync(tableDef.FullQualifiedTableName, _settings.ColumnDescriptionProperty, cancellationToken);
                var columnStrings = new List<string>();
                var constraintStrings = new List<string>();

                foreach (var colDef in columnDefinitions)
                {
                    var colLine = new StringBuilder();
                    colLine.Append($"    [{colDef.Name}] {colDef.Type}");

                    if (!colDef.IsNullable)
                    {
                        colLine.Append(" NOT NULL");
                    }

                    // Primary key constraint will be added separately to handle composite keys and naming
                    string comment = string.IsNullOrWhiteSpace(colDef.Description) ? "" : $" -- {colDef.Description.Replace("\n", " ")}";
                    colLine.Append(comment);
                    columnStrings.Add(colLine.ToString());
                }

                ddlBuilder.AppendLine(string.Join(",\n", columnStrings));

                // Add Primary Key Constraint
                var pkColumns = columnDefinitions.Where(c => c.IsPrimaryKey).ToList();
                if (pkColumns.Any())
                {
                    var pkColumnNames = string.Join(", ", pkColumns.Select(c => $"[{c.Name}]"));
                    // Note: Getting the actual PK constraint name would require another query.
                    // For simplicity, we'll use a conventional name like PK_TableName.
                    ddlBuilder.AppendLine(",");
                    ddlBuilder.AppendLine($"    CONSTRAINT PK_{tableDef.Name} PRIMARY KEY ({pkColumnNames})");
                }

                // Add Foreign Key Constraints
                var fkRelationships = await GetForeignKeyRelationshipsForTableAsync(tableDef.FullQualifiedTableName);
                foreach (var fk in fkRelationships)
                {
                    // Ensure there are columns to join on before adding the constraint
                    if (fk.KeyColumns.Any())
                    {
                        var fromColumns = string.Join(", ", fk.KeyColumns.Select(kc => $"[{kc.FromColumn}]"));
                        var toColumns = string.Join(", ", fk.KeyColumns.Select(kc => $"[{kc.ToColumn}]"));
                        ddlBuilder.AppendLine(",");
                        ddlBuilder.AppendLine($"    CONSTRAINT {fk.ForeignKeyName} FOREIGN KEY ({fromColumns}) REFERENCES {fk.ToTable}({toColumns})");
                    }
                }

                ddlBuilder.AppendLine("\n);");
            }

            return ddlBuilder.ToString();
        }

        public async Task<string> GenerateViewOntologyAsDDLAsync(List<string> viewNames, CancellationToken cancellationToken = default)
        {
            if (viewNames == null || !viewNames.Any())
            {
                return "-- No views specified for DDL generation.";
            }

            var ddlBuilder = new StringBuilder();
            var viewDefinitions = await GetViewDefinitionsAsync(viewNames, _settings.ViewDescriptionProperty, cancellationToken);

            foreach (var viewDef in viewDefinitions)
            {
                if (ddlBuilder.Length > 0)
                {
                    ddlBuilder.AppendLine("\n");
                }

                if (!string.IsNullOrWhiteSpace(viewDef.Description))
                {
                    ddlBuilder.AppendLine($"-- {viewDef.Description.Replace("\n", "\n-- ")}");
                }
                ddlBuilder.AppendLine($"-- NOTE: This is a simplified DDL representation of a VIEW. It shows the columns and their descriptions.");
                ddlBuilder.AppendLine($"CREATE VIEW {viewDef.FullQualifiedViewName} AS");
                ddlBuilder.AppendLine("SELECT");

                var columnDefinitions = await GetColumnDefinitionsAsync(viewDef.FullQualifiedViewName, _settings.ColumnDescriptionProperty, cancellationToken);
                var columnStrings = new List<string>();

                foreach (var colDef in columnDefinitions)
                {
                    string comment = string.IsNullOrWhiteSpace(colDef.Description) ? "" : $" -- {colDef.Description.Replace("\n", " ")}";
                    columnStrings.Add($"    [{colDef.Name}] AS [{colDef.Name}],{comment}");
                }

                ddlBuilder.AppendLine(string.Join("\n", columnStrings).TrimEnd(','));
                ddlBuilder.AppendLine("FROM [underlying_tables...];");
            }

            return ddlBuilder.ToString();
        }

        public async Task<string> GenerateSchemaDescriptionsBackupAsync(List<string> schemas, CancellationToken cancellationToken = default)
        {
            if (schemas == null || !schemas.Any())
            {
                schemas = _settings.SchemasToScan;
            }

            if (schemas == null || !schemas.Any())
            {
                _logger.LogWarning("GenerateSchemaDescriptionsBackupAsync called with no schemas.");
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"-- Backup of Extended Properties ({_settings.TableDescriptionProperty})");
            sb.AppendLine($"-- Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("-- Schemas: " + string.Join(", ", schemas));
            sb.AppendLine();

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT 
                        s.name AS SchemaName,
                        o.name AS ObjectName,
                        c.name AS ColumnName,
                        ep.name AS PropertyName,
                        ep.value AS PropertyValue,
                        o.type AS ObjectType
                    FROM sys.extended_properties ep
                    JOIN sys.objects o ON ep.major_id = o.object_id
                    JOIN sys.schemas s ON o.schema_id = s.schema_id
                    LEFT JOIN sys.columns c ON ep.major_id = c.object_id AND ep.minor_id = c.column_id
                    WHERE 
                        ep.name = @PropertyName 
                        AND s.name IN ({0})
                        AND (
                            (ep.minor_id = 0 AND o.type IN ('U', 'IF')) -- Table (U) or Inline Function (IF) description
                            OR 
                            (ep.minor_id > 0 AND o.type IN ('U', 'IF')) -- Column description
                        )
                    ORDER BY s.name, o.name, c.column_id
                ");

                var paramNames = new List<string>();
                var parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("@PropertyName", _settings.TableDescriptionProperty));

                for (int i = 0; i < schemas.Count; i++)
                {
                    var pName = $"@s{i}";
                    paramNames.Add(pName);
                    parameters.Add(new SqlParameter(pName, schemas[i]));
                }

                string finalQuery = string.Format(queryBuilder.ToString(), string.Join(",", paramNames));

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(finalQuery, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    await conn.OpenAsync(cancellationToken);

                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            string schema = reader["SchemaName"].ToString();
                            string objName = reader["ObjectName"].ToString();
                            string colName = reader["ColumnName"] == DBNull.Value ? null : reader["ColumnName"].ToString();
                            string propName = reader["PropertyName"].ToString();
                            string propValue = reader["PropertyValue"].ToString();
                            string objType = reader["ObjectType"].ToString().Trim();
                            string level1Type = objType == "IF" ? "FUNCTION" : "TABLE";
                            string escapedValue = propValue.Replace("'", "''");

                            if (colName == null)
                                sb.AppendLine($"EXEC sys.sp_addextendedproperty @name=N'{propName}', @value=N'{escapedValue}', @level0type=N'SCHEMA', @level0name=N'{schema}', @level1type=N'{level1Type}', @level1name=N'{objName}';");
                            else
                                sb.AppendLine($"EXEC sys.sp_addextendedproperty @name=N'{propName}', @value=N'{escapedValue}', @level0type=N'SCHEMA', @level0name=N'{schema}', @level1type=N'{level1Type}', @level1name=N'{objName}', @level2type=N'COLUMN', @level2name=N'{colName}';");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating extended properties backup.");
                sb.AppendLine($"-- Error generating backup: {ex.Message}");
            }

            return sb.ToString();
        }

    }
}


// End of file
