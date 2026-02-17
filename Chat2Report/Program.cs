using Chat2Report.Agents.Evaluation;
using Chat2Report.Agents.Factories;
using Chat2Report.Agents.Transformers;
using Chat2Report.Agents.WorkflowSteps;
using Chat2Report.Components;
using Chat2Report.Extensions;
using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Providers;
using Chat2Report.Services;
using Chat2Report.Services.Chart;
using Chat2Report.Tools;
using Chat2Report.Utilities;
using Chat2Report.VectorStore;


//using Chat2Report.Services.Ingestion;
using Microsoft.AutoGen.Core;
//using ModifyF = System.Func<int, int>;
//using TerminationF = System.Func<int, bool>;
using Microsoft.AutoGen.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using System.Diagnostics;
using System.Text;
using System.Text.Json;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Add IConfiguration to the service collection
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

builder.Services.Configure<AgentsConfiguration>(builder.Configuration.GetSection("AgentsConfiguration"));

builder.Services.PostConfigure<AgentsConfiguration>(options =>
{
    var jsonText = File.ReadAllText("appsettings.json");
    var jsonOptions=new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip
    };
    using var doc = JsonDocument.Parse(jsonText,jsonOptions);
    var workflows = doc.RootElement.GetProperty("AgentsConfiguration").GetProperty("Workflows");

    foreach(var w in options.Workflows) {

        var agentsJson=workflows.GetProperty(w.Key).GetProperty("Agents");

        foreach (var kvp in w.Value.Agents)
        {
            var agentName = kvp.Key;
            var agent = kvp.Value;

            
            //провери дали има особина со кључ "Steps"
            if (agentsJson.TryGetProperty(agentName, out JsonElement agentsElement) &&
                agentsElement.TryGetProperty("Steps", out JsonElement stepsElement) && agent.Steps!=null)
            {
                var stepsJson = agentsJson.GetProperty(agentName).GetProperty("Steps");


                for (int i = 0; i < agent.Steps.Count; i++)

                    //провери дали има особина со кључ "Config"
                    if (stepsJson[i].TryGetProperty("Config", out var config))

                        agent.Steps[i].Config = config.Clone();
            }

        }
    }
});



builder.Services.Configure<SchemaProcessingSettings>(builder.Configuration.GetSection("SchemaProcessingSettings"));
builder.Services.Configure<VectorStoreSettings>(builder.Configuration.GetSection("VectorStoreSettings"));
builder.Services.Configure<DataStoreSettings>(builder.Configuration.GetSection("DataStoreSettings"));
builder.Services.Configure<DomainVectorSearchStepOptions>(builder.Configuration.GetSection("DomainVectorSearchStepOptions"));
builder.Services.Configure<HistoryStoreSettings>(builder.Configuration.GetSection("HistoryStoreSettings"));
builder.Services.Configure<ValueMatcherОptions>(builder.Configuration.GetSection("ValueMatcherOptions"));
builder.Services.Configure<ADSettings>(builder.Configuration.GetSection("ADSettings"));
builder.Services.Configure<ChartingSettings>(builder.Configuration.GetSection("Charting"));


builder.Services.AddScoped<IChartRenderingService, ChartRenderingService>();

Console.OutputEncoding = Encoding.UTF8;

builder.Logging.AddDebug(); 

// Ова би се додало во Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IDataCacheService, InMemoryDataCacheService>();



#region agents_web_builder



AgentsWebBuilder appBuilder = new AgentsWebBuilder(builder);
appBuilder.UseInProcessRuntime();



appBuilder.Services.TryAddSingleton<ITemplateEngine>(sp => new HandlebarsTemplateEngine(
    sp.GetRequiredService<IExpressionEvaluator>()
));
appBuilder.Services.TryAddSingleton<ILogger<FilePromptProvider>>(sp=>sp.GetRequiredService<ILoggerFactory>().CreateLogger<FilePromptProvider>());
appBuilder.Services.AddSingleton<IPromptProvider, FilePromptProvider>(); // Register as Singleton
appBuilder.Services.TryAddSingleton<ITopicTerminationService>(sp=>new DefaultTopicTerminationService(sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultTopicTerminationService>()));
appBuilder.Services.TryAddSingleton<IWorkflowPauseService>(sp => new DefaultWorkflowPauseService(sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultWorkflowPauseService>()));
appBuilder.Services.TryAddSingleton<ILogger<HybridToolInstanceProvider>>(sp=>sp.GetRequiredService<ILoggerFactory>().CreateLogger<HybridToolInstanceProvider>());
appBuilder.Services.TryAddSingleton<ILogger<BaseAgent>>(sp=>sp.GetRequiredService<ILoggerFactory>().CreateLogger<BaseAgent>());
//appBuilder.Services.TryAddSingleton(sp=>sp.GetRequiredService<ILoggerFactory>().CreateLogger<UIStreamListenerAgent>());
// Option 1: Simple Activator (only parameterless constructors)
// appBuilder.Services.AddSingleton<IToolInstanceProvider, ActivatorToolInstanceProvider>();

// Option 2: Hybrid (Uses DI if available, falls back to Activator)
// This requires access to the IServiceProvider itself, often available during configuration
appBuilder.Services.AddTransient<MathTool>();
appBuilder.Services.AddSingleton<IToolInstanceProvider,HybridToolInstanceProvider>();

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

appBuilder.Services.TryAddScoped<IChartRenderer,GenericChartRenderer>();

//appBuilder.Services.AddSingleton<IDataSchemaService, MSSQLDataSchemaService>();
//appBuilder.Services.AddSingleton<IChatClient, ResilientChatClient>();

var cache = new InMemoryDelegateCache();


// Register individual transformers
appBuilder.Services.AddSingleton<JsonataTransformer>();
appBuilder.Services.AddSingleton<DIMessageTransformer>();
appBuilder.Services.AddSingleton<ReflectionMessageTransformer>();



builder.Services.AddSingleton<ISerializationHelper>(serviceProvider =>
{
    var logger = serviceProvider.GetService<ILogger<SerializationHelper>>();
    var helper = new SerializationHelper(logger);
    
    // ============================================================
    // CUSTOM СТРАТЕГИИ ЗА ТВОИТЕ ТИПОВИ
    // ============================================================
    
    // 1. ChartContent - екстрактирај само потребните својства
    // helper.RegisterStrategy(typeof(ChartContent), obj =>
    // {
    //     var chart = (ChartContent)obj;
    //     return new Dictionary<string, object?>
    //     {
    //         ["Type"] = chart.Type,
    //         ["Library"] = chart.Library,
    //         ["Config"] = chart.Config,
    //         ["Data"] = chart.Data,
    //         // Додај ги само оние својства што ти требаат за JSONata
    //         // AIContent својствата автоматски ќе се игнорираат
    //     };
    // });
    
    // 2. Пример: Ако имаш StreamableResult
    // helper.RegisterStrategy(typeof(StreamableResult), obj =>
    // {
    //     var streamable = (StreamableResult)obj;
    //     // Наместо stream, врати само metadata
    //     return new Dictionary<string, object?>
    //     {
    //         ["IsComplete"] = streamable.IsComplete,
    //         ["Length"] = streamable.Length,
    //         // НЕ го враќаме самиот stream!
    //     };
    // });
    
    // 3. Пример: Ако имаш некоја internal класа што треба целосно да се игнорира
    // helper.RegisterIgnoredType(typeof(InternalCacheObject));
    
    return helper;
});

// Register the delegating transformer as the main IMessageTransformer
appBuilder.Services.AddSingleton<IMessageTransformer, DelegatingMessageTransformer>();


// DB Schema service
//appBuilder.Services.AddSingleton<IDataSchemaService, MSSQLDataSchemaService>();
appBuilder.Services.AddSingleton<IDataSchemaService, SQLiteDataSchemaService>();


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
appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, CacheDataStep>("CacheData");
appBuilder.Services.AddKeyedTransient<IBaseWorkflowStep, RetrieveFromCacheStep>("RetrieveFromCache");


// Register the SERVICES

appBuilder.Services.AddSingleton<IWorkflowHistoryStore, JsonFileWorkflowHistoryStore>();

appBuilder.Services.AddSingleton<IAIFunctionFactoryService,AIFunctionFactoryService>();



// Register the AD service
appBuilder.Services.AddSingleton<IADService, ADService>();

// Register the Excel service
appBuilder.Services.TryAddSingleton<IExcelService, ExcelService>();

// Register SQL cleanup service
appBuilder.Services.AddTransient<ICleanupSQLService, SQLiteCleanup>();

// Register the SQL executor service
appBuilder.Services.AddTransient<ISqlExecutorService, SQLiteExecutorService>();

appBuilder.Services.TryAddSingleton<IExpressionEvaluator>(sp =>
    new JsonataEvaluator(sp.GetRequiredService<ISerializationHelper>(), sp.GetRequiredService<ILogger<JsonataEvaluator>>())
);

appBuilder.Services.TryAddSingleton<IStatePersitanceProvider>(new JSONStatePeristanceProvider());
appBuilder.Services.TryAddSingleton<IDelegateCache>(cache);

// Register the UI Stream Broker and its listener
appBuilder.Services.AddSingleton<IUIStreamBroker, UIStreamBroker>();

// The DI container will automatically resolve dependencies for SchemaProcessorService's constructor
// as long as IDataSchemaService, IEmbeddingGenerator, IVectorStore, IOptions<...>,
// IDomainResolutionService, and ILogger are all registered.
appBuilder.Services.AddSingleton<SchemaProcessorService>(sp => new SchemaProcessorService(
    sp.GetRequiredService<IDataSchemaService>(),
    sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
    sp.GetRequiredService<IVectorStore>(),
    sp.GetRequiredService<IOptions<SchemaProcessingSettings>>(),
    
    sp.GetRequiredService<ILogger<SchemaProcessorService>>()));





// Client and Step Factories
// Register ChatClientFactory with 
appBuilder.Services.TryAddSingleton<IChatClientFactory>(sp =>
    new ChatClientFactory(sp.GetRequiredService<IOptions<AgentsConfiguration>>(), sp, sp.GetRequiredService<ILogger<ChatClientFactory>>()));

appBuilder.Services.AddSingleton<IWorkflowStepFactory, WorkflowStepFactory>();

// Register the embedding generator. This is needed by ExtractAnalysisStep.
// It uses the configuration from ValueMatcherOptions in appsettings.json.
appBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var valueMatcherOptions = sp.GetRequiredService<IOptions<ValueMatcherОptions>>().Value;
    if (valueMatcherOptions?.EmbeddingClient == null)
    {
        throw new InvalidOperationException(
            "The embedding client is not configured. Please ensure that 'ValueMatcherOptions:EmbeddingClient' is correctly set in your appsettings.json file.");
    }
    return sp.GetRequiredService<IChatClientFactory>().CreateEmbeddingGenerator(valueMatcherOptions.EmbeddingClient);
});




//[Izvestai].[rptPotrosenoVremePoProgrameriVoPeriod] '2016-01-01', '2025-05-01'

//[Izvestai].[rptREGZbirenIzvestaj]



// Dynamically load all agents from all workflows
var allWorkflows = builder.Configuration.GetSection("AgentsConfiguration:Workflows").Get<Dictionary<string, WorkflowDefinition>>() ?? new();


appBuilder.DeployAgents(allWorkflows);





#endregion
try
{
    var app1 = await appBuilder.BuildAsync();


    var runSchemaProcessing = app1.Services.GetRequiredService<IConfiguration>().GetValue<bool>("SchemaProcessingSettings:RunOnStartup");

    if (runSchemaProcessing)
    {
        using (var scope = app1.Services.CreateScope())
        {
            var schemaProcessorService = scope.ServiceProvider.GetRequiredService<SchemaProcessorService>();
            Debug.WriteLine("\n--- Започнување со обработка на шемата и домените (конфигурациски-управувано) ---");
            // Овој единствен повик го оркестрира целиот процес: скенирање на шеми,
            // наоѓање релевантни табели, и обработка на табели, колони, вредности и домени.
            await schemaProcessorService.ProcessAndEmbedSchemaAsync(
                CancellationToken.None
            );
            Debug.WriteLine("--- Обработката на шемата и домените е завршена ---");
        }
    }
    else
    {
        Debug.WriteLine("Skipping schema processing based on appsettings.json (SchemaProcessingSettings:RunOnStartup is false). Using existing vector stores.");
    }

    //BACKUP descriptions
    //var backupFileName = app1.Services.GetRequiredService<IConfiguration>().GetValue<string>("SchemaProcessingSettings:DescriptionPropertiesBackupFileName");
    //if (!string.IsNullOrWhiteSpace(backupFileName))
    //{
    //    using (var scope = app1.Services.CreateScope())
    //    {
    //        var schemaService = scope.ServiceProvider.GetRequiredService<IDataSchemaService>();
    //        var schemaProcessingSettings = scope.ServiceProvider.GetRequiredService<IOptions<SchemaProcessingSettings>>().Value;
    //        var schemasToScan = schemaProcessingSettings.SchemasToScan;

    //        Debug.WriteLine($"\n--- Generating schema descriptions backup to {backupFileName} ---");
    //        var backupPath = Path.Combine(AppContext.BaseDirectory, backupFileName);
    //        var backupScript = await schemaService.GenerateSchemaDescriptionsBackupAsync(schemasToScan, CancellationToken.None);
    //        await File.WriteAllTextAsync(backupPath, backupScript);
    //        Debug.WriteLine("--- Schema descriptions backup completed ---");
    //    }
    //}


    var webApp = (WebApplication)app1.Host;



    // Configure the HTTP request pipeline.
    if (!webApp.Environment.IsDevelopment())
    {
        webApp.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        webApp.UseHsts();
    }

    webApp.UseHttpsRedirection();
    webApp.UseAntiforgery();

    webApp.UseStaticFiles();
    webApp.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

   


    webApp.Run();
}
catch (Exception e)
{
    Debug.WriteLine(e?.Message);
}
