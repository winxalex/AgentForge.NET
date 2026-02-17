using Microsoft.Extensions.Caching.Memory;

namespace Chat2Report.Services
{
    /// <summary>
    /// An in-memory implementation of the IDataCacheService using IMemoryCache.
    /// </summary>
    public class InMemoryDataCacheService : IDataCacheService
    {
        private readonly IMemoryCache _memoryCache;

        public InMemoryDataCacheService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public T? Get<T>(string key) => _memoryCache.Get<T>(key);

        public void Remove(string key) => _memoryCache.Remove(key);

        public string Set<T>(T data, TimeSpan? absoluteExpirationRelativeToNow = null)
        {
            var key = Guid.NewGuid().ToString();
            var cacheEntryOptions = new MemoryCacheEntryOptions();

            if (absoluteExpirationRelativeToNow.HasValue)
            {
                cacheEntryOptions.SetAbsoluteExpiration(absoluteExpirationRelativeToNow.Value);
            }

            _memoryCache.Set(key, data, cacheEntryOptions);
            return key;
        }
    }
}