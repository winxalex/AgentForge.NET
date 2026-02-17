// --- START OF FILE Tests/WorkflowHistoryTests.cs ---
using Xunit;
using Chat2Report.Models;
using Chat2Report.Models.Workflow;
using System.Collections.Generic;
using System.Linq;

namespace Chat2Report.Tests
{
    public class WorkflowHistoryTests
    {
        [Fact]
        public void CreateNextSnapshot_CorrectlyManages_StepAndAgentScopes()
        {
            // ARRANGE: 1. Иницијализација
            var initialState = new Dictionary<string, object>
            {
                { "user_query", "Тест барање" }
            };
            var history = new AgentStepHistory(initialState);

            // Проверка на почетната состојба
            Assert.Single(history.Snapshots);
            var snapshot0 = history.GetCurrentSnapshot();
            Assert.Equal(0, snapshot0.Turn);
            Assert.Equal("WorkflowStart", snapshot0.StepName);
            Assert.True(snapshot0.State.ContainsKey("user_query"));
            Assert.Equal(StateScope.Workflow, snapshot0.State["user_query"].Scope);


            // ACT: 2. Симулација на Чекор 1 (LlmCall)
            // Овој чекор произведува 'validation_raw_json' со Step scope
            var step1_Outputs = new Dictionary<string, StateEntry>
            {
                { "validation_raw_json", new StateEntry {
                    Value = "{ \"is_valid\": true }",
                    Scope = StateScope.Step,
                    TurnCreated = 1,
                    Timestamp = System.DateTime.UtcNow
                }}
            };
            history.CreateNextSnapshot("LlmCall", step1_Outputs);

            // ASSERT: Проверка на состојбата по Чекор 1
            Assert.Equal(2, history.Snapshots.Count);
            var snapshot1 = history.GetCurrentSnapshot();
            Assert.Equal(1, snapshot1.Turn);
            Assert.Equal("LlmCall", snapshot1.StepName);
            // Проверуваме дали ги има и стариот (workflow) и новиот (step) клуч
            Assert.True(snapshot1.State.ContainsKey("user_query"));
            Assert.True(snapshot1.State.ContainsKey("validation_raw_json"));
            Assert.Equal(StateScope.Step, snapshot1.State["validation_raw_json"].Scope);


            // ACT: 3. Симулација на Чекор 2 (DeserializeJson)
            // Овој чекор произведува 'validation_result' со Agent scope
            var step2_Outputs = new Dictionary<string, StateEntry>
            {
                { "validation_result", new StateEntry {
                    Value = new { is_valid = true }, // Симулираме десеријализиран објект
                    Scope = StateScope.Agent,
                    TurnCreated = 2,
                    Timestamp = System.DateTime.UtcNow
                }}
            };
            history.CreateNextSnapshot("DeserializeJson", step2_Outputs);

            // ASSERT: Проверка на состојбата по Чекор 2
            Assert.Equal(3, history.Snapshots.Count);
            var snapshot2 = history.GetCurrentSnapshot();
            Assert.Equal(2, snapshot2.Turn);
            Assert.Equal("DeserializeJson", snapshot2.StepName);

            // === КЛУЧНА ПРОВЕРКА ===
            // Проверуваме дали стариот StepScope клуч ('validation_raw_json') е АВТОМАТСКИ ИЗБРИШАН!
            Assert.False(snapshot2.State.ContainsKey("validation_raw_json"),
                "Клучот 'validation_raw_json' со Step scope требаше да биде автоматски избришан.");

            // Проверуваме дали новиот AgentScope клуч и стариот WorkflowScope клуч сеуште постојат
            Assert.True(snapshot2.State.ContainsKey("user_query"));
            Assert.True(snapshot2.State.ContainsKey("validation_result"));
            Assert.Equal(StateScope.Agent, snapshot2.State["validation_result"].Scope);


            // ACT & ASSERT: Симулација на уште еден чекор за да видиме дека Agent scope останува
            history.CreateNextSnapshot("SomeOtherStep", new Dictionary<string, StateEntry>());
            var snapshot3 = history.GetCurrentSnapshot();
            Assert.True(snapshot3.State.ContainsKey("user_query"), "Workflow scope треба да остане.");
            Assert.True(snapshot3.State.ContainsKey("validation_result"), "Agent scope треба да остане во рамките на агентот.");
        }

        [Fact]
        public void TimeTravel_CanRecreateStateFromSnapshot()
        {
            // ARRANGE
            var initialState = new Dictionary<string, object> { { "user_query", "Тест" } };
            var history = new AgentStepHistory(initialState);

            // Симулираме неколку чекори...
            history.CreateNextSnapshot("StepA", new Dictionary<string, StateEntry> {
                { "key_a", new StateEntry { Value = "Value A", Scope = StateScope.Agent, TurnCreated = 1 }}
            });
            history.CreateNextSnapshot("StepB", new Dictionary<string, StateEntry> {
                { "key_b", new StateEntry { Value = 123, Scope = StateScope.Step, TurnCreated = 2 }}
            });

            // ACT: Сакаме да се "вратиме" на состојбата по StepA
            var snapshotToRestore = history.Snapshots[1]; // Индекс 1 е snapshot-от по StepA

            // Рекреирај "рамен" речник од тој snapshot
            var restoredFlatState = snapshotToRestore.State
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

            // ASSERT: Провери ја рекреираната состојба
            Assert.Equal(2, restoredFlatState.Count);
            Assert.True(restoredFlatState.ContainsKey("user_query"));
            Assert.True(restoredFlatState.ContainsKey("key_a"));
            Assert.Equal("Value A", restoredFlatState["key_a"]);
            Assert.False(restoredFlatState.ContainsKey("key_b"), "Клучот од иднината (StepB) не смее да постои.");
        }
    }
}
// --- END OF FILE Tests/WorkflowHistoryTests.cs ---