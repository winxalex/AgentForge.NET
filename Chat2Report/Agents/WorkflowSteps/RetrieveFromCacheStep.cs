﻿using Chat2Report.Agents.Generated.IO.RetrieveFromCache;
using Chat2Report.Models;
using Chat2Report.Services;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    public class RetrieveFromCacheStep : IWorkflowStep<Inputs, Outputs>, IConfigurableStep
    {
        private readonly IDataCacheService _cacheService;
        private readonly ILogger<RetrieveFromCacheStep> _logger;

        public RetrieveFromCacheStep(IDataCacheService cacheService, ILogger<RetrieveFromCacheStep> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public void Configure(JsonElement config) { }

        public Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext? context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputs.DataKey))
            {
                throw new ArgumentException("Input 'dataKey' cannot be null or empty.", nameof(inputs.DataKey));
            }

            var data = _cacheService.Get<object>(inputs.DataKey);
            if (data == null)
            {
                throw new KeyNotFoundException($"Data not found in cache for key '{inputs.DataKey}'.");
            }

            _logger.LogInformation("Successfully retrieved data from cache with key '{CacheKey}'.", inputs.DataKey);
            return Task.FromResult(new Outputs { RetrievedData = data });
        }
    }
}
