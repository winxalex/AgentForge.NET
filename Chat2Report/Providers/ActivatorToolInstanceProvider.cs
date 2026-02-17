// --- In a new file: ActivatorToolInstanceProvider.cs ---
using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Chat2Report.Providers
{
    public class ActivatorToolInstanceProvider : IToolInstanceProvider
    {
        // Cache instances once created
        private readonly ConcurrentDictionary<Type, object> _instanceCache = new ConcurrentDictionary<Type, object>();
        private readonly ILogger<ActivatorToolInstanceProvider> _logger;

        public ActivatorToolInstanceProvider(ILogger<ActivatorToolInstanceProvider> logger = null)
        {
            _logger = logger;
        }

        public object GetInstance(Type toolType)
        {
            return _instanceCache.GetOrAdd(toolType, type => {
                _logger?.LogDebug("Attempting to create instance of {Type} using Activator.", type.FullName);
                try
                {
                    // Ensure there's a public parameterless constructor for a clearer error
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                    {
                         _logger?.LogError("Type {Type} does not have a public parameterless constructor. ActivatorToolInstanceProvider cannot create an instance.", type.FullName);
                         throw new InvalidOperationException($"Type '{type.FullName}' must have a public parameterless constructor to be used with {nameof(ActivatorToolInstanceProvider)}.");
                    }

                    object instance = Activator.CreateInstance(type);
                    if (instance == null) // Should not happen if constructor exists, but defensive check
                    {
                         throw new InvalidOperationException($"Activator.CreateInstance returned null for type '{type.FullName}'.");
                    }
                     _logger?.LogInformation("Successfully created and cached instance of {Type} using Activator.", type.FullName);
                    return instance;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to create instance of {Type} using Activator.CreateInstance.", type.FullName);
                    // Re-throw a more specific exception
                    throw new InvalidOperationException($"Failed to create instance of tool type '{type.FullName}' using Activator. See inner exception.", ex);
                }
            });
        }
    }
}