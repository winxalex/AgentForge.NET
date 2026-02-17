﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chat2Report.SourceGen
{
    [Generator]
    public class StateKeysAndContractsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Најпрво - тест дали генераторот воопшто работи
            context.RegisterPostInitializationOutput(ctx =>
            {
                var initLog = @"/*
==============================================
GENERATOR INITIALIZED
==============================================
This proves the generator is loaded and running.
If you see this file, the generator setup is correct.
*/";
                ctx.AddSource("_Init.log.g.cs", SourceText.From(initLog, Encoding.UTF8));
            });





            try
            {
                // Земи ги сите AdditionalFiles
                var additionalFilesProvider = context.AdditionalTextsProvider.Where(f =>
            f != null
            && !string.IsNullOrEmpty(f.Path)
            && (f.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase)))
            //|| f.Path.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase)))
            .Collect();

                // Регистрирај го главниот execution
                context.RegisterSourceOutput(additionalFilesProvider, (spc, files) => Execute(spc, files));
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());

                if (!Debugger.IsAttached) Debugger.Launch();


                Debugger.Log(1, "Error", ex.ToString());
                Debugger.Break();





            }



            //var jsonFiles = context.AdditionalTextsProvider
            //   .Where(f => f.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
            //               f.Path.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase));

            //context.RegisterSourceOutput(jsonFiles.Collect(), (spc, files) => Execute(spc, files));
        }

        private void Execute(SourceProductionContext spc, System.Collections.Immutable.ImmutableArray<AdditionalText> files)
        {
            var log = new StringBuilder();

            try
            {
                log.AppendLine("==============================================");
                log.AppendLine($"EXECUTION STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                log.AppendLine("==============================================");
                log.AppendLine();

                // Испечати ги СИТЕ additional files
                log.AppendLine($"[INFO] Total AdditionalFiles received: {files.Length}");
                log.AppendLine();

                if (files.Length == 0)
                {
                    log.AppendLine("[WARNING] NO AdditionalFiles found!");
                    log.AppendLine();
                    log.AppendLine("This means your .csproj is missing:");
                    log.AppendLine("  <ItemGroup>");
                    log.AppendLine("    <AdditionalFiles Include=\"appsettings.json\" />");
                    log.AppendLine("    <AdditionalFiles Include=\"appsettings.Development.json\" />");
                    log.AppendLine("  </ItemGroup>");
                    log.AppendLine();
                    log.AppendLine("Add this to your .csproj and rebuild!");
                }
                else
                {
                    log.AppendLine("[FILES FOUND]");
                    for (int i = 0; i < files.Length; i++)
                    {
                        var file = files[i];
                        log.AppendLine($"  [{i + 1}] Path: {file.Path}");

                        try
                        {
                            var content = file.GetText(spc.CancellationToken)?.ToString();
                            if (content == null)
                            {
                                log.AppendLine($"      [ERROR] Content is NULL");
                            }
                            else if (string.IsNullOrWhiteSpace(content))
                            {
                                log.AppendLine($"      [ERROR] Content is EMPTY");
                            }
                            else
                            {
                                log.AppendLine($"      [SUCCESS] Length: {content.Length} characters");
                                log.AppendLine($"      [PREVIEW] First 200 chars:");
                                log.AppendLine("      " + content.Substring(0, Math.Min(200, content.Length)).Replace("\n", "\n      "));
                                log.AppendLine("      ...");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debugger.Break();

                            if (Debugger.IsAttached)
                                Debugger.Log(1, "Error", $"Failed to read {file.Path}: {ex}\n");
                            log.AppendLine($"      [ERROR] Failed to read: {ex.Message}");
                        }

                        log.AppendLine();
                    }
                }

                // Филтрирај само appsettings фајлови
                // var appSettingsFiles = files.Where(f =>
                //     f.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase) ||
                //     f.Path.EndsWith("appsettings.Development.json", StringComparison.OrdinalIgnoreCase)
                // ).ToArray();

                //log.AppendLine($"[FILTERED] Found {appSettingsFiles.Length} appsettings.json files");

                //if (appSettingsFiles.Length == 0)
                if (files.Length == 0)
                {
                    log.AppendLine("[WARNING] No appsettings.json files found in AdditionalFiles!");
                    log.AppendLine("Make sure your files are named exactly:");
                    log.AppendLine("  - appsettings.json");
                    log.AppendLine("  - appsettings.Development.json");
                }
                else
                {
                    var allStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var allStepContracts = new Dictionary<string, JsonElement>();


                    //foreach (var file in appSettingsFiles) should be 

                    var file = files[0]; // Only one appsettings.json for now

                    log.AppendLine($"[PROCESSING] {file.Path}");

                    try
                    {
                        var content = file.GetText(spc.CancellationToken)?.ToString();

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            log.AppendLine("  [SKIP] File is empty");
                            if (Debugger.IsAttached)
                                Debugger.Log(1, "Warning", $"File {file.Path} is empty\n");

                            return;
                        }

                        log.AppendLine($"  [SUCCESS] Read {content.Length} characters");

                        // Ovде може да додадете JSON parsing
                        // За сега само покажуваме дека можеме да го прочитаме


                        if (Debugger.IsAttached)
                            Debugger.Log(1, "Info", $"Content of {file.Path}:\n{content}\n");



                        //Debugger.Break();




                        using var doc = JsonDocument.Parse(content, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

                        if (doc == null)
                        {
                            if (Debugger.IsAttached)
                                Debugger.Log(1, "Error", $"Failed to parse JSON in {file.Path}\n");
                            return;
                        }

                        if (!doc.RootElement.TryGetProperty("AgentsConfiguration", out var agentsConfig))
                        {
                            if (Debugger.IsAttached)
                                Debugger.Log(1, "Error", $"Failed to parse JSON in {file.Path}\n");



                            return;
                        }

                        // === НОВА ЛОГИКА #1: Читај ги договорите од 'StepDefinitions' ===
                        if (agentsConfig.TryGetProperty("StepDefinitions", out var stepDefinitions))
                        {
                            foreach (var contractProp in stepDefinitions.EnumerateObject())
                            {
                                var stepType = contractProp.Name;
                                if (contractProp.Value.TryGetProperty("IOContract", out var contractJson))
                                {
                                    allStepContracts[stepType] = contractJson.Clone();
                                }
                            }
                        }

                        // === НОВА ЛОГИКА #2: Читај ги клучевите од 'Workflows' ===
                        if (agentsConfig.TryGetProperty("Workflows", out var workflows))
                        {
                            foreach (var workflowProp in workflows.EnumerateObject())
                            {
                                if (!workflowProp.Value.TryGetProperty("Agents", out var agents)) continue;

                                foreach (var agentProp in agents.EnumerateObject())
                                {
                                    if (!agentProp.Value.TryGetProperty("Steps", out var steps)) continue;

                                    foreach (var step in steps.EnumerateArray())
                                    {
                                        if (step.TryGetProperty("InstanceConfiguration", out var instanceConfig) &&
                                            instanceConfig.TryGetProperty("OutputMapping", out var outputs))
                                        {
                                            foreach (var output in outputs.EnumerateObject())
                                            {
                                                if (output.Value.TryGetProperty("key", out var keyProp) && keyProp.ValueKind == JsonValueKind.String)
                                                {
                                                    allStateKeys.Add(keyProp.GetString()!);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }


                        // ГЕНЕРИРАЈ StateKeys.g.cs
                        if (allStateKeys.Any())
                        {
                            spc.AddSource("StateKeys.g.cs", SourceText.From(GenerateStateKeysClass(allStateKeys), Encoding.UTF8));
                        }

                        // ГЕНЕРИРАЈ *.IO.g.cs датотеки
                        foreach (var kvp in allStepContracts)
                        {
                            spc.AddSource($"{kvp.Key}.IO.g.cs", SourceText.From(GenerateIOContractClasses(kvp.Key, kvp.Value), Encoding.UTF8));
                        }


                    }
                    catch (Exception ex)
                    {

                        //Doesn't display in output window
                        //           var descriptor = new DiagnosticDescriptor(
                        //id: "GEN100",
                        //title: "File Read ",
                        //messageFormat: $"FILE path '{file.Path}': {ex.Message}",
                        //category: "Chat2Report.SourceGen",
                        //DiagnosticSeverity.Error,
                        //isEnabledByDefault: true);

                        //           spc.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));

                        if (!Debugger.IsAttached) Debugger.Launch();
                        Debugger.Break();

                        Debugger.Log(1, "Error", $"Failed to process {file.Path}: {ex}\n");

                        log.AppendLine($"  [ERROR]  {ex?.Message}{ex?.InnerException}");
                        log.AppendLine($"  [STACK] {ex.StackTrace}");
                    }

                }

                log.AppendLine();
                log.AppendLine("==============================================");
                log.AppendLine("EXECUTION COMPLETED");
                log.AppendLine("==============================================");

                // Генерирај тест класа за да покажеме дека генераторот работи
                var testClass = $@"// <auto-generated/>
namespace Chat2Report.Generated
{{
    public static class AppsettingsTest
    {{
        public const string GeneratedAt = ""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"";
        public const int FilesFound = {files.Length};
        public const int AppsettingsFilesFound = {files.Length};
    }}
}}";

                spc.AddSource("AppsettingsTest.g.cs", SourceText.From(testClass, Encoding.UTF8));
                log.AppendLine("[GENERATED] AppsettingsTest.g.cs");
            }
            catch (Exception ex)
            {
                log.AppendLine();
                log.AppendLine("==============================================");
                log.AppendLine("FATAL ERROR");
                log.AppendLine("==============================================");
                log.AppendLine($"Type: {ex.GetType().FullName}");
                log.AppendLine($"Message: {ex.Message}");
                log.AppendLine($"Stack Trace:");
                log.AppendLine(ex.StackTrace ?? "N/A");


                if (ex.InnerException != null)
                {
                    log.AppendLine();
                    log.AppendLine("Inner Exception:");
                    log.AppendLine($"  Type: {ex.InnerException.GetType().FullName}");
                    log.AppendLine($"  Message: {ex.InnerException.Message}");
                }
            }

            // СЕКОГАШ зачувај го логот
            try
            {
                var logContent = $"/*\n{log}\n*/";
                spc.AddSource("Execution.log.g.cs", SourceText.From(logContent, Encoding.UTF8));
            }
            catch (Exception logEx)
            {
                try
                {
                    var emergency = $"/* EMERGENCY: {logEx.Message}\n\nOriginal log:\n{log} */";
                    spc.AddSource("EMERGENCY.log.g.cs", SourceText.From(emergency, Encoding.UTF8));
                }
                catch { /* Give up */ }
            }
        }


        private string GenerateStateKeysClass(IEnumerable<string> keys)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace Chat2Report.Models.State;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Contains compile-time safe constants for all workflow state keys");
            sb.AppendLine("/// discovered in appsettings.json files. This prevents typos and");
            sb.AppendLine("/// enables easy refactoring.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static class StateKeys");
            sb.AppendLine("{");

            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().OrderBy(k => k))
            {
                var propertyName = ToPascalCase(key);
                sb.AppendLine($"    public const string {propertyName} = \"{key}\";");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateIOContractClasses(string stepType, JsonElement contract)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Chat2Report.Models; // Додаваме основни using-и");
            sb.AppendLine();
            sb.AppendLine($"namespace Chat2Report.Agents.Generated.IO.{stepType}");
            sb.AppendLine("{");

            // Генерирај класа за влез (Inputs)
            if (contract.TryGetProperty("Inputs", out var inputs))
            {
                sb.AppendLine($"    public class Inputs");
                sb.AppendLine("    {");
                foreach (var inputProp in inputs.EnumerateObject())
                {
                    string propName = ToPascalCase(inputProp.Name);
                    string propType = inputProp.Value.GetString() ?? "object";
                    sb.AppendLine($"        public {propType} {propName} {{ get; set; }}");
                }
                sb.AppendLine("    }");
            }

            // Генерирај класа за излез (Outputs)
            if (contract.TryGetProperty("Outputs", out var outputs))
            {
                sb.AppendLine($"    public class Outputs");
                sb.AppendLine("    {");
                foreach (var outputProp in outputs.EnumerateObject())
                {
                    string propName = ToPascalCase(outputProp.Name);
                    string propType = outputProp.Value.GetString() ?? "object";
                    sb.AppendLine($"        public {propType} {propName} {{ get; set; }}");
                }
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static readonly Regex InvalidCharsRegex = new Regex(@"[^a-zA-Z0-9_]", RegexOptions.Compiled);
        private string ToPascalCase(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return string.Empty;

            // Split on underscores, spaces, or hyphens. Also, split before an uppercase letter that is not at the start.
            var parts = Regex.Split(str, @"(?<=[a-z])(?=[A-Z])|[_\s\-]+");
            string result = string.Concat(parts.Select(p =>
                p.Length > 0
                ? char.ToUpperInvariant(p[0]) + p.Substring(1) // Don't force the rest to lower case
                : ""
            ));

            if (string.IsNullOrEmpty(result))
                return "_"; // Return a valid identifier if the result is empty

            return result;
        }
    }
}