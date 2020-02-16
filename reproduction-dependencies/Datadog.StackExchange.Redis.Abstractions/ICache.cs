namespace Datadog.StackExchange.Redis.Abstractions
{
    public interface ICache
    {
        string GetString(string key);

        void SetString(string key, string value);
    }
}
