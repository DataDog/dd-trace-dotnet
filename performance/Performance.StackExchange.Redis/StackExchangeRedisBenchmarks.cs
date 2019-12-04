using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using StackExchange.Redis;

namespace Performance.StackExchange.Redis
{
    [MinColumn, MaxColumn]
    [MarkdownExporter, AsciiDocExporter, HtmlExporter, CsvExporter]
    [Config(typeof(DatadogBenchmarkConfig))]
    public class StackExchangeRedisBenchmarks
    {
        private static readonly string ReallyBigString = string.Join("", Enumerable.Range(0, 10_000).Select(i => "-"));
        private static ConnectionMultiplexer _multiplexer = null;
        private static IDatabase _database = null;

        [Benchmark]
        public RedisResult EvalSetLargeString() => DoEvalSetOnLargeString();

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

        private static RedisResult DoEvalSetOnLargeString()
        {
            var db = GetDatabase();
            const string script = "redis.call('set', @key, @value)";
            var uniqueKeyText = Guid.NewGuid().ToString();
            var prepared = LuaScript.Prepare(script);
            return db.ScriptEvaluate(prepared, new { key = (RedisKey)uniqueKeyText, value = ReallyBigString });
        }
    }
}
