// AutoGenWebIntegration/AutoGenInitializationService.cs
using Microsoft.AutoGen.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Chat2Report.Extensions.AutoGenWebExtensions;

namespace Chat2Report.Extensions;

/// <summary>
/// An IHostedService responsible for registering AutoGen agents
/// with the IAgentRuntime after the application has started and
/// the runtime is available.
/// </summary>
internal class AutoGenInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<AutoGenSetupOptions> _options;
    private readonly ILogger<AutoGenInitializationService> _logger;

    // Flag to prevent duplicate registration if StartAsync is called multiple times (shouldn't happen with default host)
    private volatile bool _agentsRegistered = false;
    private readonly object _registrationLock = new object();

    public AutoGenInitializationService(
        IServiceProvider serviceProvider, // Use IServiceProvider to allow scoped resolution if needed later
        IOptions<AutoGenSetupOptions> options,
        ILogger<AutoGenInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Ensure registration happens only once
        if (_agentsRegistered)
        {
            _logger.LogWarning("Agent registration already performed or in progress. Skipping.");
            return;
        }

        lock (_registrationLock)
        {
            if (_agentsRegistered) return; // Double check lock
                                           // Set flag early to prevent race conditions if registration is slow
            _agentsRegistered = true;
        }

        _logger.LogInformation("Starting AutoGen agent registration process...");

        // Create a scope for the registration process. This ensures any scoped
        // dependencies required during agent activation are handled correctly.
        // Although AgentRuntime itself is often Singleton, the *activation* might need scoped services.
        using var scope = _serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        IAgentRuntime? runtime = null;

        try
        {
            runtime = scopedProvider.GetRequiredService<IAgentRuntime>();
            _logger.LogDebug("IAgentRuntime resolved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to resolve IAgentRuntime. AutoGen agents cannot be registered.");
            // We should probably prevent the application from fully starting or indicate a critical failure.
            // For now, just log and stop registration. Depending on requirements, re-throwing might be appropriate.
            _agentsRegistered = false; // Reset flag if runtime resolution fails
            return; // or throw new InvalidOperationException("Failed to resolve IAgentRuntime", ex);
        }

        var agentRegistrations = _options.Value?.AgentRegistrations;

        if (agentRegistrations == null || agentRegistrations.Count == 0)
        {
            _logger.LogInformation("No AutoGen agents configured for registration.");
            return;
        }

        _logger.LogInformation($"Found {agentRegistrations.Count} agent types to register.");

        foreach (var regInfo in agentRegistrations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Agent registration cancelled.");
                _agentsRegistered = false; // Reset flag as registration was incomplete
                return;
            }

            try
            {
                _logger.LogDebug("Registering agent type '{AgentType}' with implementation {RuntimeType}", regInfo.AgentType, regInfo.RuntimeType.Name);

                // Use the existing extension methods from AutoGen.Contracts (AgentRuntimeExtensions)
                // Pass the scoped service provider for agent activation
                await runtime.RegisterAgentTypeAsync(regInfo.AgentType, regInfo.RuntimeType, scopedProvider);
                await runtime.RegisterImplicitAgentSubscriptionsAsync(regInfo.AgentType, regInfo.RuntimeType, regInfo.SkipClassSubscriptions, regInfo.SkipDirectMessageSubscription);

                _logger.LogInformation("Successfully registered agent type '{AgentType}'.", regInfo.AgentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register agent type '{AgentType}' with implementation {RuntimeType}.", regInfo.AgentType, regInfo.RuntimeType.Name);
                // Decide whether to continue or stop? Log and continue is often more robust.
            }
        }

        _logger.LogInformation("AutoGen agent registration process completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No specific cleanup needed for registration itself.
        // The IAgentRuntime's own StopAsync (if it's also an IHostedService like InProcessRuntime)
        // will handle runtime shutdown.
        _logger.LogInformation("AutoGen Initialization Service stopping.");
        return Task.CompletedTask;
    }
}