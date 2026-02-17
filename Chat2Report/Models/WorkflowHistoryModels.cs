// --- START OF FILE Models/WorkflowHistoryModels.cs ---
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chat2Report.Models.Workflow
{
    /// <summary>
    /// Претставува мета-податоците и вредноста на еден запис во состојбата на workflow-то.
    /// </summary>
    public class StateEntry
    {
        public object? Value { get; set; }
        public StateScope Scope { get; set; }
        public int TurnCreated { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Претставува "слика" (snapshot) на состојбата на workflow-то во одредена точка во времето,
    /// обично по извршување на еден чекор.
    /// </summary>
    public class StateSnapshot
    {
        public int Turn { get; set; }
        public string StepName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, StateEntry> State { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Го содржи целиот тек на извршување на еден workflow, вклучувајќи ги сите
    /// промени на состојбата низ времето како листа од snapshots.
    /// Овој објект е лесно серијализирлив и овозможува "time-travel debugging".
    /// </summary>
    public class AgentStepHistory
    {
        public Guid RunId { get; }
        public DateTime StartTime { get; }

        private readonly List<StateSnapshot> _snapshots = new();
        public IReadOnlyList<StateSnapshot> Snapshots => _snapshots.AsReadOnly();

        public StateSnapshot GetCurrentSnapshot()
        {
            if (!_snapshots.Any())
                throw new InvalidOperationException("WorkflowHistory is not initialized with a starting snapshot.");
            return _snapshots.Last();
        }

        public void CreateNextSnapshot(string stepName, Dictionary<string, StateEntry> newOrUpdatedEntries)
        {
            var previousState = GetCurrentSnapshot().State;
            var newState = new Dictionary<string, StateEntry>(previousState, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in newOrUpdatedEntries)
            {
                newState[entry.Key] = entry.Value;
            }

            // Автоматско чистење на 'Step' scope записи од претходниот чекор
            var currentTurn = _snapshots.Count;
            var keysToRemove = newState
                .Where(kvp => kvp.Value.Scope == StateScope.Step && kvp.Value.TurnCreated < currentTurn)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                newState.Remove(key);
            }

            _snapshots.Add(new StateSnapshot
            {
                Turn = currentTurn,
                StepName = stepName,
                Timestamp = DateTime.UtcNow,
                State = newState
            });
        }

        public AgentStepHistory(Dictionary<string, object> initialState, Guid? runId = null)
        {
            RunId = runId ?? Guid.NewGuid();
            StartTime = DateTime.UtcNow;

            var initialEntries = initialState.ToDictionary(
                kvp => kvp.Key,
                kvp => new StateEntry { Value = kvp.Value, Scope = StateScope.Workflow, TurnCreated = 0, Timestamp = StartTime },
                StringComparer.OrdinalIgnoreCase);

            _snapshots.Add(new StateSnapshot
            {
                Turn = 0,
                StepName = "WorkflowStart",
                Timestamp = StartTime,
                State = initialEntries
            });
        }
    }
}
// --- END OF FILE Models/WorkflowHistoryModels.cs ---