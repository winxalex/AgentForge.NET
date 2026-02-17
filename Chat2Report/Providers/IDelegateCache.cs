// --- In file: IDelegateCache.cs ---
using System;
using System.Collections.Generic;
using System.Reflection; // Added
using System.Threading.Tasks;

namespace Chat2Report.Providers
{
    public interface IDelegateCache
    {
        /// <summary>
        /// Gets or adds an object (typically Delegate or MethodInfo) from the cache.
        /// The factory is responsible for creating the correct object type.
        /// </summary>
        /// <param name="key">The composite key identifying the cached item.</param>
        /// <param name="factory">A function that creates the object if it's not in the cache.</param>
        /// <returns>The cached or newly created object.</returns>
        object GetOrAdd(DelegateCacheKey key, Func<DelegateCacheKey, object> factory);

        /// <summary>
        /// Gets or adds a delegate using reflection if not found.
        /// This is a convenience wrapper around GetOrAdd.
        /// </summary>
        /// <typeparam name="TDelegate">The specific delegate type expected.</typeparam>
        /// <param name="key">The composite key identifying the delegate.</param>
        /// <param name="factory">A function that creates the delegate if it's not in the cache.</param>
        /// <returns>The cached or newly created delegate, cast to TDelegate.</returns>
        TDelegate GetOrAddDelegate<TDelegate>(DelegateCacheKey key, Func<DelegateCacheKey, TDelegate> factory) where TDelegate : Delegate;

        /// <summary>
        /// Gets or adds the MethodInfo for a tool method.
        /// Assumes static methods by default, adjust factory if instance methods are needed.
        /// </summary>
        /// <param name="typeName">The assembly-qualified name of the type containing the tool method.</param>
        /// <param name="methodName">The name of the tool method.</param>
        /// <returns>The cached or newly retrieved MethodInfo.</returns>
        MethodInfo GetOrAddMethodInfo(string typeName, string methodName);

        // Keep specific delegate helpers only if truly needed for fixed signatures
        // Remove GetOrAddToolDelegate as it's too specific
        // Func<string, Task<string>> GetOrAddToolDelegate(string typeName, string methodName); // REMOVED

        /// <summary>
        /// Gets or adds a delegate for a transformer method (example of keeping a specific helper).
        /// </summary>
        Func<Dictionary<string, object>,  Dictionary<string, object>> GetOrAddTransformerDelegate(string typeName, string methodName);

        void Clear();
    }
}