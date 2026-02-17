namespace Chat2Report.Providers
{
    public interface IStatePersitanceProvider
    
    {
        T Get<T>(string key);
        void Set<T>(string key, T value);
    }
}
