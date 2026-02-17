using Chat2Report.Models;
using Microsoft.Extensions.Options;
using Chat2Report.Options;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chat2Report.Services
{
    /// <summary>
    /// Имплементација на IWorkflowHistoryStore која ги зачувува состојбите
    /// како JSON датотеки на фајл систем.
    /// </summary>
    public class JsonFileWorkflowHistoryStore : IWorkflowHistoryStore
    {
        private readonly string _basePath;
        private readonly ILogger<JsonFileWorkflowHistoryStore> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new() 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public JsonFileWorkflowHistoryStore(IOptions<HistoryStoreSettings> historyStoreSettings, ILogger<JsonFileWorkflowHistoryStore> logger)
        {
            // Користиме посветена конфигурација за патеката на историјата.
            _basePath = historyStoreSettings.Value.BasePath;
            _logger = logger;

            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        public async Task SaveStateSnapshotAsync(WorkflowStateSnapshot snapshot)
        {
            try
            {
                var workflowDir = Path.Combine(_basePath, snapshot.WorkflowTopicId);
                if (!Directory.Exists(workflowDir))
                {
                    Directory.CreateDirectory(workflowDir);
                }

                // Името на датотеката ќе биде во формат: 005_AgentName.json
                var fileName = $"{snapshot.Sequence:D3}_{snapshot.TargetAgentId.Replace("/", "_")}.json";
                var filePath = Path.Combine(workflowDir, fileName);

                await using var fileStream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(fileStream, snapshot, _jsonOptions);

                _logger.LogInformation("Saved workflow state snapshot to: {FilePath}", filePath);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to save workflow state snapshot for topic {TopicId}", snapshot.WorkflowTopicId);
            }
        }

        public Task<List<WorkflowStateSnapshot>> GetHistoryForWorkflowAsync(string workflowTopicId)
        {
            var workflowDir = Path.Combine(_basePath, workflowTopicId);
            if (!Directory.Exists(workflowDir))
            {
                _logger.LogWarning("History directory not found for workflow topic: {TopicId}", workflowTopicId);
                return Task.FromResult(new List<WorkflowStateSnapshot>());
            }

            var snapshots = new List<WorkflowStateSnapshot>();
            var files = Directory.EnumerateFiles(workflowDir, "*.json")
                                 .OrderBy(f => f); // Сортирај по име на фајл (кое ја содржи секвенцата)

            foreach (var file in files)
            {
                try
                {
                    var jsonContent = File.ReadAllText(file);
                    var snapshot = JsonSerializer.Deserialize<WorkflowStateSnapshot>(jsonContent, _jsonOptions);
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Failed to read or deserialize snapshot file: {FilePath}", file);
                }
            }

            return Task.FromResult(snapshots);
        }
    }
}