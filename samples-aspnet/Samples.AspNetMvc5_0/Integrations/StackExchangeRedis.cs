using System;
using System.Linq;
using StackExchange.Redis;

namespace Samples.AspNetMvc5_0.Controllers
{
    public class StackExchangeRedis
    {
        private static readonly string ReallyBigString = string.Join("", Enumerable.Range(0, 10_000).Select(i => "-"));
        private static ConnectionMultiplexer _multiplexer = null;
        private static IDatabase _database = null;

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
        }

        private static ConnectionMultiplexer GetMultiplexer()
        {
            return _multiplexer ?? (_multiplexer = ConnectionMultiplexer.Connect(Host() + ",allowAdmin=true"));
        }

        private static IDatabase GetDatabase()
        {
            return _database ?? (_database = GetMultiplexer().GetDatabase());
        }

        public static RedisResult DoEvalSetOnLargeString()
        {
            var db = GetDatabase();
            const string script = "redis.call('set', @key, @value)";
            var uniqueKeyText = Guid.NewGuid().ToString();
            var prepared = LuaScript.Prepare(script);
            return db.ScriptEvaluate(prepared, new { key = (RedisKey)uniqueKeyText, value = ReallyBigString });
        }
    }
}
