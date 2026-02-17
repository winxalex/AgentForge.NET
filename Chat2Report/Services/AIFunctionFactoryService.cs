// Датотека: Services/AIFunctionFactoryService.cs
using Chat2Report.Models;
using Chat2Report.Providers;
using Microsoft.Extensions.AI;
using System.Reflection;

namespace Chat2Report.Services
{

    public interface IAIFunctionFactoryService
    {
        IList<AITool> CreateToolsFromConfig(List<ToolConfig> toolConfigs);
    }



    public class AIFunctionFactoryService : IAIFunctionFactoryService
    {
        private readonly ILogger<AIFunctionFactoryService> _logger;
        private readonly IDelegateCache _delegateCache; // Користиме кеш за рефлексија
        private readonly IToolInstanceProvider _toolInstanceProvider; // Хибридниот провајдер

        public AIFunctionFactoryService(
            ILogger<AIFunctionFactoryService> logger,
            IDelegateCache delegateCache,
            IToolInstanceProvider toolInstanceProvider)
        {
            _logger = logger;
            _delegateCache = delegateCache;
            _toolInstanceProvider = toolInstanceProvider;
        }

        public IList<AITool> CreateToolsFromConfig(List<ToolConfig> toolConfigs)
        {
            var aiTools = new List<AITool>();
            if (toolConfigs == null) return aiTools;

            // ОВА Е ЛОГИКАТА ОД ВАШИОТ ПРИМЕР, СТАВЕНА ВО СЕРВИС
            foreach (var item in toolConfigs)
            {
                try
                {
                    MethodInfo toolMethodInfo = _delegateCache.GetOrAddMethodInfo(item.AssemblyQualifiedName, item.Method);
                    object? target = !toolMethodInfo.IsStatic ? _toolInstanceProvider.GetInstance(toolMethodInfo.DeclaringType!) : null;
                    aiTools.Add(AIFunctionFactory.Create(toolMethodInfo, target, item.DescriptiveFunctionName, item.Description));

                    //TODO: Добро за агенти во едитор
                    //AIFunction approvalRequiredWeatherFunction = new ApprovalRequiredAIFunction(weatherFunction);
                    //                var functionApprovalRequests = response.Messages
                    //.SelectMany(x => x.Contents)
                    //.OfType<FunctionApprovalRequestContent>()
                    //.ToList();
                }
                catch (TypeLoadException ex)
                {
                    _logger.LogError(ex, "Failed to load type '{TypeName}' for tool '{FunctionName}'. Check AssemblyQualifiedName.", item.AssemblyQualifiedName, item.Method);
                }
                catch (MissingMethodException ex)
                {
                    _logger.LogError(ex, "Failed to find method '{FunctionName}' in type '{TypeName}' for tool. Check method name, parameters (if applicable), and BindingFlags.", item.Method, item.AssemblyQualifiedName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create or add tool '{FunctionName}' from type '{TypeName}' using MethodInfo.", item.Method, item.AssemblyQualifiedName);
                }
            }
            return aiTools;
        }
    }
}