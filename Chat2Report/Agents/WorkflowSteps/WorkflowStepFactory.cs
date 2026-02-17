//// Датотека: WorkflowStepFactory.cs
using Chat2Report.Models;
using System.Reflection;
using System.Text.Json;

namespace Chat2Report.Agents.WorkflowSteps
{
    public interface IWorkflowStepFactory
    {
        IBaseWorkflowStep CreateStep(WorkflowStepDefinition stepDefinition);
    }

    public class WorkflowStepFactory : IWorkflowStepFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowStepFactory> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public WorkflowStepFactory(IServiceProvider serviceProvider, ILogger<WorkflowStepFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IBaseWorkflowStep CreateStep(WorkflowStepDefinition stepDefinition)
        {
            if (string.IsNullOrWhiteSpace(stepDefinition.Type))
                throw new ArgumentException("Step configuration must have a 'Type' property.");

            _logger.LogDebug("Creating workflow step of type '{StepType}'.", stepDefinition.Type);

            // Го земаме регистрираниот сервис по клуч
            var stepInstance = _serviceProvider.GetRequiredKeyedService<IBaseWorkflowStep>(stepDefinition.Type);

            // Го повикуваме Configure методот за да ги сетираме специфичните опции (од 'Config' секцијата)
            //stepInstance.Configure(stepDefinition.InstanceConfiguration.Config);

            if (stepInstance is IConfigurableStep configurableStep)
            {
                // Директно го предаваме JsonElement-от. Едноставно и чисто.
                configurableStep.Configure(stepDefinition.Config);
            }

            return stepInstance;
        }

        
      
    }
}

