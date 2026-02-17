namespace Chat2Report.Providers
{
    /// <summary>
    /// Provides prompt text, potentially loading it from external sources.
    /// </summary>
    public interface IPromptProvider
    {
        /// <summary>
        /// Gets the prompt content based on an identifier.
        /// If the identifier points to a tracked source (like a file),
        /// this should return the latest version.
        /// </summary>
        /// <param name="promptIdentifier">Identifier for the prompt (e.g., literal text, "file:path/to/file.txt").</param>
        /// <returns>The prompt content as a string.</returns>
        Task<string> GetPromptAsync(string promptIdentifier);
    }
}