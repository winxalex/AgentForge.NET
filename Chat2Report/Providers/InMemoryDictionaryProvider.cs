using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Chat2Report.Providers
{
    public class InMemoryDictionaryProvider:IStatePersitanceProvider
    {
        private ConcurrentDictionary<string, object> _memory = new();

        public T Get<T>(string key) => (T)_memory.GetValueOrDefault(key);
        public void Set<T>(string key, T value) => _memory.AddOrUpdate(key, value, (_, _) => value);
    }
}
