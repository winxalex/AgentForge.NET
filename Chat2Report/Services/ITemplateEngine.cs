namespace Chat2Report.Services
{
    /// <summary>
    /// Defines an interface for a template processing engine.
    /// </summary>
    public interface ITemplateEngine
    {
        /// <summary>
        /// Compiles a template string with the provided data object.
        /// </summary>
        string Compile(string template, object data);
    }
}