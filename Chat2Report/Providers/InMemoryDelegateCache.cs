// --- In file: InMemoryDelegateCache.cs ---
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chat2Report.Providers
{
    public class InMemoryDelegateCache : IDelegateCache
    {
        // Store object to allow caching Delegates, MethodInfos, or potentially other reflection artifacts
        private readonly ConcurrentDictionary<DelegateCacheKey, object> _cache = new ConcurrentDictionary<DelegateCacheKey, object>();
        private readonly ILogger<InMemoryDelegateCache> _logger;

        public InMemoryDelegateCache(ILogger<InMemoryDelegateCache> logger = null)
        {
            _logger = logger;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        // Core method storing/retrieving object
        public object GetOrAdd(DelegateCacheKey key, Func<DelegateCacheKey, object> factory)
        {
            return _cache.GetOrAdd(key, k =>
            {
                _logger?.LogDebug("Cache miss for key: {Key}. Creating object.", k);
                try
                {
                    var createdObject = factory(k);
                    if (createdObject == null)
                    {
                        // Decide if nulls should be cached or if it indicates an error
                        _logger?.LogWarning("Factory for key {Key} returned null.", k);
                        // Depending on policy, you might throw or cache a specific marker object.
                        // For now, let's prevent caching nulls directly as it might hide errors.
                        throw new InvalidOperationException($"Factory for key {k} returned null.");
                    }
                    _logger?.LogTrace("Object created for key {Key}. Type: {ObjectType}", k, createdObject.GetType().Name);
                    return createdObject;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to create object for key: {Key}", k);
                    // Re-throw or return null/marker depending on desired error handling
                    throw;
                }
            });
        }

        // Generic delegate helper using the core method
        public TDelegate GetOrAddDelegate<TDelegate>(DelegateCacheKey key, Func<DelegateCacheKey, TDelegate> factory) where TDelegate : Delegate
        {
            // Pass the specific factory to the core GetOrAdd method
            object cachedObject = GetOrAdd(key, k => factory(k)); // Factory already returns TDelegate, which is an object

            // Attempt to cast the retrieved object to the desired delegate type
            if (cachedObject is TDelegate specificDelegate)
            {
                return specificDelegate;
            }
            else
            {
                // This indicates a programming error - the wrong type was cached for this key,
                // or the factory didn't return the expected type.
                _logger?.LogError("Cached object for key {Key} is of type {ActualType}, but expected {ExpectedType}.", key, cachedObject?.GetType().FullName ?? "null", typeof(TDelegate).FullName);
                throw new InvalidCastException($"Cached object for key {key} is not of the expected delegate type {typeof(TDelegate).FullName}. Found {cachedObject?.GetType().FullName ?? "null"}.");
            }
        }


        // Helper implementation for getting MethodInfo
        public MethodInfo GetOrAddMethodInfo(string typeName, string methodName)
        {

            var key = new DelegateCacheKey(typeName, methodName);

            object cachedObject = GetOrAdd(key, cacheKey =>
            {
                // Factory logic to find the MethodInfo
                var type = Type.GetType(cacheKey.TypeName, throwOnError: true);
                if (type == null) // Should be caught by throwOnError, but defensive check
                    throw new TypeLoadException($"Could not load type '{cacheKey.TypeName}'.");

                // Find the method - consider binding flags carefully.
                // Public static is common for tools, but instance might be needed.
                // Add BindingFlags.Instance if you need to support non-static methods.
                // Add BindingFlags.IgnoreCase if needed.
                var methodInfo = type.GetMethod(cacheKey.MethodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance); // Added Instance flag

                if (methodInfo == null)
                    throw new MissingMethodException(cacheKey.TypeName, cacheKey.MethodName);

                // Log details about the found method (optional)
                _logger?.LogTrace("Found MethodInfo for Tool {Key}: Static={IsStatic}, Return={ReturnType}, Params={ParamCount}",
                    cacheKey, methodInfo.IsStatic, methodInfo.ReturnType.Name, methodInfo.GetParameters().Length);

                // Return the MethodInfo itself to be cached
                return methodInfo;
            });

            // Cast the result (should always be MethodInfo if the factory worked)
            if (cachedObject is MethodInfo methodInfoResult)
            {
                return methodInfoResult;
            }
            else
            {
                // Should not happen if factory returns MethodInfo and GetOrAdd doesn't cache nulls
                _logger?.LogError("Cached object for tool key {Key} is not a MethodInfo. Type: {ActualType}", key, cachedObject?.GetType().FullName ?? "null");
                throw new InvalidCastException($"Cached object for tool key {key} is not a MethodInfo. Found {cachedObject?.GetType().FullName ?? "null"}.");
            }
        }

        // Helper implementation for Transformers (using the generic delegate helper)
        public Func<Dictionary<string, object>, Dictionary<string, object>> GetOrAddTransformerDelegate(string typeName, string methodName)
        {
            var key = new DelegateCacheKey(typeName, methodName);
            var expectedDelegateType = typeof(Func<Dictionary<string, object>, IDictionary<string, object>, Dictionary<string, object>>);

            // Use the generic GetOrAddDelegate helper
            return GetOrAddDelegate(key, cacheKey =>
            {
                // Factory logic specific to creating this delegate type
                var type = Type.GetType(cacheKey.TypeName, throwOnError: true);
                var methodInfo = type.GetMethod(cacheKey.MethodName, BindingFlags.Public | BindingFlags.Static); // Assuming static for transformers
                if (methodInfo == null) throw new MissingMethodException(cacheKey.TypeName, cacheKey.MethodName);

                // Check for Description attribute
                var descriptionAttribute = methodInfo.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute == null)
                {
                    _logger?.LogWarning("Method {MethodName} in type {TypeName} does not have a Description attribute.", cacheKey.MethodName, cacheKey.TypeName);
                }

                try
                {
                    // Create and return the specific delegate type
                    return (Func<Dictionary<string, object>, Dictionary<string, object>>)
                           Delegate.CreateDelegate(expectedDelegateType, methodInfo, throwOnBindFailure: true);
                }
                catch (ArgumentException ex) // Catches binding failures
                {
                    _logger?.LogError(ex, "Failed to bind delegate for Transformer {Key}. Method signature mismatch?", cacheKey);
                    throw new InvalidOperationException($"Method '{cacheKey.MethodName}' on type '{cacheKey.TypeName}' does not match the required Transformer signature '{expectedDelegateType.Name}'.", ex);
                }
            });
        }
    }
}

