using Microsoft.AutoGen.Contracts;

namespace Chat2Report.Services
{
    public interface ITopicTerminationService
    {
        /// <summary>
        /// Gets or creates a TaskCompletionSource for the specified topic.
        /// </summary>
        /// <param name="topicId">The ID of the topic.</param>
        /// <returns>A TaskCompletionSource that can be used to signal completion or cancellation.</returns>
        Task<TaskCompletionSource<Dictionary<string, object>>> GetOrCreateAsync(TopicId topicId);

        /// <summary>
        /// Attempts to complete the TaskCompletionSource for the specified topic with the given result.
        /// </summary>
        /// <param name="topicId">The ID of the topic.</param>
        /// <param name="result">The result to set.</param>
        /// <returns>True if the TaskCompletionSource was found and completed, false otherwise.</returns>
        bool TryComplete(TopicId topicId, Dictionary<string, object> result);

        /// <summary>
        /// Resets the TaskCompletionSource for the specified topic, removing it from the service.
        /// </summary>
        /// <param name="topicId">The ID of the topic.</param>
        void Reset(TopicId topicId);

        /// <summary>
        /// Attempts to cancel the TaskCompletionSource for the specified topic.
        /// </summary>
        /// <param name="topicId">The ID of the topic.</param>
        void Cancel(TopicId topicId);
        void TrySetException(TopicId value, Exception e);
    }
}

