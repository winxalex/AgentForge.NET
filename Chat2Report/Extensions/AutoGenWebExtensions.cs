// AutoGenWebIntegration/AutoGenWebExtensions.cs
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core; // Required for InProcessRuntime, BaseAgent
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting; // Required for IHostedService registration
using Microsoft.Extensions.Logging; // For logging within extensions if needed
using System.Reflection;

namespace Chat2Report.Extensions;

public static class AutoGenWebExtensions
{

    /// <summary>
    /// Holds information needed to register an agent.
    /// Stored within AutoGenAgentOptions.
    /// </summary>
    public class AgentRegistrationInfo
    {
        public AgentType AgentType { get; } // AgentType is struct
        public Type RuntimeType { get; }
        public bool SkipClassSubscriptions { get; }
        public bool SkipDirectMessageSubscription { get; }

        // Constructor receives an already created AgentType struct
        public AgentRegistrationInfo(AgentType agentType, Type runtimeType, bool skipClassSubscriptions, bool skipDirectMessageSubscription)
        {
            AgentType = agentType; // Struct assignment
            RuntimeType = runtimeType ?? throw new ArgumentNullException(nameof(runtimeType));
            SkipClassSubscriptions = skipClassSubscriptions;
            SkipDirectMessageSubscription = skipDirectMessageSubscription;
        }
    }

    /// <summary>
    /// Configuration options for AutoGen setup and initialization.
    /// Contains agent registrations and other setup-related configuration.
    /// </summary>
    public class AutoGenSetupOptions
    {
        /// <summary>
        /// List of agent registrations to be processed during initialization.
        /// </summary>
        public List<AgentRegistrationInfo> AgentRegistrations { get; } = new();

        /// <summary>
        /// Additional setup configuration options can be added here.
        /// </summary>
    }


    /// <summary>
    /// Adds core AutoGen services and the initialization service required for web integration.
    /// This should generally be called before adding specific agents or runtimes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAutoGenWebIntegration(this IServiceCollection services)
    {
        // Register the options class
        services.AddOptions<AutoGenSetupOptions>();

        // Register the hosted service that performs registration *after* build
        services.AddHostedService<AutoGenInitializationService>();

        // Add a marker service or log to indicate integration is added
        services.TryAddSingleton<AutoGenWebMarkerService>();
        // Consider logging: services.AddLogging(); needed? Usually added by WebApplication.CreateBuilder

        return services;
    }

    // Internal marker service to check if AddAutoGenWebIntegration was called
    private sealed class AutoGenWebMarkerService { }

    /// <summary>
    /// Configures AutoGen to use the InProcessRuntime.
    /// Registers IAgentRuntime as a Singleton and the InProcessRuntime as an IHostedService.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="deliverToSelf">Whether messages published by an agent should also be delivered back to itself if subscribed.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection UseAutoGenInProcessRuntime(this IServiceCollection services, bool deliverToSelf = false)
    {
        // Ensure core web integration is added first
        services.AddAutoGenWebIntegration();

        // Register the InProcessRuntime as the IAgentRuntime implementation
        services.AddSingleton<IAgentRuntime, InProcessRuntime>(_ => new InProcessRuntime { DeliverToSelf = deliverToSelf });

        // Crucially, register the *same instance* as an IHostedService so its StartAsync/StopAsync are called by the host.
        // Get the singleton instance registered above.
        services.AddHostedService(sp =>
        {
            var runtime = sp.GetRequiredService<IAgentRuntime>() as InProcessRuntime;
            if (runtime == null)
            {
                // This should ideally not happen if registration is done correctly
                throw new InvalidOperationException($"{nameof(InProcessRuntime)} was registered as {nameof(IAgentRuntime)} but could not be resolved as {nameof(InProcessRuntime)}. Check DI configuration.");
            }
            return runtime;
        });

        return services;
    }

    /// <summary>
    /// Adds an agent registration to be processed when the application starts.
    /// </summary>
    /// <typeparam name="TAgent">The concrete implementation type of the agent. Must implement IHostableAgent.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="agentTypeName">The symbolic type name for this agent.</param>
    /// <param name="skipClassSubscriptions">Whether to skip subscriptions defined via attributes on the agent class.</param>
    /// <param name="skipDirectMessageSubscription">Whether to skip the implicit subscription for direct messages (AgentType:).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if TAgent does not implement IHostableAgent or is abstract.</exception>
    public static IServiceCollection AddAutoGenAgent<TAgent>(
        this IServiceCollection services,
        string agentTypeName,
        bool skipClassSubscriptions = false,
        bool skipDirectMessageSubscription = false)
        where TAgent : class, IHostableAgent // Add class constraint for clarity if ActivatorUtilities is used implicitly later
    {
       

        AgentType agentType = agentTypeName;
        var runtimeType = typeof(TAgent);

        // Validate early
        if (!typeof(IHostableAgent).IsAssignableFrom(runtimeType))
        {
            throw new ArgumentException($"Type {runtimeType.FullName} must implement {nameof(IHostableAgent)}.", nameof(TAgent));
        }
        if (runtimeType.IsAbstract)
        {
            throw new ArgumentException($"Type {runtimeType.FullName} cannot be abstract.", nameof(TAgent));
        }

        // Configure the options to include this registration info
        services.Configure<AutoGenSetupOptions>(options =>
        {
            var registrationInfo = new AgentRegistrationInfo(
                agentType,
                runtimeType,
                skipClassSubscriptions,
                skipDirectMessageSubscription);
            options.AgentRegistrations.Add(registrationInfo);
        });

        // Optional: Register the agent type itself in DI if it needs to be resolved elsewhere.
        // The AgentRuntime uses ActivatorUtilities by default, which doesn't strictly require this,
        // but it can be useful for consistency or if agents have complex dependencies.
        // Choose the appropriate lifetime (Transient is often safest for agents unless designed otherwise).
        services.TryAddTransient<TAgent>();

        return services;
    }

    /// <summary>
    /// Scans specified assemblies for concrete types inheriting from BaseAgent
    /// and adds them using their type name as the AgentType name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAutoGenAgentsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        // Ensure core web integration is added first
        services.AddAutoGenWebIntegration();

        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetEntryAssembly()!]; // Default to entry assembly if none provided
        }

        var agentImplementationTypes = assemblies
            .Where(a => a != null)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IHostableAgent).IsAssignableFrom(type))
            // Optional: Add check for BaseAgent if you specifically want *that* hierarchy,
            // but IHostableAgent is the contract the runtime uses.
            // .Where(type => ReflectionHelper.IsSubclassOfGeneric(type, typeof(BaseAgent))) // Using original logic if needed
            .ToList(); // Materialize the list

        var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger(typeof(AutoGenWebExtensions)); // Temporary SP to get _logger

        logger?.LogInformation("Scanning assemblies for AutoGen agents: {Assemblies}", string.Join(", ", assemblies.Select(a => a?.GetName().Name ?? "null")));

        foreach (Type agentImplType in agentImplementationTypes)
        {
            try
            {
                // Use the class name as the agent type name by default
                var agentTypeName = agentImplType.Name;
                logger?.LogDebug("Found potential agent implementation: {AgentImplType}. Registering with type name: {AgentTypeName}", agentImplType.FullName, agentTypeName);

                // Configure the options to include this registration info
                services.Configure<AutoGenSetupOptions>(options =>
                {
                    var registrationInfo = new AgentRegistrationInfo(
                       agentTypeName,
                       agentImplType,
                       false,
                       false);
                    options.AgentRegistrations.Add(registrationInfo);
                });

                // Optional: Add to DI
                services.TryAddTransient(agentImplType); // Register concrete type
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to configure agent registration for type {AgentImplType}", agentImplType.FullName);
                // Continue scanning other types
            }
        }
        logger?.LogInformation("Finished scanning assemblies. Found {Count} potential agent implementations.", agentImplementationTypes.Count);

        return services;
    }

    /// <summary>
    /// Scans the calling assembly and currently loaded AppDomain assemblies for concrete types inheriting from BaseAgent
    /// and adds them using their type name as the AgentType name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAutoGenAgentsFromAppDomain(this IServiceCollection services)
    {
        return services.AddAutoGenAgentsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    }
}