﻿﻿﻿﻿﻿using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Providers;
using Chat2Report.Services;

namespace Chat2Report.Agents.WorkflowSteps
{

    public class JSONOntologyStep : IBaseWorkflowStep
    {

        private readonly IDataSchemaService _dataSchemaService;
       

        private readonly ILogger<JSONOntologyStep> _logger;

        public JSONOntologyStep(

      IDataSchemaService dataSchemaService,

      ILogger<JSONOntologyStep> logger)
        {

            _dataSchemaService = dataSchemaService;

            _logger = logger;
        }



        public async Task<WorkflowState> ExecuteAsync(WorkflowState state, IStatePersitanceProvider memory,IStepExecutionContext context, CancellationToken cancellationToken = default)
        {

            if (!state.Data.TryGetValue("user_query", out var userQueryObj) || userQueryObj is not string userQuery)
            {
                _logger.LogError("State is missing 'user_query' or it's not a string.");
                throw new ArgumentException("State is missing 'user_query' or it's not a string.");
            }

            if (!state.Data.TryGetValue("relevant_tables", out var tablesObj) || tablesObj is not List<object> tables)
            {
                _logger.LogError("State is missing 'relevant_tables' or it's not a List of strings");
                throw new ArgumentException("State is missing 'relevant_tables' or it's not a List of strings");
            }

            if (!state.Data.TryGetValue("relevant_views", out var viewsObj) || viewsObj is not List<object> views)
            {
                _logger.LogError("State is missing 'relevant_views' or it's not a List of strings");
                throw new ArgumentException("State is missing 'relevant_views' or it's not a List of strings");

            }

            //table is List<object> where every object is Dictionary<string, object> 
            List<string> tableNames = tables.OfType<Dictionary<string,object>>()
                .Where(d => d.TryGetValue("name", out var nameObj) && nameObj is string)
                .Select(d => d["name"].ToString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> viewNames = views.OfType<Dictionary<string,object>>()
                .Where(d => d.TryGetValue("name", out var nameObj) && nameObj is string)
                .Select(d => d["name"].ToString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            //Get root table name
            string rootTableName = await _dataSchemaService.DetectRootTableAsync(tableNames);

            //Get relevant functions_definitions from state
            if (!state.Data.TryGetValue("relevant_functions", out var functionsNamesObj) || functionsNamesObj is not List<object> functions)
            {
                _logger.LogError("State is missing 'relevant_functions' or it's not List of strings");
                throw new ArgumentException("State is missing 'relevant_functions' or it's not List of strings");
            }


            // Get function definitions from state
            if (!state.Data.TryGetValue("all_function_definitions", out var functionsObj) || functionsObj is not List<FunctionDefinition> functions_definitions)
            {
                _logger.LogError("State is missing 'all_function_definitions' or it's not List of TableFunction");
                throw new ArgumentException("State is missing 'all_function_definitions' or it's not List of TableFunction");
            }

            List<string> functionNames = functions.OfType<Dictionary<string,object>>()
                .Where(d => d.TryGetValue("name", out var nameObj) && nameObj is string)
                .Select(d => d["name"].ToString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();


            string tableOntologyJSON = await _dataSchemaService.GenerateTableOntologyAsJsonAsync(tables, rootTableName, cancellationToken);

            
            string viewsOntologyJSON = await _dataSchemaService.GenerateViewOntologyAsJsonAsync(views, cancellationToken);

            List<FunctionDefinition> relevantFunctionDefinitions=
                //from all_function definitions only those names are in relevant_functions
                functions_definitions.Where(f => f.FullQualifiedFunctionName != null && functionNames.Contains(f.FullQualifiedFunctionName)).ToList();


            var finalStateData = new Dictionary<string, object>(state.Data);

            finalStateData.Remove("all_function_definitions");

            finalStateData["tables_ontology_map"] = tableOntologyJSON;
            finalStateData["views_ontology_map"] = viewsOntologyJSON;
            finalStateData["relevant_function_definitions"] = relevantFunctionDefinitions;
            finalStateData["root_table"] = rootTableName;
            finalStateData["current_datetime"] = DateTime.Now.ToString("dd.MM.yyyy HH:mm.ss");

            return new WorkflowState { WorkflowTopicId = state.WorkflowTopicId, Data = finalStateData };

        }
    }
}
