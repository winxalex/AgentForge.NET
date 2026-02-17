﻿// --- START OF FILE Agents/WorkflowSteps/DisplayUIStep.cs (FINAL VERSION) ---
using Chat2Report.Agents.Generated.IO.DisplayUI;
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Providers;
using Chat2Report.Services;
using Chat2Report.Agents.Generated.IO.DisplayUI;
using HandlebarsDotNet;
using Microsoft.Extensions.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// Опции кои се дефинираат во "Config" секцијата на 'DisplayUIStep' во appsettings.json
    /// за да се контролира рендерирањето.
    /// </summary>
    public class DisplayUIStepOptions
    {
        public DisplayContentRenderOptions? TextSource { get; set; }
        public DisplayContentRenderOptions? ReasoningContent { get; set; }
        public DisplayContentRenderOptions? HtmlContent { get; set; }
    }

    public class DisplayContentRenderOptions
    {
        public bool Enabled { get; set; } = true;
        public string? Template { get; set; }
    }

    /// <summary>
    /// Workflow чекор кој рендерира различни типови на содржина и ги испраќа кон корисничкиот интерфејс.
    /// Работи со силно-типизирани влезови и нема излези.
    /// </summary>
    public class DisplayUIStep : IWorkflowStep<Inputs, Outputs>,IConfigurableStep
    {
        private readonly IUIStreamBroker _broker;
        private readonly IHandlebars _handlebars;
        private readonly ILogger<DisplayUIStep> _logger;
        private DisplayUIStepOptions _options = new();

        public DisplayUIStep(IUIStreamBroker broker, ILogger<DisplayUIStep> logger)
        {
            _broker = broker;
            _logger = logger;
            _handlebars = CreateHandlebarsInstance();
        }

        public void Configure(JsonElement config)
        {
            _options = config.DeserializeConfig<DisplayUIStepOptions>();
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(context.WorkflowTopicId))
            {
                _logger.LogError("Cannot display to UI without 'WorkflowTopicId'. Skipping step.");
                throw new InvalidOperationException("Cannot display to UI without 'WorkflowTopicId'.");
            }

            var staticContents = new List<AIContent>();
            string? fullTextOutput = null;

            // === 1. ОБРАБОТИ ГИ СИТЕ ТИПОВИ НА СОДРЖИНА ===
            Task<string>? task = null;
            // Главен Текст (може да е стрим или статичен) - ова е асинхроно
            task = ProcessTextSource(inputs, staticContents, context.WorkflowTopicId, cancellationToken);

            // Останатите типови содржина
            if (_options.ReasoningContent?.Enabled != false && inputs.ReasoningContent is not null)
            {
                ProcessReasoning(inputs.ReasoningContent, staticContents, inputs);
            }
            if (_options.HtmlContent?.Enabled != false && inputs.HtmlContent is not null)
            {
                ProcessHtml(inputs.HtmlContent, staticContents, inputs);
            }
            if (inputs.TableContent is not null)
            {
                ProcessTable(inputs.TableContent, staticContents);
            }
            if (inputs.ChartContent is not null)
            {
                ProcessChart(inputs.ChartContent, staticContents);
            }

            // === 2. ИСПРАТИ ГИ СОБРАНИТЕ СТАТИЧНИ СОДРЖИНИ ===
            if (staticContents.Any())
            {
                var payload = new StreamPayload { WorkflowTopicId = context.WorkflowTopicId, Contents = staticContents };
                await _broker.BroadcastMessageAsync(payload, cancellationToken).ConfigureAwait(false);
            }

            // === 3. ЧЕКАЈ СТРИМИНГОТ ДА ЗАВРШИ (АКО ПОСТОИ) ===
            if (task is not null)
            {
                fullTextOutput = await task.ConfigureAwait(false);
            }

            return new Outputs { FormattedText = fullTextOutput };
        }

        #region Content Processors

        private Task<string>? ProcessTextSource(Inputs inputs, List<AIContent> staticContents, string workflowTopicId, CancellationToken cancellationToken)
        {
            if (_options.TextSource?.Enabled == false || inputs.TextSource is null) return null;

            switch (inputs.TextSource)
            {
                case StreamableResult streamableResult:
                    return StreamAndMaterializeAsync(streamableResult.GetStream(), workflowTopicId, cancellationToken);
            
                case string staticText:
                    string displayText = RenderTemplate(_options.TextSource?.Template, inputs, staticText);
                    if (!string.IsNullOrEmpty(displayText))
                        staticContents.Add(new TextContent(displayText));
                    break;
                default:
                    _logger.LogWarning("TextSource input was of an unexpected type '{Type}'. Skipping.", inputs.TextSource.GetType().Name);
                    break;
            }
            // Ако текстот е статичен, го враќаме како веќе завршена задача.
            // Ова осигурува дека fullTextOutput во ExecuteAsync ќе ја добие вредноста.
            return Task.FromResult(staticContents.LastOrDefault(c => c is TextContent)?.ToString());
        }

        private void ProcessReasoning(string reasoningString, List<AIContent> contents, Inputs context)
        {
            if (string.IsNullOrWhiteSpace(reasoningString)) return;
            string displayReasoning = RenderTemplate(_options.ReasoningContent?.Template, context, reasoningString);
            contents.Add(new TextReasoningContent(displayReasoning));
        }

        private void ProcessHtml(object data, List<AIContent> contents, Inputs context)
        {
            if (data is string htmlString && !string.IsNullOrWhiteSpace(htmlString))
            {
                string displayHtml = RenderTemplate(_options.HtmlContent?.Template, context, htmlString);
                contents.Add(new DynamicHtmlContent(displayHtml));
            }
            else
            {
                _logger.LogWarning("HtmlContent input was of type '{Type}' but expected a string. Skipping.", data.GetType().Name);
            }
        }

        private void ProcessTable(IReadOnlyList<IReadOnlyDictionary<string, object?>> tableData, List<AIContent> contents)
        {
            if (tableData.Any())
            {
                contents.Add(new TableContent(tableData));
            }
        }

        private void ProcessChart(ChartContent chartData, List<AIContent> contents)
        {
            _logger.LogInformation("DisplayUIStep: Processing ChartContent to be sent to UI. Chart Type: {ChartType}, Library: {Library}", chartData.ChartType, chartData.Library);
            contents.Add(chartData);
        }

        #endregion

        #region Helper Methods

        

        private async Task<string> StreamAndMaterializeAsync(IAsyncEnumerable<string> stream, string workflowTopicId, CancellationToken cancellationToken)
        {
            var fullTextBuilder = new StringBuilder();
            _logger.LogInformation("Streaming text content to UI for topic '{TopicId}'.", workflowTopicId);
            try
            {
                await foreach (var chunk in stream.WithCancellation(cancellationToken))
                {
                    if (string.IsNullOrEmpty(chunk)) continue;

                    fullTextBuilder.Append(chunk);
                    var payload = new StreamPayload { WorkflowTopicId = workflowTopicId, Contents = new List<AIContent> { new TextContent(chunk) } };
                    _ = _broker.BroadcastMessageAsync(payload, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Streaming was canceled for topic '{TopicId}'.", workflowTopicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during UI streaming for topic '{TopicId}'.", workflowTopicId);
                var payload = new StreamPayload { WorkflowTopicId = workflowTopicId, Contents = new List<AIContent> { new ErrorContent($"[Грешка при стриминг: {ex.Message}]") } };
                _ = _broker.BroadcastMessageAsync(payload, CancellationToken.None);
            }
            _logger.LogDebug("Finished streaming content to UI for topic '{TopicId}'.", workflowTopicId);
            return fullTextBuilder.ToString();
        }

    

        private string RenderTemplate(string? template, object dataContext, string fallback)
        {
            if (string.IsNullOrWhiteSpace(template)) return fallback;
            try
            {
                return _handlebars.Compile(template)(dataContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile or execute display template: {Template}", template);
                return $"_Грешка при рендерирање на темплејт: {ex.Message}_";
            }
        }

        private static IHandlebars CreateHandlebarsInstance()
        {
            var handlebars = Handlebars.Create();
            handlebars.RegisterHelper("isNotEmpty", (writer, context, arguments) =>
            {
                if (arguments.Length > 0 && arguments[0] is IEnumerable enumerable && enumerable.Cast<object>().Any())
                {
                    writer.Write("true");
                }
            });
            handlebars.RegisterHelper("toJSON", (writer, context, arguments) =>
            {
                if (arguments.Length > 0 && arguments[0] is not null)
                {
                    writer.WriteSafeString(JsonSerializer.Serialize(arguments[0], new JsonSerializerOptions { WriteIndented = true }));
                }
            });
            return handlebars;
        }

        

        #endregion
    }
}
// --- END OF FILE Agents/WorkflowSteps/DisplayUIStep.cs (FINAL VERSION) ---
