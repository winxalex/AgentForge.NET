
namespace Chat2Report.Models
{
    /// <summary>
    /// Represents the settings for the data store connection.
    /// </summary>
    /// <remarks>
    /// This class is used to configure the connection string for the data store.
    /// </remarks>

    public class DataStoreSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
