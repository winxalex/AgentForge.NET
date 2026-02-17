using System;
using System.Collections.Generic;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;

namespace Chat2Report.Models
{
    /// <summary>
    /// Defines a contract for passing contextual information to a transformation step,
    /// without coupling the step to the entire workflow state.
    /// This provides a flexible, yet type-safe way to access context data.
    /// </summary>
    public interface IStepExecutionContext
    {
        string WorkflowTopicId { get; }


        /// <summary>
        /// Gets a value from the context by key, casting it to the specified type.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <typeparam name="T">The type to cast the value to.</typeparam>
        /// <returns>The value if it exists and is of the correct type; otherwise, the default value for the type.</returns>
        T? Get<T>(string key);
    }

    /// <summary>
    /// A concrete, extensible implementation of IStepExecutionContext that acts as a property bag.
    /// It uses a dictionary for storage, allowing for dynamic addition of context properties,
    /// while providing strongly-typed accessors for common properties.
    /// </summary>
    public class StepExecutionContext : IStepExecutionContext
    {
        private readonly Dictionary<string, object> _contextData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public string WorkflowTopicId
        {
            get => Get<string>(ContextKeys.WorkflowTopicId) ?? string.Empty;
            set => Set(ContextKeys.WorkflowTopicId, value);
        }

        /// <summary>
        /// Gets or sets the ID of the agent that initiated this transformation.
        /// This is another example of a strongly-typed accessor.
        /// </summary>
        public AgentId? InitiatingAgentId
        {
            get => Get<AgentId>(ContextKeys.InitiatingAgentId);
            set => Set(ContextKeys.InitiatingAgentId, value);
        }

        public T? Get<T>(string key)
        {
            return _contextData.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
        }

        /// <summary>
        /// Sets a value in the context.
        /// </summary>
        public void Set(string key, object? value) => _contextData[key] = value;

        public static class ContextKeys
        {
            public const string WorkflowTopicId = "workflow_topic_id";
            public const string InitiatingAgentId = "InitiatingAgentId";
        }
    }
}
