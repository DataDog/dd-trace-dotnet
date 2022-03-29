using System;
using System.Threading.Tasks;
using Datadog.StackExchange.Redis.Abstractions;
using StackExchange.Redis;

namespace Datadog.StackExchange.Redis.StrongName
{
    public class RedisStrongNameClient : ICache, IDisposable
    {
        private readonly ConnectionMultiplexer _connection;
        private readonly IDatabase _database;

        public RedisStrongNameClient(string configuration)
        {
            _connection = ConnectionMultiplexer.Connect(configuration);
            _database = _connection.GetDatabase();
        }

        public void SetString(string key, string value)
        {
            _database.StringSet(key, value);
        }

        public Task SetStringAsync(string key, string value)
        {
            return _database.StringSetAsync(key, value);
        }

        public string GetString(string key)
        {
            return _database.StringGet(key);
        }

        public async Task<string> GetStringAsync(string key)
        {
            // await so we can take the RedisValue return value and return it in a Task<string>
            return await _database.StringGetAsync(key);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
