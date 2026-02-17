using Microsoft.AutoGen.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Defines a service for managing the pausing and resuming of agent workflows.
    /// This allows a workflow to wait for an external event, like user input, before continuing.
    /// </summary>
    public interface IWorkflowPauseService
    {
        Task<TaskCompletionSource<Dictionary<string, object>>> GetOrCreateAsync(TopicId topicId);

        bool TryResume(TopicId topicId, Dictionary<string, object> resumeData);

        void Cancel(TopicId topicId);
    }
}