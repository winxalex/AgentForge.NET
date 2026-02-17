// --- In a new file: IToolInstanceProvider.cs ---

// --- In a new file: IToolInstanceProvider.cs ---
using System;

namespace Chat2Report.Providers // Or a suitable namespace
{
    /// <summary>
    /// Provides instances of tool types required for invoking non-static tool methods.
    /// Implementations can use various strategies like Activator.CreateInstance,
    /// dependency injection, or custom factories.
    /// </summary>
    public interface IToolInstanceProvider
    {
        /// <summary>
        /// Gets an instance of the specified tool type.
        /// Implementations are responsible for creating and potentially caching instances.
        /// </summary>
        /// <param name="toolType">The Type of the tool class required.</param>
        /// <returns>An instance of the tool type.</returns>
        /// <exception cref="InvalidOperationException">Thrown if an instance cannot be provided.</exception>
        object GetInstance(Type toolType);
    }
}