using System;
using Microsoft.Extensions.Logging;

namespace Chat2Report.Services.Chart
{
    public class ChartRenderingService : IChartRenderingService
    {
        private readonly IChartRenderer _genericRenderer;
        private readonly ILogger<ChartRenderingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartRenderingService"/> class.
        /// Since we use a single generic renderer, we inject it directly.
        /// </summary>
        /// <param name="genericRenderer">The generic chart renderer.</param>
        /// <param name="logger">The logger.</param>
        public ChartRenderingService(IChartRenderer genericRenderer, ILogger<ChartRenderingService> logger)
        {
            _logger = logger;
            _genericRenderer = genericRenderer ?? throw new ArgumentNullException(nameof(genericRenderer));
            _logger.LogInformation("ChartRenderingService initialized with a generic renderer.");
        }

        public IChartRenderer GetRenderer(string libraryName)
        {
            // Since we have a single generic renderer that handles all types, we always return it.
            _logger.LogDebug("Returning generic renderer for library '{LibraryName}'.", libraryName);
            return _genericRenderer;
        }
    }
}
