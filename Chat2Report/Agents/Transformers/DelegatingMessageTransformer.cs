﻿using Chat2Report.Models;
using Chat2Report.Providers;
using System.Threading.Tasks;

namespace Chat2Report.Agents.Transformers
{
    public class DelegatingMessageTransformer : IMessageTransformer
    {
        private readonly JsonataTransformer _jsonataTransformer;
        private readonly DIMessageTransformer _diStepExecutor;
        private readonly ReflectionMessageTransformer _reflectionMessageTransformer;
        private readonly ILogger<DelegatingMessageTransformer> _logger;

        public DelegatingMessageTransformer(
            JsonataTransformer jsonataTransformer,
            DIMessageTransformer diStepExecutor,
            ReflectionMessageTransformer reflectionMessageTransformer,
            ILogger<DelegatingMessageTransformer> logger)
        {
            _jsonataTransformer = jsonataTransformer;
            _diStepExecutor = diStepExecutor;
            _reflectionMessageTransformer = reflectionMessageTransformer;
            _logger = logger;
        }

        public Task<Dictionary<string, object>> TransformAsync(TransformOptions options, Dictionary<string, object> message,  IStepExecutionContext transformContext, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(options.Expression))
            {
                _logger.LogDebug("Delegating transformation to JsonataTransformer for expression: {Expression}", options.Expression);
                return _jsonataTransformer.TransformAsync(options, message, transformContext,cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(options.ServiceType))
            {
                _logger.LogDebug("Delegating transformation to DIStepExecutorTransformer for service: {ServiceType}", options.ServiceType);
                return _diStepExecutor.TransformAsync(options, message,transformContext,cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(options.AssemblyQualifiedName) && !string.IsNullOrWhiteSpace(options.Method))
            {
                _logger.LogDebug("Delegating transformation to ReflectionMessageTransformer for method: {Method}", options.Method);
                return _reflectionMessageTransformer.TransformAsync(options, message, transformContext,cancellationToken);
            }

            _logger.LogWarning("TransformOptions provided but no valid configuration (Expression or Assembly/Method) found. Returning original message.");
            return Task.FromResult(message);
        }

       
    }
}