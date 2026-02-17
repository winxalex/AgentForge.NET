using Chat2Report.Agents.Evaluation;
using HandlebarsDotNet;
using System;
using System.Text.Json;

namespace Chat2Report.Services
{
    public class HandlebarsTemplateEngine : ITemplateEngine
    {
        private readonly IHandlebars _handlebars;
        private readonly IExpressionEvaluator _expressionEvaluator;

        public HandlebarsTemplateEngine(IExpressionEvaluator expressionEvaluator)
        {
            _handlebars = Handlebars.Create();
            _expressionEvaluator = expressionEvaluator;
            RegisterHelpers();
        }

        public string Compile(string template, object data)
        {
            var compiledTemplate = _handlebars.Compile(template);
            return compiledTemplate(data);
        }

        private void RegisterHelpers()
        {
            _handlebars.RegisterHelper("now", (writer, context, parameters) =>
            {
                writer.Write(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            });

            _handlebars.RegisterHelper("toJSON", (writer, context, arguments) =>
            {
                if (arguments.Length > 0 && arguments[0] is not null)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                    writer.WriteSafeString(JsonSerializer.Serialize(arguments[0], options));
                }
            });

            // 
            //Example:
            //{{ toJSONata supported_charts  '$map($, function($v) { { "Type": $v.Type, "Usage": $v.Usage } })' }}
            _handlebars.RegisterHelper("toJSONata", (writer, context, arguments) =>
            {
                if (arguments.Length != 2)
                {
                    writer.Write("Error: toJSONata helper requires 2 arguments (source object, jsonata expression).");
                    return;
                }

                var sourceArgument = arguments[0];
                var expression = arguments[1] as string;

                if (expression is null)
                {
                    writer.Write("Error: toJSONata expression (argument 2) must be a string.");
                    return;
                }

                var jsonString = string.Empty;
                try
                {
                    // Ensure the source is a generic JSON-like object that Jsonata can process.
                    // If it's already a string, assume it's a JSON string and deserialize it.
                    
                     jsonString = sourceArgument is string s ? s : JsonSerializer.Serialize(sourceArgument);
                   
                }
                catch (Exception ex)
                {
                    writer.Write($"Error preparing data for toJSONata: {ex.Message}");
                    return;
                }

                try
                {
                    // Use the dedicated method for string-based transformation
                    var result = _expressionEvaluator.EvaluateAsString(expression, jsonString);
                    writer.WriteSafeString(result);
                }
                catch (Exception ex)
                {
                    writer.Write($"Error evaluating toJSONata expression: {ex.Message}");
                }
            });
        }
    }
}