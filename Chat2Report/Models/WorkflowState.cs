
namespace Chat2Report.Models
{
    /// <summary>
    /// Represents the shared state that flows through the agent workflow.
    /// It combines strongly-typed properties for common data with a flexible
    /// dictionary for dynamic data accumulated during the process.
    /// </summary>
    public class WorkflowState
    {
        /// <summary>
        /// The unique identifier for the entire workflow or conversation topic.
        /// </summary>
        public string WorkflowTopicId { get; set; }

        /// <summary>
        /// A dictionary to hold dynamic data. Keys are strings, and values can be any object.
        /// This allows agents to add, read, and modify data as the workflow progresses.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

       
    }
}