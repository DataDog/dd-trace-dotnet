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

                var batchPrefix = $"{prefix}StackExchange.Redis.Batch.";
                expected.AddRange(new TupleList<string, string>
                {
                    { "DEBUG", $"DEBUG {batchPrefix}DebugObjectAsync" },
                    { "DDCUSTOM", $"DDCUSTOM" },
                    { "GEOADD", $"GEOADD {batchPrefix}GeoAddAsync" },
                    { "GEODIST", $"GEODIST {batchPrefix}GeoDistanceAsync" },
                    { "GEOHASH", $"GEOHASH {batchPrefix}GeoHashAsync" },
                    { "GEOPOS", $"GEOPOS {batchPrefix}GeoPositionAsync" },
                    { "GEORADIUSBYMEMBER", $"GEORADIUSBYMEMBER {batchPrefix}GeoRadiusAsync" },
                    { "ZREM", $"ZREM {batchPrefix}GeoRemoveAsync" },
                    { "HINCRBYFLOAT", $"HINCRBYFLOAT {batchPrefix}HashDecrementAsync" },
                    { "HDEL", $"HDEL {batchPrefix}HashDeleteAsync" },
                    { "HEXISTS", $"HEXISTS {batchPrefix}HashExistsAsync" },
                    { "HGETALL", $"HGETALL {batchPrefix}HashGetAllAsync" },
                    { "HINCRBYFLOAT", $"HINCRBYFLOAT {batchPrefix}HashIncrementAsync" },
                    { "HKEYS", $"HKEYS {batchPrefix}HashKeysAsync" },
                    { "HLEN", $"HLEN {batchPrefix}HashLengthAsync" },
                    { "HMSET", $"HMSET {batchPrefix}HashSetAsync" },
                    { "HVALS", $"HVALS {batchPrefix}HashValuesAsync" },
                    { "PFADD", $"PFADD {batchPrefix}HyperLogLogAddAsync" },
                    { "PFCOUNT", $"PFCOUNT {batchPrefix}HyperLogLogLengthAsync" },
                    { "PFMERGE", $"PFMERGE {batchPrefix}HyperLogLogMergeAsync" },
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

                var dbPrefix = $"{prefix}StackExchange.Redis.Database.";
                expected.AddRange(new TupleList<string, string>
                {
                    { "DEBUG", $"DEBUG {dbPrefix}DebugObject" },
                    { "DDCUSTOM", $"DDCUSTOM" },
                    { "GEOADD", $"GEOADD {dbPrefix}Geo" },
                    { "GEODIST", $"GEODIST {dbPrefix}Geo" },
                    { "GEOHASH", $"GEOHASH {dbPrefix}Geo" },
                    { "GEOPOS", $"GEOPOS {dbPrefix}Geo" },
                    { "GEORADIUSBYMEMBER", $"GEORADIUSBYMEMBER {dbPrefix}Geo" },
                    { "ZREM", $"ZREM {dbPrefix}Geo" },
                    { "HINCRBYFLOAT", $"HINCRBYFLOAT {dbPrefix}Hash" },
                    { "HDEL", $"HDEL {dbPrefix}Hash" },
                    { "HEXISTS", $"HEXISTS {dbPrefix}Hash" },
                    { "HGET", $"HGET {dbPrefix}Hash" },
                    { "HGETALL", $"HGETALL {dbPrefix}Hash" },
                    { "HINCRBY", $"HINCRBY {dbPrefix}Hash" },
                    { "HKEYS", $"HKEYS {dbPrefix}Hash" },
                    { "HLEN", $"HLEN {dbPrefix}Hash" },
                    // { "HSCAN", $"HSCAN {dbPrefix}Hash" },
                    { "HMSET", $"HMSET {dbPrefix}Hash" },
                    { "HVALS", $"HVALS {dbPrefix}Hash" },
                    { "PFADD", $"PFADD {dbPrefix}HyperLogLog" },
                    { "PFCOUNT", $"PFCOUNT {dbPrefix}HyperLogLog" },
                    { "PFMERGE", $"PFMERGE {dbPrefix}HyperLogLog2" },
                    { "DEL", $"DEL {dbPrefix}Key" },
                    { "DUMP", $"DUMP {dbPrefix}Key" },
                    { "EXISTS", $"EXISTS {dbPrefix}Key" },
                    { "PEXPIREAT", $"PEXPIREAT {dbPrefix}Key" },
                    { "MIGRATE", $"MIGRATE {dbPrefix}Key" },
                    { "MOVE", $"MOVE {dbPrefix}Key" },
                    { "PERSIST", $"PERSIST {dbPrefix}Key" },
                    { "RANDOMKEY", $"RANDOMKEY" },
                    { "RENAME", $"RENAME {dbPrefix}Key" },
                    { "RESTORE", $"RESTORE {dbPrefix}Key" },
                    { "PTTL", $"PTTL {dbPrefix}Key" },
                    { "TYPE", $"TYPE {dbPrefix}Key" },
                    { "LINDEX", $"LINDEX {dbPrefix}List" },
                    { "LINSERT", $"LINSERT {dbPrefix}List" },
                    { "LINSERT", $"LINSERT {dbPrefix}List" },
                    { "LPOP", $"LPOP {dbPrefix}List" },
                    { "LPUSH", $"LPUSH {dbPrefix}List" },
                    { "LLEN", $"LLEN {dbPrefix}List" },
                    { "LRANGE", $"LRANGE {dbPrefix}List" },
                    { "LREM", $"LREM {dbPrefix}List" },
                    { "RPOP", $"RPOP {dbPrefix}List" },
                    { "RPOPLPUSH", $"RPOPLPUSH {dbPrefix}List" },
                    { "RPUSH", $"RPUSH {dbPrefix}List" },
                    { "LSET", $"LSET {dbPrefix}List" },
                    { "LTRIM", $"LTRIM {dbPrefix}List" },
                    { "GET", $"GET {dbPrefix}Lock" },
                    { "SET", $"SET {dbPrefix}Lock" },
                    { "PING", $"PING" },
                    { "PUBLISH", $"PUBLISH value" },
                    { "SADD", $"SADD {dbPrefix}Set" },
                    { "SUNION", $"SUNION {dbPrefix}Set" },
                    { "SUNIONSTORE", $"SUNIONSTORE {dbPrefix}Set" },
                    { "SISMEMBER", $"SISMEMBER {dbPrefix}Set" },
                    { "SCARD", $"SCARD {dbPrefix}Set" },
                    { "SMEMBERS", $"SMEMBERS {dbPrefix}Set" },
                    { "SMOVE", $"SMOVE {dbPrefix}Set" },
                    { "SPOP", $"SPOP {dbPrefix}Set" },
                    { "SRANDMEMBER", $"SRANDMEMBER {dbPrefix}Set" },
                    { "SRANDMEMBER", $"SRANDMEMBER {dbPrefix}Set" },
                    { "SREM", $"SREM {dbPrefix}Set" },
                    { "EXEC", $"EXEC" },
                    { "SORT", $"SORT {dbPrefix}Key" },
                    { "SORT", $"SORT {dbPrefix}Key" },
                    { "ZADD", $"ZADD {dbPrefix}SortedSet" },
                    { "ZUNIONSTORE", $"ZUNIONSTORE {dbPrefix}SortedSet2" },
                    { "ZINCRBY", $"ZINCRBY {dbPrefix}SortedSet" },
                    { "ZINCRBY", $"ZINCRBY {dbPrefix}SortedSet" },
                    { "ZCARD", $"ZCARD {dbPrefix}SortedSet" },
                    { "ZLEXCOUNT", $"ZLEXCOUNT {dbPrefix}SortedSet" },
                    { "ZRANGE", $"ZRANGE {dbPrefix}SortedSet" },
                    { "ZRANGE", $"ZRANGE {dbPrefix}SortedSet" },
                    { "ZRANGEBYSCORE", $"ZRANGEBYSCORE {dbPrefix}SortedSet" },
                    { "ZRANGEBYSCORE", $"ZRANGEBYSCORE {dbPrefix}SortedSet" },
                    { "ZRANGEBYLEX", $"ZRANGEBYLEX {dbPrefix}SortedSet" },
                    { "ZRANK", $"ZRANK {dbPrefix}SortedSet" },
                    { "ZREM", $"ZREM {dbPrefix}SortedSet" },
                    { "ZREMRANGEBYRANK", $"ZREMRANGEBYRANK {dbPrefix}SortedSet" },
                    { "ZREMRANGEBYSCORE", $"ZREMRANGEBYSCORE {dbPrefix}SortedSet"  },
                    { "ZREMRANGEBYLEX", $"ZREMRANGEBYLEX {dbPrefix}SortedSet" },
                    { "ZSCORE", $"ZSCORE {dbPrefix}SortedSet" },
                    { "APPEND", $"APPEND {dbPrefix}Key" },
                    { "BITCOUNT", $"BITCOUNT {dbPrefix}Key" },
                    { "BITOP", $"BITOP" },
                    { "BITPOS", $"BITPOS {dbPrefix}Key" },
                    { "INCRBYFLOAT", $"INCRBYFLOAT {dbPrefix}Key" },
                    { "GET", $"GET {dbPrefix}Key" },
                    { "GETBIT", $"GETBIT {dbPrefix}Key" },
                    { "GETRANGE", $"GETRANGE {dbPrefix}Key" },
                    { "GETSET", $"GETSET {dbPrefix}Key" },
                    { "PTTL+GET", $"PTTL+GET {dbPrefix}Key" },
                    { "STRLEN", $"STRLEN {dbPrefix}Key" },
                    { "SET", $"SET {dbPrefix}Key" },
                    { "SETBIT", $"SETBIT {dbPrefix}Key" },
                    { "SETRANGE", $"SETRANGE {dbPrefix}Key" },
                });

                var spans = agent.WaitForSpans(expected.Count).Where(s => s.Type == "redis").OrderBy(s => s.Start).ToList();

                foreach (var span in spans)
                {
                    Assert.Equal(Redis.OperationName, span.Name);
                    Assert.Equal($"Samples.RedisCore-{Redis.ServiceName}", span.Service);
                    Assert.Equal(SpanTypes.Redis, span.Type);
                    Assert.Equal("localhost", span.Tags.Get<string>("out.host"));
                    Assert.Equal("6379", span.Tags.Get<string>("out.port"));
                }

                var spanLookup = new Dictionary<Tuple<string, string>, int>();
                foreach (var span in spans)
                {
                    var key = new Tuple<string, string>(span.Resource, span.Tags.Get<string>("redis.raw_command"));
                    if (spanLookup.ContainsKey(key))
                    {
                        spanLookup[key]++;
                    }
                    else
                    {
                        spanLookup[key] = 1;
                    }
                }

                var missing = new List<Tuple<string, string>>();

                foreach (var e in expected)
                {
                    var found = spanLookup.ContainsKey(e);
                    if (found)
                    {
                        if (--spanLookup[e] <= 0)
                        {
                            spanLookup.Remove(e);
                        }
                    }
                    else
                    {
                        missing.Add(e);
                    }
                }

                foreach (var e in missing)
                {
                    Assert.True(false, $"no span found for `{e.Item1}`, `{e.Item2}`, remaining spans: `{string.Join(", ", spanLookup.Select(kvp => $"{kvp.Key.Item1}, {kvp.Key.Item2}").ToArray())}`");
                }
            }
        }
    }
}
