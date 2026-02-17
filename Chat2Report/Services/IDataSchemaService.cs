﻿﻿﻿// IDataSchemaService.cs
using Chat2Report.Models;

namespace Chat2Report.Services
{

    /// <summary>
    /// Defines methods for interacting with database schemas, including retrieving column definitions,
    /// 
    /// 
    /// // 1. Ги вчитува сите релации од целата база
    /// await dataSchemaService.InitializeRelationshipCacheAsync();

    /// 2. Ги вчитува релациите само за наведените табели
    /// var specificTables = new List<string> { "dbo.forumThreadsAI", "dbo.forumTopics" };
    /// await dataSchemaService.InitializeRelationshipCacheAsync(specificTables);

    /// 3. Ги вчитува сите релации помеѓу табелите во 'dbo' шемата
    /// await dataSchemaService.InitializeRelationshipCacheAsync("dbo");
    /// </summary>
    public interface IDataSchemaService
{
    /// <summary>
    /// Gets column definitions for a specific table or view.
    /// </summary>
    /// <param name="tableOrViewWithSchema">The fully qualified name of the table or view (e.g., "dbo.MyTable" or "dbo.MyView").</param>
    /// <param name="extendedPropertyName">The name of the extended property to retrieve for column descriptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of column definitions.</returns>
    Task<List<ColumnDefinition>> GetColumnDefinitionsAsync(string tableOrViewWithSchema, string extendedPropertyName = "MS_Description_EN", CancellationToken cancellationToken = default);
    Task<List<TableRelationship>> GetEntityRelationshipDiagramAsync(string tableWithSchema, List<string>? selectedTables = null, CancellationToken cancellationToken = default); // Added CancellationToken
    Task<List<Dictionary<string, object?>>> GetSampleDataAsync(
        string tableWithSchema,
        List<string> selectedColumns, // Changed to non-nullable List<string>
        int rowCount = 5, // if rowCount == int.MaxValue, fetch all rows
        CancellationToken cancellationToken = default);

    Task<long> GetTableRowCountAsync(string tableWithSchema, CancellationToken cancellationToken = default);
    
       
    Task<List<string>> GetTableNamesAsync(List<string>? tables = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of fully qualified table names that are within specified schemas and have a specific extended property set.
    /// </summary>
    /// <param name="schemas">The list of database schemas to scan.</param>
    /// <param name="extendedPropertyName">The name of the extended property that must exist on the table.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of relevant table names.</returns>
    Task<List<string>> GetRelevantTableNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of fully qualified view names that are within specified schemas and have a specific extended property set.
    /// </summary>
    /// <param name="schemas">The list of database schemas to scan.</param>
    /// <param name="extendedPropertyName">The name of the extended property that must exist on the view.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of relevant view names.</returns>
    Task<List<string>> GetRelevantViewNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proactively fetches and caches database relationships at startup.
    /// </summary>
    /// <param name="tableScope">An optional list of table names to limit the scope of the cache. If null or empty, all relationships in the DB are fetched.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeRelationshipCacheAsync(List<string> tableScope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proactively fetches and caches database relationships for all tables within a specific schema.
    /// </summary>
    /// <param name="schema">The database schema to scope the cache to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeRelationshipCacheForSchemaAsync(string schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a JSON string representing an ontological network for a given root entity.
    /// The network is built using columns that have the 'MS_Description_EN' extended property.
    /// Sample values are fetched for columns linked to enum-like tables that have the 'Embed' extended property.
    /// </summary>
    /// <param name="rootEntityName">The conceptual name for the root entity (e.g., "Case").</param> 
    /// <param name="rootTableName">The fully qualified database table name for the root entity (e.g., "dbo.forumThreadsAI").</param> 
    /// <param name="relationshipsToUse">The list of relationships to build the ontology from. If null, the internal cache will be used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON string representing the ontology.</returns>
    Task<string> GenerateOntologyFromRelationshipsAsync(string rootTableName, IReadOnlyList<TableRelationship> relationships, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a JSON string representing an ontological network for a given root entity.
    /// </summary>
    /// <param name="tableScope"></param>
    /// <param name="rootTableName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GenerateTableOntologyAsJsonAsync(List<string> tableScope, string rootTableName = null, CancellationToken cancellationToken = default);
    Task<string> GenerateTableOntologyAsJsonAsync(List<object> tables, string rootTableName = null, CancellationToken cancellationToken = default);
    Task<string> GenerateViewOntologyAsJsonAsync(List<string> viewNames, CancellationToken cancellationToken = default);
    Task<string> GenerateViewOntologyAsJsonAsync(List<object> views, CancellationToken cancellationToken = default);

    Task<string> DetectRootTableAsync(List<string>? tableScope = null, CancellationToken cancellationToken = default);
    Task<List<TableDefinition>> GetTableDefinitionsAsync(List<string> tableNames, string extendedPropertyName, CancellationToken cancellationToken = default);
    Task<List<ViewDefinition>> GetViewDefinitionsAsync(List<string> viewNames, string extendedPropertyName, CancellationToken cancellationToken = default);
    Task<List<TableRelationship>> GetForeignKeyRelationshipsForTableAsync(string tableWithSchema);
    Task<List<FunctionDefinition>> GetTableValueFunctionsAsync(string msDescriptionPropertyName, CancellationToken cancellationToken=default);
    Task<List<FunctionDefinition>> GetStoredProceduresAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default);
    Task<string> GenerateTableOntologyAsDDLAsync(List<string> tableNames, CancellationToken cancellationToken = default);
    Task<string> GenerateViewOntologyAsDDLAsync(List<string> viewNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a SQL script to backup extended properties (MS_Description_EN) for tables, columns, and IVF functions.
    /// </summary>
    /// <param name="schemas">List of schemas to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated SQL script as a string.</returns>
    Task<string> GenerateSchemaDescriptionsBackupAsync(List<string> schemas, CancellationToken cancellationToken = default);
}
}