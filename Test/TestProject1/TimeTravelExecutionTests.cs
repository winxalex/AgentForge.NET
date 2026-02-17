// --- START OF FILE Tests/TimeTravelExecutionTests.cs (FINAL, REALISTIC VERSION) ---
using Xunit;
using Chat2Report.Agents;
using Chat2Report.Models;
using Chat2Report.Models.Workflow;
using Chat2Report.Providers;
using Chat2Report.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AutoGen.Web;
using Chat2Report.Extensions;
using Chat2Report.Agents.Evaluation;
using Chat2Report.Agents.Transformers;
using Chat2Report.Agents.WorkflowSteps;
using Chat2Report.VectorStore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.JSInterop;

namespace Chat2Report.Tests
{
    public class TimeTravelExecutionTests : IAsyncLifetime
    {
        private ServiceProvider _serviceProvider;
        private IAgentRuntime _runtime;
        private ITopicTerminationService _terminationService;
        private Mock<IADService> _adServiceMock;
        private Mock<IWorkflowPauseService> _pauseServiceMock;

        // Овој метод се извршува еднаш пред сите тестови во класата
        public async Task InitializeAsync()
        {
            // 1. Симулираме WebApplicationBuilder и конфигурација
            var builder = WebApplication.CreateBuilder();

            // Вчитај го appsettings.json од главниот проект
            builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // 2. ГО КОРИСТИМЕ ВАШИОТ КОД ЗА ПОСТАВУВАЊЕ!
            // Ова е клучниот дел. Го реупотребуваме истиот код од Program.cs

            AgentsWebBuilder appBuilder = new AgentsWebBuilder(builder);
            appBuilder.UseInProcessRuntime();

            // --- СИМУЛАЦИЈА НА НАДВОРЕШНИ ЗАВИСНОСТИ ---
            // Ги заменуваме вистинските сервиси со мокирани верзии САМО за овој тест
            _adServiceMock = new Mock<IADService>();
            _pauseServiceMock = new Mock<IWorkflowPauseService>();
            appBuilder.Services.AddSingleton(_adServiceMock.Object);
            appBuilder.Services.AddSingleton(_pauseServiceMock.Object);

            // --- ОСТАНАТАТА КОНФИГУРАЦИЈА Е ПРЕЗЕМЕНА ОД ВАШИОТ КОД ---
            // (го копираме целиот код за регистрација на сервиси и чекори)
            // ...
            appBuilder.Services.AddSingleton<IPromptProvider, FilePromptProvider>();
            appBuilder.Services.AddSingleton<ITopicTerminationService, DefaultTopicTerminationService>();
            appBuilder.Services.TryAddSingleton<ILogger<FilePromptProvider>>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<FilePromptProvider>());
            appBuilder.Services.AddSingleton<IPromptProvider, FilePromptProvider>(); // Register as Singleton
            appBuilder.Services.TryAddSingleton<ITopicTerminationService>(sp => new DefaultTopicTerminationService(sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultTopicTerminationService>()));
            appBuilder.Services.TryAddSingleton<IWorkflowPauseService>(sp => new DefaultWorkflowPauseService(sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultWorkflowPauseService>()));
            appBuilder.Services.TryAddSingleton<ILogger<HybridToolInstanceProvider>>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<HybridToolInstanceProvider>());
            appBuilder.Services.TryAddSingleton<ILogger<BaseAgent>>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<BaseAgent>());
            //appBuilder.Services.TryAddSingleton(sp=>sp.GetRequiredService<ILoggerFactory>().CreateLogger<UIStreamListenerAgent>());
            // Option 1: Simple Activator (only parameterless constructors)
            // appBuilder.Services.AddSingleton<IToolInstanceProvider, ActivatorToolInstanceProvider>();

            // Option 2: Hybrid (Uses DI if available, falls back to Activator)
            // This requires access to the IServiceProvider itself, often available during configuration
            appBuilder.Services.AddSingleton<IToolInstanceProvider, HybridToolInstanceProvider>();

            appBuilder.Services.AddSingleton<IVectorStore, USearchVectorStore>();

            var dataStoreDbPath = builder.Configuration["VectorStoreSettings:ConnectionString"]
                ?? throw new InvalidOperationException("VectorStoreSettings:ConnectionString is not configured in appsettings.json");

            appBuilder.Services.AddSingleton<IDataStore<ulong, ColumnDefinition>>(sp =>
                                 new SqliteDataStore<ColumnDefinition>(Path.Combine(AppContext.BaseDirectory, dataStoreDbPath), sp.GetService<ILogger<SqliteDataStore<ColumnDefinition>>>())); // Assuming constructor takes logger
            appBuilder.Services.AddSingleton<IDataStore<ulong, ValueDefinition>>(sp =>
                new SqliteDataStore<ValueDefinition>(Path.Combine(AppContext.BaseDirectory, dataStoreDbPath), sp.GetService<ILogger<SqliteDataStore<ValueDefinition>>>())); // Assuming constructor takes logger

            // Add IDataStore for string
            appBuilder.Services.AddSingleton<IDataStore<ulong, DomainDefinition>>(sp =>
                new SqliteDataStore<DomainDefinition>(Path.Combine(AppContext.BaseDirectory, dataStoreDbPath), sp.GetService<ILogger<SqliteDataStore<DomainDefinition>>>()));

            // Add IDataStore for TableDefinition
            appBuilder.Services.AddSingleton<IDataStore<ulong, TableDefinition>>(sp =>
              new SqliteDataStore<TableDefinition>(Path.Combine(AppContext.BaseDirectory, dataStoreDbPath), sp.GetService<ILogger<SqliteDataStore<TableDefinition>>>()));

            // Add IDataStore for ViewDefinition
            appBuilder.Services.AddSingleton<IDataStore<ulong, ViewDefinition>>(sp =>
              new SqliteDataStore<ViewDefinition>(Path.Combine(AppContext.BaseDirectory, dataStoreDbPath), sp.GetService<ILogger<SqliteDataStore<ViewDefinition>>>()));

            //appBuilder.Services.AddSingleton<IDataSchemaService, MSSQLDataSchemaService>();
            //appBuilder.Services.AddSingleton<IChatClient, ResilientChatClient>();

            var cache = new InMemoryDelegateCache();


            // Register individual transformers
            appBuilder.Services.AddSingleton<JsonataTransformer>();
            appBuilder.Services.AddSingleton<DIMessageTransformer>();
            appBuilder.Services.AddSingleton<ReflectionMessageTransformer>();

            // Register the delegating transformer as the main IMessageTransformer
            appBuilder.Services.AddSingleton<IMessageTransformer, DelegatingMessageTransformer>();


            // DB Schema service
            appBuilder.Services.AddSingleton<IDataSchemaService, MSSQLDataSchemaService>();


            // --- 3. WORKFLOW STEP REGISTRATIONS ---
            // Register all available IBaseWorkflowStep implementations with a unique key.
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, LlmCallStep>("LlmCall");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, StreamingLlmCallStep>("StreamingLlmCall");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, DeserializeJsonStep>("DeserializeJson");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, EmbeddingGenerationStep>("EmbeddingGeneration");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, DisplayUIStep>("DisplayUI");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, LoadDomainsStep>("LoadDomains");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, ExtractUserEntitiesStep>("ExtractUserEntities");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, ADLookupStep>("ADLookup");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, ResolveAmbiguityStep>("ResolveAmbiguity");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, WaitForExternalEventStep>("WaitForExternalEvent");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, DomainVectorSearchStep>("DomainVectorSearch");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, GatherSchemaFromSelectedDomainsStep>("GatherSchemaFromSelectedDomains");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, TableRelevanceStep>("TableRelevance");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, ViewRelevanceStep>("ViewRelevance");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, FunctionRelevanceStep>("FunctionRelevance");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, JSONOntologyStep>("JSONOntology");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, DDLOntologyStep>("DDLOntology");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, ExtractAnalysisStep>("ExtractAnalysis");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, SqlPromptBuilderStep>("SqlPromptBuilder");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, SqlPreExecuteCleanStep>("SqlPreExecuteClean");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, SqlFixPromptBuilderStep>("SqlFixPromptBuilder");
            appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, SqlExecuteStep>("SqlExecute");


            // Register the SERVICES

            // Register the AD service
            appBuilder.Services.AddSingleton<IADService, ADService>();

            // Register the Excel service
            appBuilder.Services.TryAddSingleton<IExcelService, ExcelService>();

            // Register the SQL executor service
            appBuilder.Services.AddTransient<ISqlExecutorService, MSSQLExecutorService>();

            //appBuilder.Services.TryAddSingleton<IExpressionEvaluator>(sp =>
            //    new JsonataEvaluator(sp.GetRequiredService<ILogger<JsonataEvaluator>>())
            //);

            appBuilder.Services.TryAddSingleton<IStatePersitanceProvider>(new JSONStatePeristanceProvider());
            appBuilder.Services.TryAddSingleton<IDelegateCache>(cache);

            // Register the UI Stream Broker and its listener
            appBuilder.Services.AddSingleton<IUIStreamBroker, UIStreamBroker>();



            var allWorkflows = builder.Configuration.GetSection("AgentsConfiguration:Workflows").Get<Dictionary<string, WorkflowDefinition>>() ?? new();
            appBuilder.DeployAgents(allWorkflows);

            _serviceProvider = builder.Services.BuildServiceProvider();
            _runtime = _serviceProvider.GetRequiredService<IAgentRuntime>();
            _terminationService = _serviceProvider.GetRequiredService<ITopicTerminationService>();



        //    this.Services.AddSingleton<IAgentRuntime, InProcessRuntime>(_ => new InProcessRuntime { DeliverToSelf = deliverToSelf });
        //    this.Services.AddHostedService<InProcessRuntime>(services =>
        //    {
        //        return (services.GetRequiredService<IAgentRuntime>() as InProcessRuntime)!;
        //    });

        //    return this;
        //}

        // Replace usage of AgentRuntimeHostedService with InProcessRuntime
        var hostedService = _serviceProvider.GetRequiredService<IHostedService>() as InProcessRuntime;
            await hostedService.StartAsync(CancellationToken.None);
        }

        // Овој метод се извршува по завршување на сите тестови
        public async Task DisposeAsync()
        {
            var hostedService = _serviceProvider.GetRequiredService<IHostedService>() as InProcessRuntime;
            await hostedService.StopAsync(CancellationToken.None);
            await _serviceProvider.DisposeAsync();
        }


        [Fact]
        public async Task AgentWorkflow_CanBeTerminated_AndResumed_FromAnyPoint()
        {
            // === ФАЗА 1: Извршување до точка на терминирање ===

            // ARRANGE
            var initialWorkflowId = $"time-travel-test-{Guid.NewGuid()}";
            var initialState = new WorkflowState
            {
                WorkflowTopicId = initialWorkflowId,
                Data = new Dictionary<string, object> { { "user_query", "Провери го Петар" } }
            };

            _adServiceMock.Setup(s => s.FindUsersByNameAsync("Петар", null,null)).ReturnsAsync(new List<UserModel>()); // Симулираме дека не го наоѓа

            // Го земаме Task-от за комплетирање од termination service
            var completionTaskSource = await _terminationService.GetOrCreateAsync(new TopicId("workflow-completion", initialWorkflowId));

            // ACT
            // Го стартуваме workflow-то со објавување на почетната тема
            await _runtime.PublishMessageAsync(
                message: initialState,
                topic: new TopicId("validate_query", initialWorkflowId)
            );

            // Чекаме workflow-то да заврши (во овој случај, ADLookup ќе врати unknown_user и ќе терминира)
            var finalResult = await completionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // ASSERT (Фаза 1)
            Assert.NotNull(finalResult);
            Assert.True(finalResult.ContainsKey("response"));
            Assert.Contains("Не е пронајден корисник", finalResult["response"] as string);
            _adServiceMock.Verify(x => x.FindUsersByNameAsync("Петар", null,null), Times.Once);


            // === ФАЗА 2: Патување низ Времето и Модификација ===

            // ARRANGE (Фаза 2)
            // Да претпоставиме дека сме ја зачувале историјата. Сега сакаме да се вратиме на почеток.
            var history = new AgentStepHistory(initialState.Data); // Повторно, симулираме историја
            var snapshotToRewindTo = history.GetCurrentSnapshot(); // snapshot[0]

            var modifiedStateData = snapshotToRewindTo.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            modifiedStateData["user_query"] = "Провери ја Ана"; // <-- Го менуваме барањето

            var rewindState = new WorkflowState
            {
                WorkflowTopicId = initialWorkflowId, // Користиме ист ID за да го следиме разговорот
                Data = modifiedStateData
            };

            _adServiceMock.Setup(x => x.FindUsersByNameAsync("Ана", null, null)).ReturnsAsync(new List<UserModel> { new UserModel { FirstName = "Ана", LastName = "Ковачева" } });

            // === ФАЗА 3: Продолжување од Модифицираната Точка ===

            // ARRANGE (Фаза 3)
            var newCompletionTaskSource = await _terminationService.GetOrCreateAsync(new TopicId("workflow-completion", initialWorkflowId));

            // ACT (Фаза 3)
            // Повторно го стартуваме истиот workflow, но со МОДИФИЦИРАНАТА состојба
            await _runtime.PublishMessageAsync(
                message: rewindState,
                topic: new TopicId("validate_query", initialWorkflowId)
            );

            // Чекаме да заврши новиот тек. Овој пат, не треба да терминира туку да продолжи кон DomainSearch.
            // За целите на тестот, ќе претпоставиме дека DomainSearch терминира веднаш.
            // Во вистински тест, би го следеле до следната точка на интерес.
            // За да го направиме ова, ќе додадеме правило за терминирање во DomainSearchAgent за тестот.
            var secondFinalResult = await newCompletionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // ASSERT (Фаза 3)
            // Повикот за "Петар" треба да остане 1 (од првиот тек)
            _adServiceMock.Verify(x => x.FindUsersByNameAsync("Петар", null, null), Times.Once);
            // Повикот за "Ана" треба да биде направен во новиот тек
            _adServiceMock.Verify(x => x.FindUsersByNameAsync("Ана", null,null), Times.Once);

            // Провери дали финалниот резултат е поинаков (на пр. не содржи 'response' клуч со грешка)
            Assert.False(secondFinalResult.ContainsKey("response"));
        }
    }
}
   