using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class StackExchangeRedisTests : TestHelper
    {
        private const int AgentPort = 9003;

        public StackExchangeRedisTests(ITestOutputHelper output)
            : base("RedisCore", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            var prefix = $"{BuildParameters.Configuration}.{BuildParameters.TargetFramework}.";
            using (var agent = new MockTracerAgent(AgentPort))
            using (var processResult = RunSampleAndWaitForExit(AgentPort, arguments: $"StackExchange {prefix}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(8).Where(s => s.Type == "redis").OrderBy(s => s.Start).ToList();

                foreach (var span in spans)
                {
                    Assert.Equal(Redis.OperationName, span.Name);
                    Assert.Equal($"Samples.RedisCore-{Redis.ServiceName}", span.Service);
                    Assert.Equal(SpanTypes.Redis, span.Type);
                    Assert.Equal("localhost", span.Tags.Get<string>("out.host"));
                    Assert.Equal("6379", span.Tags.Get<string>("out.port"));
                }

                var expected = new TupleList<string, string>
                {
                    { "SET", $"SET {prefix}StackExchange.Redis.INCR" },
                    { "PING", "PING" },
                    { "DDCUSTOM", "DDCUSTOM" },
                    { "ECHO", "ECHO" },
                    { "SLOWLOG", "SLOWLOG" },
                    { "INCR", $"INCR {prefix}StackExchange.Redis.INCR" },
                    { "INCRBYFLOAT", $"INCRBYFLOAT {prefix}StackExchange.Redis.INCR" },
                    { "GET", $"GET {prefix}StackExchange.Redis.INCR" },
                    { "TIME", "TIME" },
                };

                prefix = $"{prefix}StackExchange.Redis.Batch.";
                expected.AddRange(new TupleList<string, string>
                {
                    { "DEBUG", $"DEBUG {prefix}DebugObjectAsync" },
                    { "DDCUSTOM", $"DDCUSTOM" },
                    { "GEOADD", $"GEOADD {prefix}GeoAddAsync" },
                    { "GEODIST", $"GEODIST {prefix}GeoDistanceAsync" },
                    { "GEOHASH", $"GEOHASH {prefix}GeoHashAsync" },
                    { "GEOPOS", $"GEOPOS {prefix}GeoPositionAsync" },
                    { "GEORADIUSBYMEMBER", $"GEORADIUSBYMEMBER {prefix}GeoRadiusAsync" },
                    { "ZREM", $"ZREM {prefix}GeoRemoveAsync" },
                    { "HINCRBYFLOAT", $"HINCRBYFLOAT {prefix}HashDecrementAsync" },
                    { "HDEL", $"HDEL {prefix}HashDeleteAsync" },
                    { "HEXISTS", $"HEXISTS {prefix}HashExistsAsync" },
                    { "HGETALL", $"HGETALL {prefix}HashGetAllAsync" },
                    { "HINCRBYFLOAT", $"HINCRBYFLOAT {prefix}HashIncrementAsync" },
                    { "HKEYS", $"HKEYS {prefix}HashKeysAsync" },
                    { "HLEN", $"HLEN {prefix}HashLengthAsync" },
                    { "HMSET", $"HMSET {prefix}HashSetAsync" },
                    { "HVALS", $"HVALS {prefix}HashValuesAsync" },
                    { "PFADD", $"PFADD {prefix}HyperLogLogAddAsync" },
                    { "PFCOUNT", $"PFCOUNT {prefix}HyperLogLogLengthAsync" },
                    { "PFMERGE", $"PFMERGE {prefix}HyperLogLogMergeAsync" },
                    { "PING", $"PING" },
                    { "DEL", $"DEL key" },
                    { "DUMP", $"DUMP key" },
                    { "EXISTS", $"EXISTS key" },
                    { "PEXPIREAT", $"PEXPIREAT key" },
                    { "MOVE", $"MOVE key" },
                    { "PERSIST", $"PERSIST key" },
                    { "RANDOMKEY", $"RANDOMKEY" },
                    { "RENAME", "RENAME key1" },
                    { "RESTORE", "RESTORE key" },
                    { "TYPE", "TYPE key" },
                    { "LINDEX", "LINDEX listkey" },
                    { "LINSERT", "LINSERT listkey" },
                    { "LINSERT", "LINSERT listkey" },
                    { "LPOP", "LPOP listkey" },
                    { "LPUSH", "LPUSH listkey" },
                    { "LLEN", "LLEN listkey" },
                    { "LRANGE", "LRANGE listkey" },
                    { "LREM", "LREM listkey" },
                    { "RPOP", "RPOP listkey" },
                    { "RPOPLPUSH", "RPOPLPUSH listkey" },
                    { "RPUSH", "RPUSH listkey" },
                    { "LSET", "LSET listkey" },
                    { "LTRIM", "LTRIM listkey" },
                    { "GET", "GET listkey" },
                    { "SET", "SET listkey" },
                    { "PUBLISH", "PUBLISH channel" },
                    { "SADD", "SADD setkey" },
                    { "SUNIONSTORE", "SUNIONSTORE setkey" },
                    { "SUNION", "SUNION setkey1" },
                    { "SISMEMBER", "SISMEMBER setkey" },
                    { "SCARD", "SCARD setkey" },
                    { "SMEMBERS", "SMEMBERS setkey" },
                    { "SMOVE", "SMOVE setkey1" },
                    { "SPOP", "SPOP setkey1" },
                    { "SRANDMEMBER", "SRANDMEMBER setkey" },
                    { "SRANDMEMBER", "SRANDMEMBER setkey" },
                    { "SREM", "SREM setkey" },
                    { "SORT", "SORT setkey" },
                    { "SORT", "SORT setkey" },
                    { "ZADD", "ZADD ssetkey" },
                    { "ZUNIONSTORE", "ZUNIONSTORE ssetkey1" },
                    { "ZINCRBY", "ZINCRBY ssetkey" },
                    { "ZINCRBY", "ZINCRBY ssetkey" },
                    { "ZCARD", "ZCARD ssetkey" },
                    { "ZLEXCOUNT", "ZLEXCOUNT ssetkey" },
                    { "ZRANGE", "ZRANGE ssetkey" },
                    { "ZRANGE", "ZRANGE ssetkey" },
                    { "ZRANGEBYSCORE", "ZRANGEBYSCORE ssetkey" },
                    { "ZRANGEBYSCORE", "ZRANGEBYSCORE ssetkey" },
                    { "ZRANGEBYLEX", "ZRANGEBYLEX ssetkey" },
                    { "ZRANK", "ZRANK ssetkey" },
                    { "ZREM", "ZREM ssetkey" },
                    { "ZREMRANGEBYRANK", "ZREMRANGEBYRANK ssetkey" },
                    { "ZREMRANGEBYSCORE", "ZREMRANGEBYSCORE ssetkey"  },
                    { "ZREMRANGEBYLEX", "ZREMRANGEBYLEX ssetkey" },
                    { "ZSCORE", "ZSCORE ssestkey" },
                    { "APPEND", "APPEND ssetkey" },
                    { "BITCOUNT", "BITCOUNT ssetkey" },
                    { "BITOP", "BITOP" },
                    { "BITPOS", "BITPOS ssetkey1" },
                    { "INCRBYFLOAT", "INCRBYFLOAT key" },
                    { "GET", "GET key" },
                    { "GETBIT", "GETBIT key" },
                    { "GETRANGE", "GETRANGE key" },
                    { "GETSET", "GETSET key" },
                    { "INCR", "INCR key" },
                    { "STRLEN", "STRLEN key" },
                    { "SET", "SET key" },
                    { "SETBIT", "SETBIT key" },
                    { "SETRANGE", "SETRANGE key" },
                });

                var spanLookup = new Dictionary<Tuple<string, string>, MockTracerAgent.Span>();
                foreach (var span in spans)
                {
                    spanLookup[new Tuple<string, string>(span.Resource, span.Tags.Get<string>("redis.raw_command"))] = span;
                }

                foreach (var e in expected)
                {
                    Assert.True(spanLookup.ContainsKey(e), $"no span found for `{e.Item1}`, `{e.Item2}`");
                }
            }
        }
    }
}
