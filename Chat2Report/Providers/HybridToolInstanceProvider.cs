// --- In a new file: HybridToolInstanceProvider.cs ---
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace Chat2Report.Providers
{
    public class HybridToolInstanceProvider : IToolInstanceProvider
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly ConcurrentDictionary<Type, object> _instanceCache = new ConcurrentDictionary<Type, object>();
        private readonly ILogger<HybridToolInstanceProvider> _logger;

        // Constructor accepts optional IServiceProvider
        public HybridToolInstanceProvider(IServiceProvider? serviceProvider = null, ILogger<HybridToolInstanceProvider> logger = null)
        {
            _serviceProvider = serviceProvider; // Can be null
            _logger = logger;
             _logger?.LogInformation("HybridToolInstanceProvider initialized. ServiceProvider available: {HasServiceProvider}", _serviceProvider != null);
        }

        public object GetInstance(Type toolType)
        {
             // Check cache first
            if (_instanceCache.TryGetValue(toolType, out var cachedInstance))
            {
                return cachedInstance;
            }

            return _instanceCache.GetOrAdd(toolType, type => {
                object? instance = null;

                // 1. Try Service Type (if available)
                if (_serviceProvider != null)
                {
                    _logger?.LogDebug("Attempting to resolve {Type} from IServiceProvider.", type.FullName);
                    try
                    {
                        // Use GetService which returns null if not found, instead of GetRequiredService which throws
                        instance = _serviceProvider.GetService(type);
                        if (instance != null)
                        {
                            _logger?.LogInformation("Resolved instance of {Type} via IServiceProvider.", type.FullName);
                            return instance; // Found in DI, return it (cache happens via GetOrAdd)
                        }
                         _logger?.LogDebug("Type {Type} not found in IServiceProvider. Attempting fallback.", type.FullName);
                    }
                    catch (Exception ex)
                    {
                        // Log DI resolution errors but proceed to fallback
                        _logger?.LogWarning(ex, "Error resolving {Type} from IServiceProvider. Attempting fallback.", type.FullName);
                    }
                }
                else
                {
                     _logger?.LogTrace("IServiceProvider not available for resolving {Type}.", type.FullName);
                }


                // 2. Fallback to Activator.CreateInstance (parameterless constructor)
                _logger?.LogDebug("Attempting fallback to Activator.CreateInstance for {Type}.", type.FullName);
                try
                {
                    if (type.GetConstructor(Type.EmptyTypes) != null)
                    {
                        instance = Activator.CreateInstance(type);
                        if (instance != null)
                        {
                            _logger?.LogInformation("Created instance of {Type} via Activator.CreateInstance fallback.", type.FullName);
                            return instance; // Created via Activator, return it (cache happens via GetOrAdd)
                        }
                        // This case (Activator returning null) is highly unlikely if constructor exists
                        _logger?.LogError("Activator.CreateInstance returned null for type {Type} despite parameterless constructor existing.", type.FullName);
                    }
                    else
                    {
                        _logger?.LogWarning("Type {Type} has no public parameterless constructor. Cannot use Activator.CreateInstance fallback.", type.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed fallback attempt to create instance of {Type} using Activator.CreateInstance.", type.FullName);
                    // Don't throw here yet, throw comprehensive error at the end if nothing worked
                }

                // 3. Failed to obtain instance by any means
                _logger?.LogError("Failed to get or create instance for type {Type} using available methods (ServiceProvider: {HasServiceProvider}).", type.FullName, _serviceProvider != null);
                throw new InvalidOperationException($"Could not provide an instance for tool type '{type.FullName}'. Check DI registration or ensure a public parameterless constructor exists for Activator fallback.");
            });
        }
    }
}