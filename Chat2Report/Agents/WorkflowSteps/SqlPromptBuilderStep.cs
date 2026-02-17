using Chat2Report.Agents.Generated.IO.SqlPromptBuilder;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Конструира сеопфатен контекст (во markdown формат) потребен за генерирање на SQL,
    /// врз основа на обработениот 'UserQueryAnalysis' објект.
    /// </summary>
    public class SqlPromptBuilderStep : IWorkflowStep<Inputs, Outputs>
    {
        private readonly ILogger<SqlPromptBuilderStep> _logger;
        private readonly IDataSchemaService _schemaService;

        public SqlPromptBuilderStep(ILogger<SqlPromptBuilderStep> logger, IDataSchemaService dataSchemaService)
        {
            _logger = logger;
            _schemaService = dataSchemaService;
        }

       

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            var analysis = inputs.Analysis;
            _logger.LogInformation("Building context for SQL generation based on query: '{Query}'", analysis.UserQuery);

            var fullContextBuilder = new StringBuilder();

            var conditionsByObject = analysis.ResolvedConditions
               .Where(c => c.TargetColumn != null)
               .GroupBy(c => c.TargetColumn.FullQualifiedTableName)
               .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // КОМПЛЕКСНАТА ЛОГИКА ЗА ГРАДЕЊЕ НА КОНТЕКСТОТ ОСТАНУВА ИСТА, НО КОРИСТИ 'analysis'
            fullContextBuilder.Append(BuildDomainContext(analysis));
            var (tableSchemaInfo, viewSchemaInfo) = await BuildSchemaInfoAsync(analysis, conditionsByObject, cancellationToken).ConfigureAwait(false);
            fullContextBuilder.Append(tableSchemaInfo);
            fullContextBuilder.Append(viewSchemaInfo);
            fullContextBuilder.Append(await BuildJoinInfoAsync(analysis, inputs.RootTable, cancellationToken).ConfigureAwait(false));
            fullContextBuilder.Append(BuildTableFunctionInfo(analysis, conditionsByObject));

            var resolvedQuery = ReplaceUserQueryWithCanonicalValues(analysis.UserQuery, analysis.ResolvedConditions,_logger);

            _logger.LogInformation("Successfully built and added SQL generation context to state.");

            return new Outputs
            {
                FullGenerationContext = fullContextBuilder.ToString(),
                ResolvedUserQuery = resolvedQuery
            };
        }

        private string BuildDomainContext(UserQueryAnalysis analysis)
        {
            var contextBuilder = new StringBuilder();
            if (analysis.RelevantDomains != null && analysis.RelevantDomains.Any())
            {
                contextBuilder.AppendLine("### Overall Context: The User's Domain(s) of Interest");
                if (analysis.RelevantDomains.Count == 1)
                {
                    var domainDict = (Dictionary<string, object>)analysis.RelevantDomains.First();
                    domainDict.TryGetValue("name", out var nameObj);
                    domainDict.TryGetValue("description", out var descriptionObj);
                    contextBuilder.AppendLine($"The user's query falls within the **{nameObj ?? "N/A"}** domain. This domain is described as: *{descriptionObj ?? "N/A"}*");
                }
                else
                {
                    contextBuilder.AppendLine("The user's query relates to multiple domains. Here are their descriptions:");
                    foreach (var domain in analysis.RelevantDomains)
                    {
                        var domainDict = (Dictionary<string, object>)domain;
                        domainDict.TryGetValue("name", out var nameObj);
                        domainDict.TryGetValue("description", out var descriptionObj);
                        contextBuilder.AppendLine($"- **{nameObj ?? "N/A"}**: *{descriptionObj ?? "N/A"}*");
                    }
                }
                contextBuilder.AppendLine("Use this high-level context to better understand the user's intent.\n");
            }
            return contextBuilder.ToString();
        }


        /// <summary>
        /// Го заменува оригиналното корисничко барање со канонските вредности пронајдени во базата.
        /// Ова ја зголемува транспарентноста и му помага на LLM-от.
        /// </summary>
        /// <param name="originalQuery">Оригиналниот стринг од корисникот.</param>
        /// <param name="resolvedConditions">Листата на сите разрешени услови.</param>
        /// <returns>Модификуван стринг од барањето.</returns>
        public static string ReplaceUserQueryWithCanonicalValues(string originalQuery, List<ResolvedValueCondition> resolvedConditions,ILogger<SqlPromptBuilderStep> logger)
        {

            if(string.IsNullOrWhiteSpace(originalQuery)) throw new ArgumentException("Original query cannot be null or empty.");


            if ( resolvedConditions == null || !resolvedConditions.Any())
            {
                return originalQuery;
            }

            var modifiedQuery = originalQuery;

            // Точка #6: Обработи ги подолгите фрази прво за да избегнеш парцијални замени.
            var replacements = resolvedConditions
                .Where(c => !string.IsNullOrEmpty(c.OriginalUserText) && c.CanonicalValues.Any())
                .GroupBy(c => c.OriginalUserText, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Key.Length);

            foreach (var group in replacements)
            {
                string textToReplace = group.Key;
                var representativeCondition = group.First();
                var allCanonicalValues = group.SelectMany(c => c.CanonicalValues).Distinct().ToList();


                string formattedValues = FormatCanonicalValues(allCanonicalValues, representativeCondition.EffectiveType);

                if(string.IsNullOrEmpty(formattedValues))
                {
                    logger.LogWarning(textToReplace + " - Unable to format canonical values for replacement.");
                    // Ако не можеме да форматираме валидни вредности, прескокни ја оваа замена.
                    continue;
                }

                string prefix = "";
                if (representativeCondition.EffectiveHint == WhereHint.NotEquals || representativeCondition.EffectiveHint == WhereHint.NotInList)
                {
                    if (!representativeCondition.IsFromAntonym)
                    {
                        prefix = "не ";
                    }
                }

                string replacementText = $"{prefix}[{formattedValues}]";
                modifiedQuery = modifiedQuery.Replace(textToReplace, replacementText, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(modifiedQuery))
            {
                // This should not happen. If it does, it indicates a bug in the replacement logic.
                throw new InvalidOperationException($"Internal error: The user query became empty after canonical value replacement. Original query: '{originalQuery}'");
            }

            return modifiedQuery;
        }


        /// <summary>
        /// Помошна функција за форматирање на листа од вредности во читлив стринг,
        /// со посебен третман за различни типови на податоци.
        /// </summary>
        private static string FormatCanonicalValues(List<string> values, MentionedValueType type)
        {
            if (values == null || !values.Any()) return "";


            if ((type == MentionedValueType.DateRange || type == MentionedValueType.NumericRange) && values.Count == 2)
            {
                return $"помеѓу {values[0]} и {values[1]}";
            }

            // Постоечката логика за листи
            if (values.Count == 1) return values[0];
            if (values.Count == 2) return $"{values[0]} и {values[1]}";

            return $"{string.Join(", ", values.Take(values.Count - 1))} и {values.Last()}";
        }


        private string BuildColumnContext(UserQueryAnalysis analysis)
        {
            var contextBuilder = new StringBuilder();
            if (analysis.RelevantColumnsPerTableOrView != null && analysis.RelevantColumnsPerTableOrView.Any())
            {
                contextBuilder.AppendLine("### Column Descriptions");
                contextBuilder.AppendLine("Here are the descriptions for the columns that might appear in the result set:");

                var allColumns = analysis.RelevantColumnsPerTableOrView.Values.SelectMany(cols => cols).Distinct();

                foreach (var col in allColumns)
                {
                    if (!string.IsNullOrWhiteSpace(col.Description))
                    {
                        contextBuilder.AppendLine($"- **`{col.FullQualifiedTableName}.[{col.Name}]`**: {col.Description}");
                    }
                }
            }
            return contextBuilder.ToString();
        }

        private async Task<(string tableInfo, string viewInfo)> BuildSchemaInfoAsync(UserQueryAnalysis analysis, Dictionary<string, List<ResolvedValueCondition>> conditionsByObject, CancellationToken cancellationToken)
        {
            var allObjectGroups = analysis.RelevantColumnsPerTableOrView;

           
            var relevantViewNames = analysis.RelevantViews?
                .OfType<Dictionary<string, object>>()
                .Select(v => v.TryGetValue("name", out var nameObj) && nameObj is string nameStr ? nameStr : null)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string?>();


            
            var relevantTableNames= analysis.RelevantTables?
                .OfType<Dictionary<string, object>>()
                .Select(t => t.TryGetValue("name", out var nameObj) && nameObj is string nameStr ? nameStr : null)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string?>();


            var tableGroups = allObjectGroups.Where(kvp => relevantTableNames.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var viewGroups = allObjectGroups.Where(kvp => relevantViewNames.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

           

            var tableBuilder = new StringBuilder();
            if (tableGroups.Any())
            {
                tableBuilder.AppendLine("### 1. Relevant Tables, Columns, and Conditions:");
                foreach (var group in tableGroups)
                {
                    await AppendObjectSchemaAsync(tableBuilder, group.Key, group.Value, conditionsByObject, cancellationToken);
                }
            }

            var viewBuilder = new StringBuilder();
            if (viewGroups.Any())
            {
                viewBuilder.AppendLine("\n### 2. Relevant Views, Columns, and Conditions:");
                viewBuilder.AppendLine("The following views are relevant and might simplify the query. A view is a pre-written query that can be used like a table.");
                foreach (var group in viewGroups)
                {
                    await AppendObjectSchemaAsync(viewBuilder, group.Key, group.Value, conditionsByObject, cancellationToken);
                }
            }

            return (tableBuilder.ToString(), viewBuilder.ToString());
        }

        private async Task AppendObjectSchemaAsync(StringBuilder builder, string objectName, List<ColumnDefinition> columns, Dictionary<string, List<ResolvedValueCondition>> conditionsByObject, CancellationToken cancellationToken)
        {
            builder.AppendLine($"\n#### OBJECT: `{objectName}`");
            builder.AppendLine("**Relevant Columns:**");
            foreach (var col in columns)
            {
                builder.AppendLine($"  - `{col.FullQualifiedTableName}.[{col.Name}]` (Type: `{col.Type}`, Description: {col.Description})");
            }

            if (conditionsByObject.TryGetValue(objectName, out var conditions) && conditions.Any())
            {
                builder.AppendLine("\n**Recommended WHERE Conditions:**");
                foreach (var condition in conditions)
                {
                    builder.AppendLine($"  - `{condition.ToWhereClauseString()}`");
                }
            }

            builder.AppendLine("\n**Sample Data (up to 3 rows):**");
            try
            {
                var sampleData = await _schemaService.GetSampleDataAsync(objectName, columns.Select(c => c.Name).ToList(), 3, cancellationToken);
                if (sampleData.Any())
                {
                    var headers = columns.Select(c => c.Name).ToList();
                    builder.AppendLine($"| {string.Join(" | ", headers.Select(h => $"`{h}`"))} |");
                    builder.AppendLine($"|{string.Join("|", headers.Select(_ => "---"))}|");
                    foreach (var row in sampleData)
                    {
                        var orderedRowValues = headers.Select(h => row.TryGetValue(h, out var v) ? v?.ToString()?.Replace("|", "\\|") ?? "NULL" : "...");
                        builder.AppendLine($"| {string.Join(" | ", orderedRowValues)} |");
                    }
                }
                else { builder.AppendLine("  (No sample data available)"); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sample data for {ObjectName}", objectName);
                // builder.AppendLine($"  (Error getting sample data: {ex.Message})");

                throw;
            }
        }

        private async Task<string> BuildJoinInfoAsync(UserQueryAnalysis analysis, string? rootTable, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            //relevant tables are list of Dictionaries<string, object> with "name" key

            var allRelevantTables = analysis.RelevantTables
                .OfType<Dictionary<string, object>>()
                .Select(t => t.TryGetValue("name", out var nameObj) && nameObj is string nameStr ? nameStr : null)
                
                .ToList();

            if (allRelevantTables.Count > 1)
            {
                string fromTable;
                if (!string.IsNullOrEmpty(rootTable) && allRelevantTables.Contains(rootTable, StringComparer.OrdinalIgnoreCase))
                {
                    fromTable = rootTable;
                    _logger.LogInformation("Using '{RootTable}' as the root table for JOIN path generation.", fromTable);
                }
                else
                {
                    fromTable = allRelevantTables.First()!; // First() is safe due to Count > 1 check
                    _logger.LogWarning("Could not find 'root_table' in state or it's not in the list of relevant tables. Falling back to using the first table '{FromTable}' for JOIN path generation.", fromTable);
                }
                var tablesToJoinTo = allRelevantTables.Where(t => t != null && !t.Equals(fromTable, StringComparison.OrdinalIgnoreCase)).ToList();
                var joinPathRelationships = await _schemaService.GetEntityRelationshipDiagramAsync(fromTable, tablesToJoinTo, cancellationToken);

                builder.AppendLine("\n### 3. Required JOIN Path:");
                builder.AppendLine("  To connect the tables, use the following JOINs:");

                if (joinPathRelationships.Any())
                {
                    foreach (var rel in joinPathRelationships)
                    {
                        builder.AppendLine($"  - Perform a **JOIN** between {rel.FromTable} and  {rel.ToTable} on keys {rel.ToSqlJoinHintString()}`");
                    }
                }

                if (analysis.ResolvedJoins.Any())
                {
                    
                    foreach (var resolvedJoin in analysis.ResolvedJoins)
                    {
                        var fromTableFqn = resolvedJoin.FromColumn.FullQualifiedTableName;
                        var toTableFqn = resolvedJoin.ToColumn.FullQualifiedTableName;
                        var fromColName = resolvedJoin.FromColumn.Name;
                        var toColName = resolvedJoin.ToColumn.Name;
                       

                        builder.AppendLine($"  - Perform a **JOIN** between `{fromTableFqn}` and `{toTableFqn}` on the condition: `{fromTableFqn}.[{fromColName}]` = `{toTableFqn}.[{toColName}]`.");
                    }
                }
                else
                {
                    _logger.LogWarning("Multiple tables are relevant, but no foreign keys or explicit JOIN suggestions were found. SQL generation might fail or be incorrect.");
                    builder.AppendLine("No foreign key relationships or explicit join suggestions were found. Only join tables if absolutely necessary based on the query context and column names.");
                }
            }
            return builder.ToString();
        }

        // private string BuildFunctionInfo(UserQueryAnalysis analysis, Dictionary<string,List<ResolvedValueCondition>> conditionsByObject)
        // {
        //     var builder = new StringBuilder();
        //     if (analysis.RelevantFunctions != null && analysis.RelevantFunctions.Any())
        //     {
        //         builder.AppendLine("\n### 4. Relevant Functions:");
        //         builder.AppendLine("If the user's query can be answered more directly or efficiently with a function, consider using it.");
        //         foreach (var func in analysis.RelevantFunctions)
        //         {
        //             builder.AppendLine($"#### `{func.ToString()}`");

        //             if (conditionsByObject.TryGetValue(func.FullQualifiedFunctionName, out var conditions) && conditions.Any())
        //             {
        //                 builder.AppendLine("\n**Recommended WHERE Conditions:**");
        //                 foreach (var condition in conditions)
        //                 {
                            
        //                     builder.AppendLine($"  - `{condition.ToWhereClauseString()}`");
        //                 }
        //             }
        //         }
        //     }
        //     return builder.ToString();
        // }

        private string BuildTableFunctionInfo(UserQueryAnalysis analysis, Dictionary<string,List<ResolvedValueCondition>> conditionsByObject)
        {
            var builder = new StringBuilder();
            if (analysis.RelevantFunctions != null && analysis.RelevantFunctions.Any())
            {
                builder.AppendLine("\n### 4. Relevant iTVF = Inline Table-Valued Functions (An inline table-valued function returns a table by using a single SELECT statement):");
                builder.AppendLine("If the user's query can be answered more directly or efficiently with a function, consider using it by passing parameters.");
                foreach (var func in analysis.RelevantFunctions)
                {
                    builder.AppendLine($"#### `{func.ToString()}`");

                    if (conditionsByObject.TryGetValue(func.FullQualifiedFunctionName, out var conditions) && conditions.Any())
                    {
                        builder.AppendLine("\n**Recommended WHERE Conditions:**");
                        foreach (var condition in conditions)
                        {
                            
                            builder.AppendLine($"  - `{condition.ToWhereClauseString()}`");
                        }
                    }
                }
            }
            return builder.ToString();
        }
    }
}