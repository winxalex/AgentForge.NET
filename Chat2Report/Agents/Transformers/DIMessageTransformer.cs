using Chat2Report.Agents.WorkflowSteps;
using Chat2Report.Models;
using Chat2Report.Providers;
using System.Threading.Tasks;

namespace Chat2Report.Agents.Transformers
{
    /// <summary>
    /// An IMessageTransformer that resolves and executes an IBaseWorkflowStep from the DI container.
    /// </summary>
    public class DIMessageTransformer : IMessageTransformer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DIMessageTransformer> _logger;

        public DIMessageTransformer(IServiceProvider serviceProvider, ILogger<DIMessageTransformer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<Dictionary<string, object>> TransformAsync(TransformOptions options,Dictionary<string,object> message, IStepExecutionContext transformContext, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.ServiceType))
            {
                _logger.LogWarning("ServiceType is not specified in TransformOptions. Cannot execute step.");
                return message;
            }

            Type serviceType = Type.GetType(options.ServiceType, throwOnError: true)!;

            object serivce = _serviceProvider.GetRequiredService(serviceType);

            if(serivce is not IMessageTransformer transformer)
            {
                throw new InvalidOperationException($"Resolved service of type {options.ServiceType} does not implement IMessageTransformer.");
            }

            _logger.LogDebug("Executing workflow step: {ServiceType}", options.ServiceType);
            return await transformer.TransformAsync(options,message,transformContext, cancellationToken);

        }
    }
}