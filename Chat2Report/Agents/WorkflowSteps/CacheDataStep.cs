﻿using Chat2Report.Agents.Generated.IO.CacheData;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class CacheDataStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataCacheService _cacheService;
        private readonly ILogger<CacheDataStep> _logger;

        public CacheDataStep(IDataCacheService cacheService, ILogger<CacheDataStep> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (inputs.DataToCache is null)
            {
                _logger.LogWarning("Input 'dataToCache' is null. Caching will not be performed.");
                return Task.FromResult(new Outputs { DataKey = null });
            }

            // Секогаш кеширај без временско ограничување (TTL), според новата логика.
            var key = _cacheService.Set(inputs.DataToCache, null);

            _logger.LogInformation("Data cached successfully with key '{CacheKey}'.", key);

            return Task.FromResult(new Outputs { DataKey = key });
        }
    }
}
