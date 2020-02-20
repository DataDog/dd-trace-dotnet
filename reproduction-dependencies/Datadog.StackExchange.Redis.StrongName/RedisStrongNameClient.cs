using System;
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

        public string GetString(string key)
        {
            return _database.StringGet(key);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
