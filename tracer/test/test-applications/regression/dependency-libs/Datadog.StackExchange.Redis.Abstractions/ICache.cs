using System.Threading.Tasks;

namespace Datadog.StackExchange.Redis.Abstractions
{
    public interface ICache
    {
        string GetString(string key);

        Task<string> GetStringAsync(string key);

        void SetString(string key, string value);

        Task SetStringAsync(string key, string value);
    }
}
