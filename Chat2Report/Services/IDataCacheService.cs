namespace Chat2Report.Services
{
    /// <summary>
    /// Defines a contract for a simple key-value cache service.
    /// </summary>
    public interface IDataCacheService
    {
        string Set<T>(T data, TimeSpan? absoluteExpirationRelativeToNow = null);
        T? Get<T>(string key);
        void Remove(string key);
    }
}