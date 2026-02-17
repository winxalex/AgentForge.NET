using Chat2Report.Agents.Generated.IO.DDLOntology;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Генерира онтолошки контекст во DDL (CREATE TABLE...) формат за табели,
    /// погледи и функции кои LLM ги идентификувал како релевантни.
    /// </summary>
    public class DDLOntologyStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataSchemaService _dataSchemaService;
        private readonly ILogger<DDLOntologyStep> _logger;

        public DDLOntologyStep(IDataSchemaService dataSchemaService, ILogger<DDLOntologyStep> logger)
        {
            _dataSchemaService = dataSchemaService;
            _logger = logger;
        }

        // Нема 'Config' секција
        public void Configure(JsonElement config) { }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Generating DDL ontology for relevant schema objects.");

            // Конвертирај List<object> во List<string> со имиња
            var tableNames = GetNamesFromObjectList(inputs.Tables);
            var viewNames = GetNamesFromObjectList(inputs.Views);
            var functionNames = GetNamesFromObjectList(inputs.Functions);

            // Генерирај DDL за табели и погледи
            string tablesDDL = await _dataSchemaService.GenerateTableOntologyAsDDLAsync(tableNames, cancellationToken).ConfigureAwait(false);
            string viewsDDL = await _dataSchemaService.GenerateViewOntologyAsDDLAsync(viewNames, cancellationToken).ConfigureAwait(false);

            // Одреди го root табелата
            string rootTableName = await _dataSchemaService.DetectRootTableAsync(tableNames).ConfigureAwait(false);

            // Филтрирај ги целосните дефиниции на функции само за оние што LLM-от ги избрал
            var relevantFunctionDefs = inputs.AllFunctionDefinitions           
                .Where(f => f.FullQualifiedFunctionName != null && functionNames.Contains(f.FullQualifiedFunctionName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            _logger.LogDebug("Generated DDL for {tCount} tables, {vCount} views and identified {fCount} relevant functions.",
                new object[] { tableNames.Count, viewNames.Count, relevantFunctionDefs.Count });

            return new Outputs
            {
                TablesOntology = tablesDDL,
                ViewsOntology = viewsDDL,
                RootTable = rootTableName,
                RelevantFunctionDefinitions = relevantFunctionDefs
            };
        }

        /// <summary>
        /// Помошен метод за извлекување на 'name' пропертито од листа
        /// на објекти вратени од LLM (кои се обично речници или JsonElement-и).
        /// </summary>
        private List<string> GetNamesFromObjectList(IEnumerable<object>? objectList)
        {
            if (objectList == null) return new List<string>();

            var names = new List<string>();
            foreach (var item in objectList)
            {
                if (item is JsonElement jsonElem &&
                    jsonElem.TryGetProperty("name", out var nameProp) &&
                    nameProp.ValueKind == JsonValueKind.String)
                {
                    names.Add(nameProp.GetString()!);
                }
                else if (item is Dictionary<string, object> dict &&
                         dict.TryGetValue("name", out var nameObj) &&
                         nameObj is string nameStr)
                {
                    names.Add(nameStr);
                }
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}