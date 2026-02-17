// USearchVectorDataExample/Services/SLMQuery/SchemaProcessorService.cs
// ... (using statements and constructor as before) ...
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;


namespace Chat2Report.Services
{
    public class ValuesRelatedConcept
    {
        public string Value { get; set; } = string.Empty;
        public string RelatedConcept {  get; set; }
        
    }

    public class UserQueryAnylysis
    {
        public string Query { get; set; } = string.Empty;

        public List<string> concepts_attributes_metrics { get; set; } = new List<string>();

        public List<ValuesRelatedConcept> mentioned_values { get; set; }


    }


    //Дај ми ги сите трансакции со статус одбиена и тип кредитна картичка
    //{
    //  "concepts_attributes_metrics": ["трансакции", "статус", "тип"],
    //  "mentioned_values": [
    //    {"value": "одбиена", "related_concept": "статус"},
    //    { "value": "кредитна картичка", "related_concept": "тип"}
    //  ]
    //}


    /// <summary>
    /// Service for processing database schema and embedding column definitions and values.
    /// </summary>
    public class SchemaProcessorService
    {
        // ... (fields and GenerateDeterministicUlongId as before) ...
        private readonly IDataSchemaService _schemaService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly IVectorStore _vectorStore;
        private readonly SchemaProcessingSettings _settings;
        private readonly ILogger<SchemaProcessorService> _logger;
     

       

        public SchemaProcessorService(
            IDataSchemaService schemaService,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IVectorStore vectorStore,
            IOptions<SchemaProcessingSettings> settings,
          
            ILogger<SchemaProcessorService> logger=null)
        {
            _schemaService = schemaService;
            _embeddingGenerator = embeddingGenerator;
            _vectorStore = vectorStore;
           
            _settings = settings.Value;
            _logger = logger ?? LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<SchemaProcessorService>();
        }

      

        //public async Task InitializeSchemaAsync(string collectionName, CancellationToken cancellationToken = default)
        //{
        //    await ProcessSchemaAsync(null, collectionName,cancellationToken);
        //}

        /// <summary>
        /// Orchestrates the entire schema processing workflow based on appsettings.json.
        /// It scans configured schemas, finds tables with descriptions, and then processes
        /// and embeds definitions for tables, columns, values, and domains.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task ProcessAndEmbedSchemaAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting full schema and domain processing using configured settings...");

            // 1. Get relevant table names directly from the database.
            // This is optimized to only return tables from the specified schemas that have the description property set.
            var relevantTableNames = await _schemaService.GetRelevantTableNamesAsync(
                _settings.SchemasToScan,
                _settings.TableDescriptionProperty,
                cancellationToken);

            // NEW: Get relevant view names from the database.
            var relevantViewNames = await _schemaService.GetRelevantViewNamesAsync(
                _settings.SchemasToScan,
                _settings.ViewDescriptionProperty, // Assumes this property exists in settings
                cancellationToken);

            if (!relevantTableNames.Any() && !relevantViewNames.Any())
            {
                _logger.LogWarning("No tables or views with the specified description properties found in the configured schemas.");
                return;
            }
            _logger.LogInformation("Found {TableCount} relevant tables and {ViewCount} relevant views with descriptions to process.", relevantTableNames.Count, relevantViewNames.Count);

            // NEW: Initialize the relationship cache for the relevant scope BEFORE processing columns.
            // This is crucial so that GetColumnDefinitionsAsync can correctly identify foreign keys.
            if (relevantTableNames.Any())
            {
                _logger.LogInformation("Initializing relationship cache for the scope of {Count} relevant tables to ensure FK information is available.", relevantTableNames.Count);
                await _schemaService.InitializeRelationshipCacheAsync(relevantTableNames, cancellationToken);
            }

            // 2. Get the full definitions for these relevant tables.
            var relevantTableDefinitions = await _schemaService.GetTableDefinitionsAsync(
                relevantTableNames,
                _settings.TableDescriptionProperty,
                cancellationToken);

            // NEW: Get the full definitions for these relevant views.
            var relevantViewDefinitions = await _schemaService.GetViewDefinitionsAsync(
                relevantViewNames,
                _settings.ViewDescriptionProperty, // Assumes this property exists in settings
                cancellationToken);

            // 4. Process and embed different schema parts in sequence using the relevant tables.
            // The table definitions are now parsed in GetTableDefinitionsAsync.
            await ProcessTableEmbeddingsAsync(relevantTableDefinitions, cancellationToken);

            relevantTableNames = relevantTableDefinitions.Select(td => td.FullQualifiedTableName).ToList();
            // NEW: Process and embed view definitions.
            await ProcessViewEmbeddingsAsync(relevantViewDefinitions, cancellationToken);

            var columnsToEmbedValuesFor = await ProcessColumnEmbeddingsAsync(relevantTableNames, cancellationToken);
            await ProcessValueEmbeddingsAsync(columnsToEmbedValuesFor, cancellationToken);

            // Reuse the already fetched and parsed table definitions for domain processing.
            await ProcessDomainEmbeddingsAsync(relevantTableDefinitions, relevantViewDefinitions, cancellationToken);

            _logger.LogInformation("Full schema and domain processing complete.");
        }

        /// <summary>
        /// Parses table descriptions, generates embeddings, and upserts them into the vector store.
        /// </summary>
        private async Task ProcessTableEmbeddingsAsync(List<TableDefinition> tableDefinitions, CancellationToken cancellationToken)
        {
            _logger.LogInformation("--- Phase 1: Processing and Embedding Table Description ---");
            var allTableDefinitionsCollection = _vectorStore.GetCollection<ulong, TableDefinition>(_settings.TableCollectionName);
            await allTableDefinitionsCollection.CreateCollectionIfNotExistsAsync(cancellationToken);

            var tableDefinitionsToUpsert = new List<TableDefinition>();

            foreach (var tableDef in tableDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string textToEmbed = $"Table: {tableDef.FullQualifiedTableName}. Description: {tableDef.Description ?? "No description provided."}";
                _logger.LogDebug("Embedding table description {FullTableName}: {TextToEmbed}", tableDef.FullQualifiedTableName, textToEmbed);
                tableDef.Embedding = await _embeddingGenerator.GenerateVectorAsync(textToEmbed, null,cancellationToken);
                tableDefinitionsToUpsert.Add(tableDef);
            }

            if (tableDefinitionsToUpsert.Any())
            {
                _logger.LogInformation("Upserting {Count} TableDefinition entries into '{CollectionName}'...", tableDefinitionsToUpsert.Count, _settings.TableCollectionName);
                await foreach (var _ in allTableDefinitionsCollection.UpsertBatchAsync(tableDefinitionsToUpsert, cancellationToken)) { }
            }
        }

        /// <summary>
        /// Generates embeddings for view definitions and upserts them into the vector store.
        /// </summary>
        private async Task ProcessViewEmbeddingsAsync(List<ViewDefinition> viewDefinitions, CancellationToken cancellationToken)
        {
            _logger.LogInformation("--- Phase 1.5: Processing and Embedding View Definitions ---");
            var allViewDefinitionsCollection = _vectorStore.GetCollection<ulong, ViewDefinition>(_settings.ViewCollectionName); // Assumes this property exists in settings
            await allViewDefinitionsCollection.CreateCollectionIfNotExistsAsync(cancellationToken);

            var viewDefinitionsToUpsert = new List<ViewDefinition>();

            foreach (var viewDef in viewDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string textToEmbed = $"View: {viewDef.FullQualifiedViewName}. Description: {viewDef.Description ?? "No description provided."}";
                _logger.LogDebug("Embedding view definition for {FullViewName}: {TextToEmbed}", viewDef.FullQualifiedViewName, textToEmbed);
                viewDef.Embedding = await _embeddingGenerator.GenerateVectorAsync(textToEmbed, null, cancellationToken);
                viewDefinitionsToUpsert.Add(viewDef);
            }

            if (viewDefinitionsToUpsert.Any())
            {
                _logger.LogInformation("Upserting {Count} ViewDefinition entries into '{CollectionName}'...", viewDefinitionsToUpsert.Count, _settings.ViewCollectionName);
                await foreach (var _ in allViewDefinitionsCollection.UpsertBatchAsync(viewDefinitionsToUpsert, cancellationToken)) { }
            }
        }

        /// <summary>
        /// Fetches and embeds column definitions for the given tables.
        /// </summary>
        private async Task<List<ColumnDefinition>> ProcessColumnEmbeddingsAsync(List<string> tableNames, CancellationToken cancellationToken)
        {
            _logger.LogInformation("--- Phase 2: Processing and Embedding Column Definitions for {Count} tables...", tableNames.Count);
            var allColumnsCollection = _vectorStore.GetCollection<ulong, ColumnDefinition>(_settings.ColumnCollectionName);
            await allColumnsCollection.CreateCollectionIfNotExistsAsync(cancellationToken);

            var columnDefinitionsToUpsert = new List<ColumnDefinition>();
            var columnsToEmbedValuesFor = new List<ColumnDefinition>();

            foreach (var tableFullName in tableNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Processing table for ColumnDefinitions: {TableFullName}", tableFullName);

                var retrievedColumnDefinitions = await _schemaService.GetColumnDefinitionsAsync(tableFullName, _settings.ColumnDescriptionProperty).ConfigureAwait(false);
                if (!retrievedColumnDefinitions.Any()) continue;

                foreach (var colDef in retrievedColumnDefinitions)
                {
                    string textToEmbed = $"{colDef.Description}";
                    _logger.LogDebug("Embedding column definition for {FullColumnName}: {TextToEmbed}", $"{colDef.FullQualifiedTableName}.{colDef.Name}", textToEmbed);
                    colDef.Embedding = await _embeddingGenerator.GenerateVectorAsync(textToEmbed, null,cancellationToken);
                    columnDefinitionsToUpsert.Add(colDef);

                    if (colDef.IsEnumLikeColumn)
                    {
                        columnsToEmbedValuesFor.Add(colDef);
                    }
                }
            }

            if (columnDefinitionsToUpsert.Any())
            {
                _logger.LogInformation("Upserting {Count} ColumnDefinition entries into '{CollectionName}'...", columnDefinitionsToUpsert.Count, _settings.ColumnCollectionName);
                await foreach (var _ in allColumnsCollection.UpsertBatchAsync(columnDefinitionsToUpsert, cancellationToken)) { }
            }

            return columnsToEmbedValuesFor;
        }

        /// <summary>
        /// Fetches and embeds distinct values for columns marked as enum-like.
        /// </summary>
        private async Task ProcessValueEmbeddingsAsync(List<ColumnDefinition> columnsToEmbedValuesFor, CancellationToken cancellationToken)
        {
            if (!columnsToEmbedValuesFor.Any())
            {
                _logger.LogInformation("--- Phase 3: No columns marked for value embedding. Skipping. ---");
                return;
            }

            _logger.LogInformation("--- Phase 3: Processing and Embedding Values for {Count} columns... ---", columnsToEmbedValuesFor.Count);

            // Групирај по табела за ефикасност, за да не се повикува GetColumnDefinitionsAsync во секоја итерација
            var columnsByTable = columnsToEmbedValuesFor.GroupBy(c => c.FullQualifiedTableName);

            foreach (var tableGroup in columnsByTable)
            {
                var tableName = tableGroup.Key;
                _logger.LogInformation("Processing table: {TableName}", tableName);

                // Земи ги дефинициите за сите колони ОДНАПРЕД за оваа табела
                var allColumnDefinitionsForTable = await _schemaService.GetColumnDefinitionsAsync(tableName, null);
                var keyColumnDefinitions = allColumnDefinitionsForTable.Where(c => c.KeyType != KeyType.None).ToList();
                var columnsToSelect = tableGroup.Select(c => c.Name).Union(keyColumnDefinitions.Select(k => k.Name)).Distinct().ToList();

                // Земи ги сите редови со потребните колони
                var allRowsForValueEmbedding = await _schemaService.GetSampleDataAsync(tableName, columnsToSelect, int.MaxValue, cancellationToken);

                foreach (var colDefForValueEmbedding in tableGroup)
                {
                    string valueCollectionName = $"{tableName.Replace(".", "_")}_{colDefForValueEmbedding.Name}_ValueDefinitions";
                    var specificValueCollection = _vectorStore.GetCollection<ulong, ValueDefinition>(valueCollectionName);
                    await specificValueCollection.CreateCollectionIfNotExistsAsync(cancellationToken);

                    var valueDefinitionsToUpsert = new List<ValueDefinition>();

                    foreach (var row in allRowsForValueEmbedding)
                    {
                        if (!row.TryGetValue(colDefForValueEmbedding.Name, out var valueObject) || valueObject == null) continue;

                        string valueStringified = valueObject.ToString();

                        var vd = new ValueDefinition
                        {
                            Id = GeneratorUtil.GenerateDeterministicUlongId($"{valueCollectionName}_{valueStringified}", true),
                            SourceColumnFullQualifiedName = colDefForValueEmbedding.FullQualifiedTableName + "." + colDefForValueEmbedding.Name,
                            SourceColumnType = colDefForValueEmbedding.Type,
                            ValueStringified = valueStringified,
                            IsEnum = true,
                            Tags = new List<string> {
                            $"sourceTable:{tableName}",
                            $"sourceColumn:{colDefForValueEmbedding.Name}"
                        }
                        };

                        // --- КЛУЧНА ЛОГИКА: Пополни ги сите клучеви ---
                        foreach (var keyColDef in keyColumnDefinitions)
                        {
                            if (row.TryGetValue(keyColDef.Name, out var keyValue) && keyValue != null)
                            {
                                vd.KeysWithTypeColumnNamePairs.Add(new ValueDefinition.KeyInfo(
                                    ColumnName: keyColDef.FullQualifiedTableName + "." + keyColDef.Name,
                                    Value: keyValue.ToString(),
                                    KeyType: keyColDef.KeyType
                                ));

                                // Додај таг за брз филтер ако е странски клуч
                                if (keyColDef.KeyType.HasFlag(KeyType.Foreign))
                                {
                                    vd.Tags.Add($"fk_{keyColDef.Name}:{keyValue.ToString()}");
                                }
                            }
                        }

                        vd.Embedding = await _embeddingGenerator.GenerateVectorAsync(vd.ValueStringified, null, cancellationToken);
                        valueDefinitionsToUpsert.Add(vd);
                    }

                    if (valueDefinitionsToUpsert.Any())
                    {
                        _logger.LogInformation("Upserting {Count} distinct ValueDefinition entries into '{ValueCollectionName}'...", valueDefinitionsToUpsert.Count, valueCollectionName);
                        await foreach (var _ in specificValueCollection.UpsertBatchAsync(valueDefinitionsToUpsert.DistinctBy(v => v.Id), cancellationToken)) { }
                    }
                }
            }
        }

        /// <summary>
        /// Associates tables with pre-defined domains from configuration and embeds the domain definitions.
        /// </summary>
        private async Task ProcessDomainEmbeddingsAsync(List<TableDefinition> relevantTableDefinitions, List<ViewDefinition> relevantViewDefinitions, CancellationToken cancellationToken)
        {
            _logger.LogInformation("--- Phase 4: Processing and Embedding Domain Definitions ---");

            // 1. Load pre-defined domains from configuration
            var domains = _settings.Domains.ToDictionary(
                d => d.Name,
                d => new DomainDefinition { Name = d.Name, Description = d.Description, TableNames = new List<string>(), ViewNames = new List<string>() },
                StringComparer.OrdinalIgnoreCase
            );

            if (!domains.Any())
            {
                _logger.LogWarning("No domains are defined in the configuration file. Skipping domain processing.");
                return;
            }
            _logger.LogInformation("Loaded {Count} domain definitions from configuration.", domains.Count);

            // 2. Scan tables from the database to associate them with domains
            // We use the pre-parsed table definitions passed into this method.
            foreach (var tableDef in relevantTableDefinitions)
            {
                if (tableDef.BelongsToDomains.Any())
                {
                    foreach (var domainName in tableDef.BelongsToDomains)
                    {
                        if (domains.TryGetValue(domainName, out var domain))
                        {
                            if (!domain.TableNames.Contains(tableDef.FullQualifiedTableName, StringComparer.OrdinalIgnoreCase))
                            {
                                domain.TableNames.Add(tableDef.FullQualifiedTableName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Table '{TableName}' refers to domain '{DomainName}' which is not defined in the configuration. The table will not be added to this domain.", tableDef.FullQualifiedTableName, domainName);
                        }
                    }
                }
            }

            // NEW: Scan views to associate them with domains
            foreach (var viewDef in relevantViewDefinitions)
            {
                if (viewDef.BelongsToDomains.Any())
                {
                    foreach (var domainName in viewDef.BelongsToDomains)
                    {
                        if (domains.TryGetValue(domainName, out var domain))
                        {
                            if (!domain.ViewNames.Contains(viewDef.FullQualifiedViewName, StringComparer.OrdinalIgnoreCase))
                            {
                                domain.ViewNames.Add(viewDef.FullQualifiedViewName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("View '{ViewName}' refers to domain '{DomainName}' which is not defined in the configuration. The view will not be added to this domain.", viewDef.FullQualifiedViewName, domainName);
                        }
                    }
                }
            }

            // 3. Filter out domains that have no associated tables or views and initialize the rest
            var domainDefinitionsToEmbed = domains.Values
                .Where(d => d.TableNames.Any() || d.ViewNames.Any())
                .ToList();

            if (domainDefinitionsToEmbed.Any())
            {
                await ProcessDomainEmbeddingsAsync(domainDefinitionsToEmbed, cancellationToken); // This calls the other overload
                _logger.LogInformation("Domain processing complete. {Count} domains with associated tables or views were processed and embedded.", domainDefinitionsToEmbed.Count);
            }
            else
            {
                _logger.LogInformation("No relevant tables were found to be associated with the configured domains. Skipping domain embedding.");
            }
        }


        public async Task ProcessDomainEmbeddingsAsync(IEnumerable<DomainDefinition> domains, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing and embedding domains...");
            var domainCollection = _vectorStore.GetCollection<ulong, DomainDefinition>(_settings.DomainCollectionName);
            await domainCollection.CreateCollectionIfNotExistsAsync(cancellationToken);

            var domainsToUpsert = new List<DomainDefinition>();
            foreach (var domain in domains)
            {
                cancellationToken.ThrowIfCancellationRequested();
                domain.Id = GeneratorUtil.GenerateDeterministicUlongId(domain.Name, true);
                // Create a richer text for embedding by combining the domain name and its description.
                //string textToEmbed = $"Domain: {domain.Name}. Description: {domain.Description}";
                string textToEmbed = $"{domain.Description}";
                domain.DescriptionEmbedding = await _embeddingGenerator.GenerateVectorAsync(textToEmbed, null, cancellationToken);
                domainsToUpsert.Add(domain);
            }

            if (domainsToUpsert.Any())
            {
                _logger.LogInformation("Upserting {Count} domains into '{CollectionName}'...", domainsToUpsert.Count, _settings.DomainCollectionName);
                await foreach (var _ in domainCollection.UpsertBatchAsync(domainsToUpsert, cancellationToken))
                {
                    // consume the async enumerable
                }
            }
            _logger.LogInformation("            Domain initialization complete.");
        }


        
    }
}