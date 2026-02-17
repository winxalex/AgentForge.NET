using Chat2Report.Agents.Generated.IO.WaitForExternalEvent;
using Chat2Report.Models;
using Chat2Report.Services;
using Microsoft.AutoGen.Contracts;
using System.Threading;

namespace Chat2Report.Agents.WorkflowSteps
{
    /// <summary>
    /// A generic step that pauses the workflow and waits for an external event.
    /// The event is signaled by completing a TaskCompletionSource on a specific topic.
    /// The topic name is provided as an input, making this step reusable for various scenarios
    /// like waiting for user input, API callbacks, or other external system signals.
    /// </summary>
    public class WaitForExternalEventStep : IWorkflowStep<Inputs, Outputs>
    {
        private readonly ITopicTerminationService _topicTerminationService;
        private readonly ILogger<WaitForExternalEventStep> _logger;

        public WaitForExternalEventStep(ITopicTerminationService topicTerminationService, ILogger<WaitForExternalEventStep> logger)
        {
            _topicTerminationService = topicTerminationService;
            _logger = logger;
        }

        public async Task<Outputs> ExecuteAsync(Inputs inputs, IStepExecutionContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputs.TopicName))
            {
                throw new ArgumentException("TopicName must be provided to WaitForExternalEventStep.");
            }

            _logger.LogInformation("Pausing workflow '{WorkflowTopicId}' and waiting for event on topic '{TopicName}'.", context.WorkflowTopicId, inputs.TopicName);

            var topicId = new TopicId(inputs.TopicName, context.WorkflowTopicId);
            var tcs = await _topicTerminationService.GetOrCreateAsync(topicId);

            // Ensure the wait respects the overall cancellation of the workflow
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

            var eventData = await tcs.Task;

            _logger.LogInformation("Resuming workflow '{WorkflowTopicId}' with data from event on topic '{TopicName}'.", context.WorkflowTopicId, inputs.TopicName);

            return new Outputs { Result = eventData };
        }
    }
}
