using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Дефинира "слика" од состојбата на работниот тек во одредена точка.
    /// </summary>
    public class WorkflowStateSnapshot
    {
        public string WorkflowTopicId { get; set; }
        public string TargetAgentId { get; set; }
        public int Sequence { get; set; }
        public System.DateTime Timestamp { get; set; }
        public Dictionary<string, object> StateData { get; set; }
    }

    /// <summary>
    /// Договор за сервис кој овозможува перзистентно чување и вчитување
    /// на историјата на состојбите на еден работен тек.
    /// </summary>
    public interface IWorkflowHistoryStore
    {
        /// <summary>
        /// Зачувува слика од состојбата за даден работен тек, поврзана со агентот што ќе ја прими.
        /// </summary>
        /// <param name="snapshot">Објектот што ја содржи состојбата за зачувување.</param>
        Task SaveStateSnapshotAsync(WorkflowStateSnapshot snapshot);

        /// <summary>
        /// Вчитува листа на сите зачувани состојби за еден работен тек, подредени секвенцијално.
        /// </summary>
        Task<List<WorkflowStateSnapshot>> GetHistoryForWorkflowAsync(string workflowTopicId);
    }
}