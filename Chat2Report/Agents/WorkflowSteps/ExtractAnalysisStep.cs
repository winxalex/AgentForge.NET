using Chat2Report.Comparer;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Providers;
using Chat2Report.Services;
using Chat2Report.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Chat2Report.Agents.Generated.IO.ExtractAnalysis;

namespace Chat2Report.Agents.WorkflowSteps
{




    /// <summary>
    /// A workflow step designed to process the output of the OntologyAgent.
    /// It extracts the 'analyses' object from the state, which contains the structured
    /// LLM response, and sets it as the new root of the state.
    /// It deserializes the analysis into a `UserQueryAnalysis` object and implements the
    /// core logic from `AnalzizaAlgo.txt` to resolve attributes, check types, and prepare
    /// the data for the final SQL generation step.
    /// </summary>
    public class ExtractAnalysisStep : IWorkflowStep<Inputs, Outputs>
    {
        private readonly ILogger<ExtractAnalysisStep> _logger;
        private readonly IDataSchemaService _dataSchemaService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly IVectorStore _vectorStore;
        private readonly ValueMatcherОptions _valueMatcherSettings;
        

        public ExtractAnalysisStep(ILogger<ExtractAnalysisStep> logger,
            IDataSchemaService dataSchemaService,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IVectorStore vectorStore,
            IOptions<ValueMatcherОptions> valueMatcherOptions
            )
        {
            _logger = logger;
            _dataSchemaService = dataSchemaService;
            _embeddingGenerator = embeddingGenerator;
            _vectorStore = vectorStore;
            _valueMatcherSettings = valueMatcherOptions.Value;
        }


        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext context, CancellationToken cancellationToken)
        {

            if (inputs.Analysis is null)
            {
                throw new ArgumentNullException(nameof(inputs.Analysis), "Input 'Analysis' object cannot be null.");
            }

            _logger.LogInformation("--- Starting semantic analysis processing for query: '{Query}' ---", inputs.Analysis.UserQuery);

            var analysis = inputs.Analysis; // Работиме директно на инстанцата за да ја мутираме


            // 2. Implement the logic 
            _logger.LogInformation("--- Starting user query processing: \"{UserQuery}\" ---", analysis.UserQuery);

            var resolvedConditions = new List<ResolvedValueCondition>();
            var resolvedJoins = new List<ResolvedJoinCondition>();
            var allRelevantColumns = new HashSet<ColumnDefinition>(new ColumnDefinitionEqualityComparer());
            var processedMentionedValues = new HashSet<ConceptValue>(); // To avoid processing the same value multiple times
            var resolutionContext = new Dictionary<string, ResolvedValueCondition>(StringComparer.OrdinalIgnoreCase);

              // Збогати го analysis објектот со дополнителни информации од влезовите
            analysis.RelevantTables = inputs.RelevantTables;
            analysis.RelevantViews = inputs.RelevantViews;
            analysis.RelevantDomains = inputs.RelevantDomains;
            analysis.RelevantFunctions = inputs.RelevantFunctionDefinitions;
            


            if (analysis.ExtractedAttributes != null && analysis.ExtractedAttributes.Any())
            {
                foreach (var attribute in analysis.ExtractedAttributes)
                {
                    // Handle JOIN intent specifically, as it has a different structure
                    if ("JOIN".Equals(attribute.Intent, StringComparison.OrdinalIgnoreCase))
                    {
                        if (attribute.SchemaReferences == null || attribute.SchemaReferences.Count != 2)
                        {
                            _logger.LogWarning("JOIN intent for attribute '{AttributeName}' requires exactly two schema references. Found {Count}. Skipping.", attribute.Name, attribute.SchemaReferences?.Count ?? 0);
                            continue;
                        }

                        var col1Fqn = attribute.SchemaReferences[0];
                        var col2Fqn = attribute.SchemaReferences[1];

                        var col1Def = await ResolveColumnDefinitionAsync(col1Fqn, analysis.RelevantFunctions, cancellationToken);
                        var col2Def = await ResolveColumnDefinitionAsync(col2Fqn, analysis.RelevantFunctions, cancellationToken);

                        if (col1Def == null || col2Def == null)
                        {
                            _logger.LogWarning("Could not resolve one or both columns for JOIN intent '{AttributeName}'. Col1: '{Col1Fqn}', Col2: '{Col2Fqn}'. Skipping.", attribute.Name, col1Fqn, col2Fqn);
                            continue;
                        }

                        // Add both columns to the set of relevant columns for SQL generation
                        allRelevantColumns.Add(col1Def);
                        allRelevantColumns.Add(col2Def);



                        resolvedJoins.Add(new ResolvedJoinCondition
                        {
                            FromColumn = col1Def,
                            ToColumn = col2Def,

                        });

                        _logger.LogInformation("=> Processed JOIN intent '{AttributeName}': JOIN {Table1}.{Column1} ON {Table2}.{Column2}",
                            attribute.Name, col1Def.FullQualifiedTableName, col1Def.Name, col2Def.FullQualifiedTableName, col2Def.Name);

                        continue; // Move to the next attribute
                    }


                    // --- Existing logic for other intents (SELECT, WHERE, etc.) ---
                    if (attribute.SchemaReferences == null || !attribute.SchemaReferences.Any())
                    {
                        if (!"LIMIT".Equals(attribute.Intent, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Attribute '{AttributeName}' has no schema references and is not a LIMIT intent, skipping.", attribute.Name);
                        }
                        // Also skip if it's a JOIN that was malformed and fell through
                        continue;
                    }

                    foreach (var fullColumnName in attribute.SchemaReferences)
                    {
                        var parts = fullColumnName.Split('.');
                        if (parts.Length != 3)
                        {
                            _logger.LogWarning("Invalid schema reference format '{SchemaReference}' for attribute '{Attribute}'. Expected 'schema.table.column' or 'schema.function.return_value'.", fullColumnName, attribute.Name);
                            continue;
                        }

                        // can be table, view, inline-value-function
                        string shemaReference = $"{parts[0]}.{parts[1]}";

                        // First, try to resolve as a database column
                        string columnName = parts[2];

                        var columnsForTable = await _dataSchemaService.GetColumnDefinitionsAsync(shemaReference, cancellationToken: cancellationToken);
                        var targetColumnDef = columnsForTable.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                        if (targetColumnDef != null)
                        {
                            allRelevantColumns.Add(targetColumnDef);


                            // --- Value Matching Logic ---
                            var mentionedValue = attribute.Value;
                            if (mentionedValue != null)
                            {
                                if (processedMentionedValues.Contains(mentionedValue)) continue;

                                if (!GeneratorUtil.IsTypeCompatible(targetColumnDef, mentionedValue))
                                {
                                    _logger.LogWarning("      Type mismatch: Value of type '{ValueType}' is not compatible with column '{ColumnName}' of type '{ColumnType}'. Skipping condition.",
                                        mentionedValue.Type, targetColumnDef.FullQualifiedTableName + "." + targetColumnDef.Name, targetColumnDef.Type);
                                    continue;
                                }

                                // Special handling for dates/numerics - they are pre-processed and we just create the condition
                                if (mentionedValue.Type == MentionedValueType.SingleDate
                                    || mentionedValue.Type == MentionedValueType.DateRange
                                    || mentionedValue.Type == MentionedValueType.NumericRange
                                    || mentionedValue.Type == MentionedValueType.SingleNumeric
                                    || mentionedValue.Type == MentionedValueType.TemporalExpression
                                    )
                                {
                                    _logger.LogInformation("      Detected date or numeric value. Creating resolved condition directly (no semantic value search).");

                                    List<string> canonical = mentionedValue.Data;

                                    if (mentionedValue.Type == MentionedValueType.TemporalExpression)
                                    {
                                        canonical = mentionedValue.Resolved;
                                    }

                                    resolvedConditions.Add(new ResolvedValueCondition
                                    {
                                        TargetColumn = targetColumnDef,
                                        CanonicalValues = canonical,
                                        EffectiveHint = mentionedValue.Hint ?? WhereHint.Unknown,
                                        OriginalHint = mentionedValue.Hint ?? WhereHint.Unknown,
                                        EffectiveType = mentionedValue.Type ?? MentionedValueType.Unknown,
                                        OriginalType = mentionedValue.Type ?? MentionedValueType.Unknown,
                                        OriginalUserText = mentionedValue.OriginalUserText,
                                    });
                                    continue;
                                }

                                bool canAttemptValueMatch = targetColumnDef.IsEnumLikeColumn &&
                                                            (mentionedValue.Type == MentionedValueType.SingleEnum ||
                                                             mentionedValue.Type == MentionedValueType.EnumSet ||
                                                             mentionedValue.Type == MentionedValueType.SingleText
                                                             );

                                if (!canAttemptValueMatch)
                                {
                                    // It's a raw value for a non-enum column (e.g., searching a text field)
                                    if (mentionedValue.Type == MentionedValueType.SingleText || mentionedValue.Type == MentionedValueType.TextSet)
                                    {
                                        resolvedConditions.Add(new ResolvedValueCondition
                                        {
                                            TargetColumn = targetColumnDef,
                                            CanonicalValues = mentionedValue.Data,
                                            EffectiveHint = mentionedValue.Hint ?? WhereHint.Unknown,
                                            OriginalHint = mentionedValue.Hint ?? WhereHint.Unknown,
                                            EffectiveType = mentionedValue.Type ?? MentionedValueType.Unknown,
                                            OriginalType = mentionedValue.Type ?? MentionedValueType.Unknown,
                                            OriginalUserText = mentionedValue.OriginalUserText,
                                        });
                                        processedMentionedValues.Add(mentionedValue);
                                    }
                                    continue;
                                }

                                var parentContextFilters = new List<string>();
                                var parentRelationships = (await _dataSchemaService.GetColumnDefinitionsAsync(shemaReference, null, cancellationToken)).Where(c => c.KeyType.HasFlag(KeyType.Foreign));

                                _logger.LogInformation("      Checking for parent context. Found {Count} FK columns in '{Table}'.", parentRelationships.Count(), shemaReference);

                                foreach (var fkColDef in parentRelationships)
                                {
                                    var relationship = (await _dataSchemaService.GetForeignKeyRelationshipsForTableAsync(shemaReference))
                                                         .FirstOrDefault(r => r.KeyColumns.Any(kc => kc.FromColumn.Equals(fkColDef.Name, StringComparison.OrdinalIgnoreCase)));

                                    if (relationship == null) continue;

                                    if (resolutionContext.TryGetValue(relationship.ToTable, out var parentCondition))
                                    {
                                        var keyPair = relationship.KeyColumns.FirstOrDefault(kc => kc.FromColumn.Equals(fkColDef.Name, StringComparison.OrdinalIgnoreCase));
                                        if (keyPair == null) continue;

                                        string parentPkColumnName = keyPair.ToColumn;
                                        var parentPrimaryKeyValue = parentCondition.RawValueDefinition?.KeysWithTypeColumnNamePairs
                                            .FirstOrDefault(k => k.KeyType.HasFlag(KeyType.Primary) &&
                                                                 k.ColumnName.Equals($"{relationship.ToTable}.{parentPkColumnName}", StringComparison.OrdinalIgnoreCase))?.Value;

                                        if (parentPrimaryKeyValue != null)
                                        {
                                            string contextTag = $"fk_{fkColDef.Name}:{parentPrimaryKeyValue}";
                                            parentContextFilters.Add(contextTag);
                                            _logger.LogInformation("      - SUCCESS: Found parent PK value '{PkValue}'. Created context tag: '{ContextTag}'.", parentPrimaryKeyValue, contextTag);
                                        }
                                    }
                                }

                                VectorSearchOptions<ValueDefinition> valueSearchOptions;
                                if (parentContextFilters.Any())
                                {
                                    valueSearchOptions = new VectorSearchOptions<ValueDefinition>
                                    {
                                        Top = 1,
                                        Filter = vd => parentContextFilters.All(filter => vd.Tags.Contains(filter))
                                    };
                                }
                                else
                                {
                                    valueSearchOptions = new VectorSearchOptions<ValueDefinition> { Top = 1 };
                                }

                                IVectorStoreRecordCollection<ulong, ValueDefinition> valueCollection;
                                try
                                {
                                    string valueCollectionName = $"{targetColumnDef.FullQualifiedTableName.Replace(".", "_")}_{targetColumnDef.Name}_ValueDefinitions";
                                    valueCollection = _vectorStore.GetCollection<ulong, ValueDefinition>(valueCollectionName);
                                }
                                catch (Exception)
                                {
                                    _logger.LogInformation("      [i] Info: No vector value store found for column '{ColumnName}'. Skipping value matching.", targetColumnDef.Name);
                                    continue;
                                }

                                bool isNegation = (mentionedValue.Hint == WhereHint.NotEquals || mentionedValue.Hint == WhereHint.NotInList);
                                var positiveMatches = new List<ResolvedMatch>();
                                var antonymMatches = new List<ResolvedMatch>();
                                var negatedMatches = new List<ResolvedMatch>();
                                bool hasMatchingAntonyms = mentionedValue.Antonyms != null && mentionedValue.Antonyms.Any() && mentionedValue.Data.Count == mentionedValue.Antonyms.Count;

                                for (int i = 0; i < mentionedValue.Data.Count; i++)
                                {
                                    string termToProcess = mentionedValue.Data[i];
                                    var (directCanonical, directScore, rawDef) = await GeneratorUtil.FindCanonicalValueInVDBWithScore(
                                        valueCollection, termToProcess, _embeddingGenerator, valueSearchOptions, _valueMatcherSettings.GoodValueMatchThreshold, _logger);

                                    if (directCanonical != null)
                                    {
                                        if (isNegation)
                                        {
                                            negatedMatches.Add(new ResolvedMatch(directCanonical, directScore, rawDef));
                                        }
                                        else
                                        {
                                            positiveMatches.Add(new ResolvedMatch(directCanonical, directScore, rawDef));
                                        }
                                    }
                                    else if (isNegation && hasMatchingAntonyms)
                                    {
                                        //Check if there is an antonym we can match instead
                                        if (mentionedValue.Antonyms == null || mentionedValue.Antonyms.Count == 0) continue;


                                        string antonymToTry = mentionedValue.Antonyms[i];
                                        var (antonymCanonical, antonymScore, r) = await GeneratorUtil.FindCanonicalValueInVDBWithScore(
                                            valueCollection, antonymToTry, _embeddingGenerator, valueSearchOptions, _valueMatcherSettings.GoodValueMatchThreshold, _logger);

                                        if (antonymCanonical != null)
                                        {
                                            antonymMatches.Add(new ResolvedMatch(antonymCanonical, antonymScore, r));
                                        }
                                    }
                                }

                                if (positiveMatches.Any())
                                {
                                    var newCondition = CreateConditionFromMatches(positiveMatches, targetColumnDef, mentionedValue, isFromAntonym: false, _valueMatcherSettings.GoodValueMatchThreshold);
                                    resolvedConditions.Add(newCondition);
                                    resolutionContext[shemaReference] = newCondition;
                                }
                                if (antonymMatches.Any())
                                {
                                    var newCondition = CreateConditionFromMatches(antonymMatches, targetColumnDef, mentionedValue, isFromAntonym: true, _valueMatcherSettings.GoodValueMatchThreshold);
                                    resolvedConditions.Add(newCondition);
                                    resolutionContext[shemaReference] = newCondition;
                                }
                                if (negatedMatches.Any())
                                {
                                    resolvedConditions.Add(CreateConditionFromMatches(negatedMatches, targetColumnDef, mentionedValue, isFromAntonym: false, _valueMatcherSettings.GoodValueMatchThreshold, forceNegation: true));
                                }

                                processedMentionedValues.Add(mentionedValue);
                            }
                            else
                            {
                                // This attribute is likely for the SELECT clause. We've already added its column to `allRelevantColumns`.
                                _logger.LogInformation("   -> Attribute '{AttributeName}' targeting column '{ColumnName}' is for SELECT clause.", attribute.Name, targetColumnDef.Name);
                            }
                            continue; // Go to the next schema reference
                        }


                        var targetFunction = analysis.RelevantFunctions.FirstOrDefault(f => f.FullQualifiedFunctionName.Equals(shemaReference, StringComparison.OrdinalIgnoreCase));

                        if (targetFunction != null && targetFunction.Returns.TryGetValue(fullColumnName, out var returnColumn))
                        {
                            _logger.LogInformation("=> Processing attribute '{Attribute}' for function return '{FunctionReturn}'", attribute.Name, fullColumnName);

                            if (attribute.Value != null)
                            {
                                // NOTE: We are creating a pseudo-ColumnDefinition for type checking and condition resolution.
                                var pseudoColumnDef = new ColumnDefinition
                                {
                                    Name = fullColumnName.Replace($"{shemaReference}.", string.Empty),
                                    FullQualifiedTableName = targetFunction.FullQualifiedFunctionName, // Using function FQN as table name
                                    Type = returnColumn.Type,
                                    //IsFunctionReturn = true
                                };

                                if (!GeneratorUtil.IsTypeCompatible(pseudoColumnDef, attribute.Value))
                                {
                                    _logger.LogWarning("      Type mismatch: Value of type '{ValueType}' is not compatible with function return '{FunctionReturn}' of type '{ReturnType}'. Skipping condition.",
                                        attribute.Value.Type, fullColumnName, returnColumn.Type);
                                    continue;
                                }



                                _logger.LogInformation("   -> Attribute has value. Creating resolved condition for function return.");
                                resolvedConditions.Add(new ResolvedValueCondition
                                {
                                    TargetColumn = pseudoColumnDef,
                                    CanonicalValues = attribute.Value.Type == MentionedValueType.TemporalExpression && attribute.Value.Resolved.Any() ? attribute.Value.Resolved : attribute.Value.Data,
                                    EffectiveHint = attribute.Value.Hint ?? WhereHint.Unknown,
                                    OriginalHint = attribute.Value.Hint ?? WhereHint.Unknown,
                                    EffectiveType = attribute.Value.Type ?? MentionedValueType.Unknown,
                                    OriginalType = attribute.Value.Type ?? MentionedValueType.Unknown,
                                    OriginalUserText = attribute.Value.OriginalUserText,
                                });
                            }
                            else
                            {
                                _logger.LogInformation("   -> Attribute has no value, likely for SELECT, GROUP, or ORDER intent on a function return.");
                            }
                            continue; // Successfully processed as a function, move to the next schema reference
                        }

                        _logger.LogWarning("Could not resolve schema reference '{SchemaReference}' for attribute '{Attribute}' as either a function return or a database column. Skipping.", fullColumnName, attribute.Name);
                    }
                }
            }

            analysis.ResolvedConditions = resolvedConditions;
            analysis.ResolvedJoins = resolvedJoins;
            analysis.RelevantColumnsPerTableOrView = allRelevantColumns
                .GroupBy(c => c.FullQualifiedTableName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            analysis.RelevantColumns = allRelevantColumns
                .Select(col => new Dictionary<string, object>
                {
                    { "name", $"{col.FullQualifiedTableName}.[{col.Name}]" },
                    { "description", col.Description },
                    {"type",col.Type }
                }).ToList();

            _logger.LogInformation("--- Finished query analysis processing. Found {ConditionCount} conditions and {TableCount} relevant tables. ---",
                analysis.ResolvedConditions.Count,
                analysis.RelevantColumnsPerTableOrView.Count);

            // The 'analysis' object is a reference type, and it has been modified in-place.
            // The modifications will be reflected in the state dictionary.
            _logger.LogInformation("Successfully processed 'UserQueryAnalysis' object. The updated state will be forwarded.");
            

            return new Outputs { ProcessedAnalysis = analysis };
        }

       

        private ResolvedValueCondition CreateConditionFromMatches(List<ResolvedMatch> matches, ColumnDefinition targetColumn, ConceptValue mentionedValue, bool isFromAntonym, double threshold, bool forceNegation = false)
        {
            var canonicalValues = matches.Select(m => m.CanonicalValue).Distinct().ToList();
            var averageScore = matches.Average(m => m.Score);

            var originalHint = mentionedValue.Hint ?? WhereHint.Unknown;
            var effectiveHint = originalHint;

            if (isFromAntonym)
            {
                effectiveHint = (originalHint == WhereHint.NotInList) ? WhereHint.InList : WhereHint.Equals;
            }

            if (forceNegation)
            {
                effectiveHint = (canonicalValues.Count > 1) ? WhereHint.NotInList : WhereHint.NotEquals;
            }

            return new ResolvedValueCondition
            {
                TargetColumn = targetColumn,
                CanonicalValues = canonicalValues,
                OriginalHint = originalHint,
                EffectiveHint = effectiveHint,
                OriginalType = mentionedValue.Type ?? MentionedValueType.Unknown,
                EffectiveType = mentionedValue.Type ?? MentionedValueType.Unknown,
                OriginalUserText = mentionedValue.OriginalUserText,
                IsFromAntonym = isFromAntonym,
                ValueMatchConfidence = 1.0 - (averageScore / threshold), // Normalize confidence
                RawValueDefinition = matches.FirstOrDefault()?.RawValueDefinition
            };
        }

        /// <summary>
        /// Resolves a fully qualified column name (e.g., "schema.table.column") into a ColumnDefinition.
        /// </summary>
        private async Task<ColumnDefinition?> ResolveColumnDefinitionAsync(string fullColumnName, List<FunctionDefinition> functions, CancellationToken cancellationToken)
        {
            var parts = fullColumnName.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid schema reference format '{SchemaReference}'. Expected 'schema.table.column'.", fullColumnName);
                return null;
            }

            string tableWithSchema = $"{parts[0]}.{parts[1]}";
            string columnName = parts[2];

            var columnsForTable = await _dataSchemaService.GetColumnDefinitionsAsync(tableWithSchema, cancellationToken: cancellationToken);
            var columnDef = columnsForTable.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

            if (columnDef != null)
            {
                return columnDef;
            }

            // If not found in a table/view, check if it's a function return value
            var targetFunction = functions.FirstOrDefault(f => f.FullQualifiedFunctionName.Equals(tableWithSchema, StringComparison.OrdinalIgnoreCase));
            if (targetFunction != null && targetFunction.Returns.TryGetValue(fullColumnName, out var returnColumn))
            {
                _logger.LogDebug("Resolved '{FullColumnName}' as a return value from function '{FunctionName}'.", fullColumnName, targetFunction.FullQualifiedFunctionName);
                // Create a pseudo-ColumnDefinition for the function return to be used in subsequent steps.
                return new ColumnDefinition
                {
                    Name = columnName,
                    FullQualifiedTableName = targetFunction.FullQualifiedFunctionName, // Use the function's FQN as the "table" name
                    Type = returnColumn.Type,
                    Description = returnColumn.Description
                };
            }

            _logger.LogWarning("Could not resolve schema reference '{FullColumnName}' as either a database column or a function return value.", fullColumnName);
            return null;
        }
    }
}