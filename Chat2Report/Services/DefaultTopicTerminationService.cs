using Microsoft.AutoGen.Contracts;
using System.Collections.Concurrent;


namespace Chat2Report.Services
{
    public class DefaultTopicTerminationService : ITopicTerminationService
    {
        private readonly ConcurrentDictionary<TopicId, TaskCompletionSource<Dictionary<string, object>>> _completions = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private ILogger<DefaultTopicTerminationService> logger;

        public DefaultTopicTerminationService(ILogger<DefaultTopicTerminationService> logger)
        {
            this.logger = logger;
        }

        public async Task<TaskCompletionSource<Dictionary<string, object>>> GetOrCreateAsync(TopicId topicId)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _completions.GetOrAdd(topicId,
                    _ => new TaskCompletionSource<Dictionary<string, object>>());
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public bool TryComplete(TopicId topicId, Dictionary<string, object> result)
        {
            _semaphore.WaitAsync();
            try
            {
                if (_completions.TryGetValue(topicId, out var tcs))
                {
                    Reset(topicId);
                    return tcs.TrySetResult(result);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return false;
        }

        public void TrySetException(TopicId topicId, Exception exception)
        {
            if (_completions.TryRemove(topicId, out var tcs))
            {
                tcs.TrySetException(exception);
                logger.LogError(exception, "Topic {TopicId} encountered an exception.", topicId);
            }
            else
            {
                logger.LogWarning("Topic {TopicId} not found for exception setting.", topicId);
            }
        }

        public void Reset(TopicId topicId)
        {
            _completions.TryRemove(topicId, out _);
        }


        public void Cancel(TopicId topicId)
        {
            _semaphore.WaitAsync();
            try
            {
                if (_completions.TryGetValue(topicId, out var tcs))
                {
                    tcs.TrySetCanceled();
                    Reset(topicId);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}