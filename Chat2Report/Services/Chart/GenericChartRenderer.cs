using Chat2Report.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Chat2Report.Services.Chart
{
    /// <summary>
    /// A generic renderer that can handle multiple chart libraries. It uses configuration
    /// to determine the correct JavaScript function to invoke for rendering.
    /// </summary>
    public class GenericChartRenderer : IChartRenderer
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IOptions<ChartingSettings> _chartingSettings;
        private readonly ILogger<GenericChartRenderer> _logger;

        public GenericChartRenderer(IJSRuntime jsRuntime, IOptions<ChartingSettings> chartingSettings, ILogger<GenericChartRenderer> logger)
        {
            _jsRuntime = jsRuntime;
            _chartingSettings = chartingSettings;
            _logger = logger;
        }

        public async Task RenderAsync(string containerId, ChartContent chartContent)
        {
            if (chartContent is null)
            {
                _logger.LogWarning("ChartContent is null, cannot render chart for container '{ContainerId}'.", containerId);
                return;
            }

            // Dynamically assign the JS render function from configuration
            var supportedChart = _chartingSettings.Value.SupportedCharts
                .FirstOrDefault(c => c.Type.Equals(chartContent.ChartType, StringComparison.OrdinalIgnoreCase));


            if (supportedChart is null || string.IsNullOrWhiteSpace(supportedChart.Function))
            {
                var errorMessage = $"Unsupported chart type '{chartContent.ChartType}' for library '{chartContent.Library}' or render function not configured.";
                _logger.LogError(errorMessage);
                // Optionally, render an error message in the UI.
                await _jsRuntime.InvokeVoidAsync("renderChartError", containerId, errorMessage);
                return;
            }

            _logger.LogDebug("Rendering chart '{ChartType}' from library '{ChartLibrary}' in container '{ContainerId}' using JS function '{JsFunction}'.",
                chartContent.ChartType, chartContent.Library, containerId, supportedChart.Function);

            await _jsRuntime.InvokeVoidAsync(supportedChart.Function, containerId, chartContent.Options);
        }
    }
}