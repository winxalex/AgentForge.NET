using Microsoft.AutoGen.Contracts;
using Microsoft.Extensions.AI;

namespace Chat2Report.Models
{
    /// <summary>
    /// Represents a strongly-typed payload for streaming updates from an agent to the UI.
    /// </summary>
    public struct StreamPayload
    {
        public string WorkflowTopicId { get; init; }
        public IList<AIContent> Contents { get; init; }
    }
}