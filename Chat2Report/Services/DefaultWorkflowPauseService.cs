using Microsoft.AutoGen.Contracts;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Default implementation of <see cref="IWorkflowPauseService"/> that uses an in-memory dictionary
    /// to manage <see cref="TaskCompletionSource{TResult}"/> instances for each topic.
    /// </summary>
    public class DefaultWorkflowPauseService : IWorkflowPauseService
    {
        private readonly ConcurrentDictionary<TopicId, TaskCompletionSource<Dictionary<string, object>>> _pauseHandles = new();
        private readonly ILogger<DefaultWorkflowPauseService> _logger;

        public DefaultWorkflowPauseService(ILogger<DefaultWorkflowPauseService> logger)
        {
            _logger = logger;
        }

        public Task<TaskCompletionSource<Dictionary<string, object>>> GetOrCreateAsync(TopicId topicId)
        {
            _logger.LogDebug("Getting or creating a pause handle for topic: {TopicId}", topicId);
            var tcs = _pauseHandles.GetOrAdd(topicId, _ => new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously));
            return Task.FromResult(tcs);
        }

        public bool TryResume(TopicId topicId, Dictionary<string, object> resumeData)
        {
            if (_pauseHandles.TryRemove(topicId, out var tcs))
            {
                _logger.LogInformation("Resuming workflow for topic: {TopicId}", topicId);
                return tcs.TrySetResult(resumeData);
            }

            _logger.LogWarning("Attempted to resume a workflow for topic {TopicId}, but no pause handle was found.", topicId);
            return false;
        }

        public void Cancel(TopicId topicId)
        {
            if (_pauseHandles.TryRemove(topicId, out var tcs))
            {
                if (!tcs.Task.IsCompleted)
                {
                    _logger.LogInformation("Cancelling pause handle for topic: {TopicId}", topicId);
                    tcs.TrySetCanceled();
                }
            }
            else
            {
                _logger.LogDebug("Attempted to cancel a pause handle for topic {TopicId}, but it was not found (it may have already completed or been cancelled).", topicId);
            }
        }
    }
}