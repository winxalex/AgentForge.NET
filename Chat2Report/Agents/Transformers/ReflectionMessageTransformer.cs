// --- In a new file: ITransformerExecutor.cs ---
using Chat2Report.Models;
using Chat2Report.Providers;

namespace Chat2Report.Agents.Transformers
{
    public class ReflectionMessageTransformer : IMessageTransformer
    {
        private readonly ILogger<ReflectionMessageTransformer> _logger;
        private readonly IDelegateCache _delegateCache;
       

        public ReflectionMessageTransformer(IDelegateCache delegateCache, ILogger<ReflectionMessageTransformer> logger = null)
        {
            _delegateCache = delegateCache ?? throw new ArgumentNullException(nameof(delegateCache));
            _logger = logger;
           
        }

      
        //public Dictionary<string,object> Transform(TransformOptions options, string json, ISharedMemoryProvider sharedMemoryProvider)
        //{
        //    return ProcessWithReflection(options.AssemblyQualifiedName, options.Method,JsonToDictionaryConverter.DeserializeToDictionary(json), sharedMemoryProvider);

        //}

        //public Dictionary<string, object> Transform(TransformOptions options, Dictionary<string, object> message, ISharedMemoryProvider sharedMemoryProvider)
        //{
        //      return ProcessWithReflection(options.AssemblyQualifiedName,options.Method,message, sharedMemoryProvider);
        //}

        public Task<Dictionary<string, object>> TransformAsync(TransformOptions options,Dictionary<string,object> message, IStepExecutionContext context, CancellationToken cancellationToken)
        {
            return ProcessWithReflection(options.AssemblyQualifiedName, options.Method, message,context,cancellationToken);
        }

       

        private Task<Dictionary<string, object>> ProcessWithReflection(string typeName, string methodName, Dictionary<string, object> message, IStepExecutionContext transformContext, CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Processing message with Reflection transformer: {TypeName}.{MethodName}", typeName, methodName);
            try
            {
                // Use the cache to get the delegate
                var transformDelegate = _delegateCache.GetOrAddTransformerDelegate(typeName, methodName);

                // Invoke the delegate
                var result = transformDelegate(message);
                _logger?.LogDebug("Reflection transformation result obtained.");
                return Task.FromResult(result ?? message); // Wrap the result in a Task
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing reflection transformer '{TypeName}.{MethodName}'.", typeName, methodName);
                // Return original message or an error object
                return Task.FromResult(new Dictionary<string, object> { { "transformError", $"Reflection transform failed: {ex.Message}" } });
            }
        }

     

        
    }

}

// --- Example Transformer Class ---
// namespace MyProject.Transformers
// {
//     public static class PlanTransformers
//     {
//         // Example transformer for the Planner agent
//         public static Dictionary<string, object> PrepareExecutorMessage(Dictionary<string, object> plannerResult, IDictionary<string, object> sharedMemory)
//         {
//             // 1. Update Shared Memory based on planner's result
//             if (plannerResult.TryGetValue("currentStep", out var step) && step is int currentStep)
//             {
//                 sharedMemory["currentStep"] = currentStep;
//             }
//             if (plannerResult.TryGetValue("plan", out var plan)) // Assuming plan is List<Dictionary<string, object>> or similar
//             {
//                  sharedMemory["plan"] = plan;
//                  // Maybe update totalSteps here too
//                  if (plan is List<object> planList) sharedMemory["totalSteps"] = planList.Count;
//             }
//              if (plannerResult.TryGetValue("planStatus", out var status))
//             {
//                  sharedMemory["planStatus"] = status; // Update status array/list
//             }


//             // 2. Create the message for the Executor
//             var executorMessage = new Dictionary<string, object>();
//             if (plannerResult.TryGetValue("currentTask", out var task))
//             {
//                 executorMessage["task"] = task;
//                 // Add the step number for context
//                 executorMessage["step"] = sharedMemory.TryGetValue("currentStep", out var currentS) ? currentS : -1;
//             }
//             else
//             {
//                 // Handle error: task not found in planner result
//                 Console.WriteLine("WARN: currentTask not found in planner result for transformation.");
//             }


//             Console.WriteLine($"[Transformer] Updated Shared Memory: Step={sharedMemory["currentStep"]}, TotalSteps={sharedMemory.GetValueOrDefault("totalSteps")}");
//             Console.WriteLine($"[Transformer] Sending to Executor: {System.Text.Json.JsonSerializer.Serialize(executorMessage)}");


//             return executorMessage;
//         }

//         // Example transformer for the Executor agent reporting back
//          public static Dictionary<string, object> ReportToPlanner(Dictionary<string, object> executorResult, IDictionary<string, object> sharedMemory)
//          {
//              var report = new Dictionary<string, object>();
//              int currentStep = sharedMemory.TryGetValue("currentStep", out var step) ? (int)step : -1;

//              report["step"] = currentStep;
//              report["status"] = executorResult.ContainsKey("error") ? "failed" : "completed"; // Example logic
//              report["result"] = executorResult; // Pass the raw result back

//              // Optionally update shared memory status here
//               if (sharedMemory.TryGetValue("planStatus", out var statusList) && statusList is List<string> statuses && currentStep >= 0 && currentStep < statuses.Count)
//               {
//                   statuses[currentStep] = report["status"].ToString();
//               }

//              Console.WriteLine($"[Transformer] Reporting to Planner: {System.Text.Json.JsonSerializer.Serialize(report)}");

//              return report;
//          }
//     }
// }