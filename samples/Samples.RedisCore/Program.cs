using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Redis;
using StackExchange.Redis;

namespace Samples.RedisCore
{
    class Program
    {
        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 1)
            {
                prefix = args[1];
            }

            if (args.Length == 0 || args.Contains("ServiceStack"))
            {
                RunServiceStack(prefix);
            }
            if (args.Length == 0 || args.Contains("StackExchange"))
            {
                RunStackExchange(prefix);
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
        }

        private static void RunServiceStack(string prefix)
        {
            prefix += "ServiceStack.Redis.";

            Console.WriteLine($"Testing ServiceStack.Redis: {prefix}");
            using (var redisManager = new PooledRedisClientManager(Host()))
            using (var redis = (RedisClient)redisManager.GetClient())
            {
                // clear
                redis.Set($"{prefix}INCR", 0);

                RunCommands(new TupleList<string, Func<object>>
                {
                    // test SendExpectCode
                    { "PING", () => redis.Ping() },
                    // test SendExpectComplexResponse
                    { "DDCUSTOM", () => redis.Custom("DDCUSTOM", "COMMAND") },
                    // test SendExpectData
                    { "ECHO", () => redis.Echo("Hello World") },
                    // test SendExpectDeeplyNestedMultiData
                    { "SLOWLOG", () => redis.GetSlowlog(5) },
                    // test SendExpectLong
                    { "INCR", () => redis.Incr($"{prefix}INCR") },
                    // test SendExpectDouble},
                    { "INCR", () => redis.IncrByFloat($"{prefix}INCR", 1.25) },
                    // test SendExpectMultiData
                    { "TIME", () => redis.GetServerTime() },
                    // test SendExpectSuccess
                    { "DB", () => { redis.ChangeDb(0); return ""; } },
                    // test SendWithoutRead
                    // only Shutdown, so we will skip for now
                });
            }
        }

        private static void RunStackExchange(string prefix)
        {
            prefix += "StackExchange.Redis.";

            Console.WriteLine($"Testing StackExchange.Redis {prefix}");
            using (var redis = ConnectionMultiplexer.Connect(Host() + ",allowAdmin=true"))
            {
                var db = redis.GetDatabase(1);

                db.StringSet($"{prefix}INCR", "0");

                RunCommands(new TupleList<string, Func<object>>
                {
                    { "PING", () => db.PingAsync().Result },
                    { "DDCUSTOM", () => db.Execute("DDCUSTOM", "COMMAND") },
                    { "ECHO", () => db.Execute("ECHO", "Hello World") },
                    { "SLOWLOG", () => db.Execute("SLOWLOG", "GET") },
                    { "INCR", () => db.StringIncrement($"{prefix}INCR") },
                    { "INCR", () => db.StringIncrement($"{prefix}INCR", 1.25) },
                    { "GET", () => db.StringGet($"{prefix}INCR") },
                    { "TIME", () => db.Execute("TIME") },
                });

                RunCommands(StackExchangeSyncCommands(prefix + "Database.", db));

                var batch = db.CreateBatch();
                var batchPending = RunStackExchangeAsync(prefix + "Batch.", batch);
                batch.Execute();
                foreach (var item in batchPending)
                {
                    try
                    {
                        Console.WriteLine($"{item.Item1}: {TaskResult(item.Item2)}");
                    }
                    catch (Exception e)
                    {
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                        }
                        Console.WriteLine($"{item.Item1}: {e.Message}");
                    }
                }

                try
                {
                    redis.GetServer("localhost:6379").FlushDatabase(1);
                }
                catch
                {
                }
            }
        }

        private static TupleList<string, Func<object>> StackExchangeSyncCommands(string prefix, IDatabase db)
        {
            return new TupleList<string, Func<object>>()
            {
                { "DebugObject", () => db.DebugObject($"{prefix}DebugObject") },
                { "Execute", () => db.Execute("DDCUSTOM", "COMMAND") },

                { "GeoAdd", () => db.GeoAdd($"{prefix}Geo", new GeoEntry(1.5, 2.5, "member")) },
                { "GeoDistance", () => db.GeoDistance($"{prefix}Geo", "member1", "member2") },
                { "GeoHash", () => db.GeoHash($"{prefix}Geo", "member") },
                { "GeoPosition", () => db.GeoPosition($"{prefix}Geo", "member") },
                { "GeoRadius", () => db.GeoRadius($"{prefix}Geo", "member", 2.3) },
                { "GeoRemove", () => db.GeoRemove($"{prefix}Geo", "member") },

                { "HashDecrement", () => db.HashDecrement($"{prefix}Hash", "hashfield", 4.5) },
                { "HashDelete", () => db.HashDelete($"{prefix}Hash", "hashfield") },
                { "HashExists", () => db.HashExists($"{prefix}Hash", "hashfield") },
                { "HashGet", () => db.HashGet($"{prefix}Hash", "hashfield") },
                { "HashGetAll", () => db.HashGetAll($"{prefix}Hash") },
                { "HashIncrement", () => db.HashIncrement($"{prefix}Hash", "hashfield") },
                { "HashKeys", () => db.HashKeys($"{prefix}Hash") },
                { "HashLength", () => db.HashLength($"{prefix}Hash") },
                { "HashScan", () => db.HashScan($"{prefix}Hash", "*", 5, CommandFlags.None) },
                { "HashSet", () => { db.HashSet($"{prefix}Hash", new HashEntry[] { new HashEntry("hashfield", "hashvalue") }); return null; } },
                { "HashValues", () => db.HashValues($"{prefix}Hash") },

                { "HyperLogLogAdd", () => db.HyperLogLogAdd($"{prefix}HyperLogLog", "value") },
                { "HyperLogLogLength", () => db.HyperLogLogLength($"{prefix}HyperLogLog") },
                { "HyperLogLogMerge", () => { db.HyperLogLogMerge($"{prefix}HyperLogLog2", new RedisKey[] { $"{prefix}HyperLogLog" }); return null; } },

                { "KeyDelete", () => db.KeyDelete($"{prefix}Key") },
                { "KeyDump", () => db.KeyDump($"{prefix}Key") },
                { "KeyExists", () => db.KeyExists($"{prefix}Key") },
                { "KeyExpire", () => db.KeyExpire($"{prefix}Key", DateTime.Now) },
                { "KeyMigrate", () => { db.KeyMigrate($"{prefix}Key", db.IdentifyEndpoint());  return null; } },
                { "KeyMove", () =>  db.KeyMove($"{prefix}Key", 1) },
                { "KeyPersist", () => db.KeyPersist($"{prefix}Key") },
                { "KeyRandom", () => db.KeyRandom() },
                { "KeyRename", () => db.KeyRename($"{prefix}Key", $"{prefix}Key2") },
                { "KeyRestore", () => { db.KeyRestore($"{prefix}Key", new byte[] { 1,2,3,4 }); return null; } },
                { "KeyTimeToLive", () => db.KeyTimeToLive($"{prefix}Key") },
                { "KeyType", () => db.KeyType($"{prefix}Key") },

                { "ListGetByIndex", () => db.ListGetByIndex($"{prefix}List", 0) },
                { "ListInsertAfter", () => db.ListInsertAfter($"{prefix}List", "value1", "value2") },
                { "ListInsertBefore", () => db.ListInsertBefore($"{prefix}List", "value1", "value2") },
                { "ListLeftPop", () => db.ListLeftPop($"{prefix}List") },
                { "ListLeftPush", () => db.ListLeftPush($"{prefix}List", "value3") },
                { "ListLength", () => db.ListLength($"{prefix}List") },
                { "ListRange", () => db.ListRange($"{prefix}List") },
                { "ListRemove", () => db.ListRemove($"{prefix}List", "value1") },
                { "ListRightPop", () => db.ListRightPop($"{prefix}List") },
                { "ListRightPopLeftPush", () => db.ListRightPopLeftPush($"{prefix}List", $"{prefix}List2") },
                { "ListRightPush", () => db.ListRightPush($"{prefix}List", new RedisValue[] { "value1", "value2" }) },
                { "ListSetByIndex", () => { db.ListSetByIndex($"{prefix}List", 0, "value3"); return null; } },
                { "ListTrim", () => { db.ListTrim($"{prefix}List", 0, 1); return null;  } },

                { "LockExtend", () => db.LockExtend($"{prefix}Lock", "value1", new TimeSpan(0, 0, 10)) },
                { "LockQuery", () => db.LockQuery($"{prefix}Lock") },
                { "LockRelease", () => db.LockRelease($"{prefix}Lock", "value1") },
                { "LockTake", () => db.LockTake($"{prefix}Lock", "value1", new TimeSpan(0, 0, 10)) },

                { "Ping", () => db.Ping() },
                { "Publish", () => db.Publish(new RedisChannel("value", RedisChannel.PatternMode.Auto), "message") },
                // { "ScriptEvaluate", () => db.ScriptEvaluate() }

                { "SetAdd", () => db.SetAdd($"{prefix}Set", "value1") },
                { "SetCombine", () => db.SetCombine(SetOperation.Union, new RedisKey[] { $"{prefix}Set" }) },
                { "SetCombineAndStore", () => db.SetCombineAndStore(SetOperation.Union, $"{prefix}Set", new RedisKey[] { $"{prefix}Set" }) },
                { "SetContains", () => db.SetContains($"{prefix}Set", "value1") },
                { "SetLength", () => db.SetLength($"{prefix}Set") },
                { "SetMembers", () => db.SetMembers($"{prefix}Set") },
                { "SetMove", () => db.SetMove($"{prefix}Set", $"{prefix}Set2", "value1") },
                { "SetPop", () => db.SetPop($"{prefix}Set") },
                { "SetRandomMember", () => db.SetRandomMember($"{prefix}Set") },
                { "SetRandomMembers", () => db.SetRandomMembers($"{prefix}Set", 1) },
                { "SetRemove", () => db.SetRemove($"{prefix}Set", "value1") },
                { "SetScan", () => db.SetScan($"{prefix}Set", "*", 5) },

                { "Sort", () => db.Sort($"{prefix}Key") },
                { "SortAndStore", () => db.SortAndStore($"{prefix}Key2", $"{prefix}Key") },

                { "SortedSetAdd", () => db.SortedSetAdd($"{prefix}SortedSet", new SortedSetEntry[] { new SortedSetEntry("element", 1) }) },
                { "SortedSetCombineAndStore", () => db.SortedSetCombineAndStore(SetOperation.Union, $"{prefix}SortedSet2", $"{prefix}SortedSet", $"{prefix}SortedSet2") },
                { "SortedSetDecrement", () => db.SortedSetDecrement($"{prefix}SortedSet", "element", 0.5) },
                { "SortedSetIncrement", () => db.SortedSetIncrement($"{prefix}SortedSet", "element", 0.5) },
                { "SortedSetLength", () => db.SortedSetLength($"{prefix}SortedSet") },
                { "SortedSetLengthByValue", () => db.SortedSetLengthByValue($"{prefix}SortedSet", "value1", "value2") },
                { "SortedSetRangeByRank", () => db.SortedSetRangeByRank($"{prefix}SortedSet") },
                { "SortedSetRangeByRankWithScores", () => db.SortedSetRangeByRankWithScores($"{prefix}SortedSet") },
                { "SortedSetRangeByScore", () => db.SortedSetRangeByScore($"{prefix}SortedSet") },
                { "SortedSetRangeByScoreWithScores", () => db.SortedSetRangeByScoreWithScores($"{prefix}SortedSet") },
                { "SortedSetRangeByValue", () => db.SortedSetRangeByValue($"{prefix}SortedSet") },
                { "SortedSetRank", () => db.SortedSetRank($"{prefix}SortedSet", "element") },
                { "SortedSetRemove", () => db.SortedSetRemove($"{prefix}SortedSet", "element") },
                { "SortedSetRemoveRangeByRank", () => db.SortedSetRemoveRangeByRank($"{prefix}SortedSet", 0, 1) },
                { "SortedSetRemoveRangeByScore", () => db.SortedSetRemoveRangeByScore($"{prefix}SortedSet", 1, 2) },
                { "SortedSetRemoveRangeByValue", () => db.SortedSetRemoveRangeByValue($"{prefix}SortedSet", 1, 2) },
                { "SortedSetScan", () => db.SortedSetScan($"{prefix}SortedSet", "*", 5) },
                { "SortedSetScore", () => db.SortedSetScore($"{prefix}SortedSet", "element") },

                { "StringAppend", () => db.StringAppend($"{prefix}Key", "value") },
                { "StringBitCount", () => db.StringBitCount($"{prefix}Key") },
                { "StringBitOperation", () => db.StringBitOperation(Bitwise.And, $"{prefix}Key", new RedisKey[] {$"{prefix}Key2" }) },
                { "StringBitPosition", () => db.StringBitPosition($"{prefix}Key", true) },
                { "StringDecrement", () => db.StringDecrement($"{prefix}Key", 5.5) },
                { "StringGet", () => db.StringGet($"{prefix}Key") },
                { "StringGetBit", () => db.StringGetBit($"{prefix}Key", 5) },
                { "StringGetRange", () => db.StringGetRange($"{prefix}Key", 0, 3) },
                { "StringGetSet", () => db.StringGetSet($"{prefix}Key", "value") },
                { "StringGetWithExpiry", () => db.StringGetWithExpiry($"{prefix}Key") },
                { "StringIncrement", () => db.StringIncrement($"{prefix}Key", 7.2) },
                { "StringLength", () => db.StringLength($"{prefix}Key") },
                { "StringSet", () => db.StringSet(new KeyValuePair<RedisKey, RedisValue>[] { new KeyValuePair<RedisKey, RedisValue>($"{prefix}Key", "value") }) },
                { "StringSetBit", () => db.StringSetBit($"{prefix}Key", 0, true) },
                { "StringSetRange", () => db.StringSetRange($"{prefix}Key", 17, "value") },
            };
        }

        private static TupleList<string, Task> RunStackExchangeAsync(string prefix, IDatabaseAsync db)
        {
            var tasks = new TupleList<string, Func<Task>>()
            {
                { "DebugObjectAsync", () => db.DebugObjectAsync($"{prefix}DebugObjectAsync") },
                { "ExecuteAsync", () => db.ExecuteAsync("DDCUSTOM", "COMMAND") },
                { "GeoAddAsync", () => db.GeoAddAsync($"{prefix}GeoAddAsync", new GeoEntry(1.5, 2.5, "member")) },
                { "GeoDistanceAsync",  () => db.GeoDistanceAsync($"{prefix}GeoDistanceAsync", "member1", "member2") },
                { "GeoHashAsync", () => db.GeoHashAsync($"{prefix}GeoHashAsync", "member") },
                { "GeoPositionAsync", () => db.GeoPositionAsync($"{prefix}GeoPositionAsync", "member") },
                { "GeoRadiusAsync", () => db.GeoRadiusAsync($"{prefix}GeoRadiusAsync", "member", 2.3) },
                { "GeoRemoveAsync", () => db.GeoRemoveAsync($"{prefix}GeoRemoveAsync", "member") },
                { "HashDecrementAsync", () => db.HashDecrementAsync($"{prefix}HashDecrementAsync", "hashfield", 4.5) },
                { "HashDeleteAsync", () => db.HashDeleteAsync($"{prefix}HashDeleteAsync", "hashfield") },
                { "HashExistsAsync", () => db.HashExistsAsync($"{prefix}HashExistsAsync", "hashfield") },
                { "HashGetAllAsync", () => db.HashGetAllAsync($"{prefix}HashGetAllAsync") },
                { "HashIncrementAsync", () => db.HashIncrementAsync($"{prefix}HashIncrementAsync", "hashfield", 1.5) },
                { "HashKeysAsync", () => db.HashKeysAsync($"{prefix}HashKeysAsync") },
                { "HashLengthAsync", () => db.HashLengthAsync($"{prefix}HashLengthAsync") },
                { "HashSetAsync", () => db.HashSetAsync($"{prefix}HashSetAsync", new HashEntry[] { new HashEntry("x", "y") }) },
                { "HashValuesAsync", () => db.HashValuesAsync($"{prefix}HashValuesAsync") },
                { "HyperLogLogAddAsync", () => db.HyperLogLogAddAsync($"{prefix}HyperLogLogAddAsync", "value") },
                { "HyperLogLogLengthAsync", () => db.HyperLogLogLengthAsync($"{prefix}HyperLogLogLengthAsync") },
                { "HyperLogLogMergeAsync", () => db.HyperLogLogMergeAsync($"{prefix}HyperLogLogMergeAsync", new RedisKey[] { "key1", "key2" }) },
                { "IdentifyEndpointAsync", () => db.IdentifyEndpointAsync() },
                { "KeyDeleteAsync", () => db.KeyDeleteAsync("key") },
                { "KeyDumpAsync", () => db.KeyDumpAsync("key") },
                { "KeyExistsAsync", () => db.KeyExistsAsync("key") },
                { "KeyExpireAsync", () => db.KeyExpireAsync("key", DateTime.Now) },
                // () => db.KeyMigrateAsync("key", ???)
                { "KeyMoveAsync", () => db.KeyMoveAsync("key", 1) },
                { "KeyPersistAsync", () => db.KeyPersistAsync("key") },
                { "KeyRandomAsync", () => db.KeyRandomAsync() },
                { "KeyRenameAsync", () => db.KeyRenameAsync("key1", "key2") },
                { "KeyRestoreAsync", () => db.KeyRestoreAsync("key", new byte[] { 0,1,2,3,4 }) },
                { "KeyTimeToLiveAsync", () => db.KeyTimeToLiveAsync("key") },
                { "KeyTypeAsync", () => db.KeyTypeAsync("key") },
                { "ListGetByIndexAsync", () => db.ListGetByIndexAsync("listkey", 0) },
                { "ListInsertAfterAsync", () => db.ListInsertAfterAsync("listkey", "value1", "value2") },
                { "ListInsertBeforeAsync", () => db.ListInsertBeforeAsync("listkey", "value1", "value2") },
                { "ListLeftPopAsync", () => db.ListLeftPopAsync("listkey") },
                { "ListLeftPushAsync", () => db.ListLeftPushAsync("listkey", new RedisValue[] { "value3", "value4" }) },
                { "ListLengthAsync", () => db.ListLengthAsync("listkey") },
                { "ListRangeAsync", () => db.ListRangeAsync("listkey") },
                { "ListRemoveAsync", () => db.ListRemoveAsync("listkey", "value3") },
                { "ListRightPopAsync", () => db.ListRightPopAsync("listkey") },
                { "ListRightPopLeftPushAsync", () => db.ListRightPopLeftPushAsync("listkey", "listkey2") },
                { "ListRightPushAsync", () => db.ListRightPushAsync("listkey", new RedisValue[] { "value5", "value6" }) },
                { "ListSetByIndexAsync", () => db.ListSetByIndexAsync("listkey", 0, "value7") },
                { "ListTrimAsync", () => db.ListTrimAsync("listkey", 0, 1) },
                { "LockExtendAsync", () => db.LockExtendAsync("listkey", "value7", new TimeSpan(0, 0, 10)) },
                { "LockQueryAsync", () => db.LockQueryAsync("listkey") },
                { "LockReleaseAsync", () => db.LockReleaseAsync("listkey", "value7") },
                { "LockTakeAsync", () => db.LockTakeAsync("listkey", "value8", new TimeSpan(0, 0, 10)) },
                { "PublishAsync", () => db.PublishAsync(new RedisChannel("channel", RedisChannel.PatternMode.Auto), "somemessage") },
                // { "ScriptEvaluateAsync", () => db.ScriptEvaluateAsync(}
                { "SetAddAsync", () => db.SetAddAsync("setkey", "value1") },
                { "SetCombineAndStoreAsync", () => db.SetCombineAndStoreAsync(SetOperation.Union, "setkey", new RedisKey[] { "value2" }) },
                { "SetCombineAsync", () => db.SetCombineAsync(SetOperation.Union, new RedisKey[] { "setkey1", "setkey2"}) },
                { "SetContainsAsync", () => db.SetContainsAsync("setkey", "value1") },
                { "SetLengthAsync", () => db.SetLengthAsync("setkey") },
                { "SetMembersAsync", () => db.SetMembersAsync("setkey") },
                { "SetMoveAsync", () => db.SetMoveAsync("setkey1", "setkey2", "value2") },
                { "SetPopAsync", () => db.SetPopAsync("setkey1") },
                { "SetRandomMemberAsync", () => db.SetRandomMemberAsync("setkey") },
                { "SetRandomMembersAsync", () => db.SetRandomMembersAsync("setkey", 2) },
                { "SetRemoveAsync", () => db.SetRemoveAsync("setkey", "value2") },
                { "SortAndStoreAsync", () => db.SortAndStoreAsync("setkey2", "setkey") },
                { "SortAsync", () => db.SortAsync("setkey") },
                { "SortedSetAddAsync", () => db.SortedSetAddAsync("ssetkey", new SortedSetEntry[] { new SortedSetEntry("value1", 1.5), new SortedSetEntry("value2", 2.5) }) },
                { "SortedSetCombineAndStoreAsync", () => db.SortedSetCombineAndStoreAsync(SetOperation.Union, "ssetkey1", "ssetkey2", "ssetkey3") },
                { "SortedSetDecrementAsync", () => db.SortedSetDecrementAsync("ssetkey", "value1", 1) },
                { "SortedSetIncrementAsync", () => db.SortedSetIncrementAsync("ssetkey", "value2", 1) },
                { "SortedSetLengthAsync", () => db.SortedSetLengthAsync("ssetkey") },
                { "SortedSetLengthByValueAsync", () => db.SortedSetLengthByValueAsync("ssetkey", "value1", "value2") },
                { "SortedSetRangeByRankAsync", () => db.SortedSetRangeByRankAsync("ssetkey") },
                { "SortedSetRangeByRankWithScoresAsync", () => db.SortedSetRangeByRankWithScoresAsync("ssetkey") },
                { "SortedSetRangeByScoreAsync", () => db.SortedSetRangeByScoreAsync("ssetkey") },
                { "SortedSetRangeByScoreWithScoresAsync", () => db.SortedSetRangeByScoreWithScoresAsync("ssetkey") },
                { "SortedSetRangeByValueAsync", () => db.SortedSetRangeByValueAsync("ssetkey") },
                { "SortedSetRankAsync", () => db.SortedSetRankAsync("ssetkey", "value1") },
                { "SortedSetRemoveAsync", () => db.SortedSetRemoveAsync("ssetkey", "value1") },
                { "SortedSetRemoveRangeByRankAsync", () => db.SortedSetRemoveRangeByRankAsync("ssetkey", 0, 1) },
                { "SortedSetRemoveRangeByScoreAsync", () => db.SortedSetRemoveRangeByScoreAsync("ssetkey", 0, 1) },
                { "SortedSetRemoveRangeByValueAsync", () => db.SortedSetRemoveRangeByValueAsync("ssetkey", "value1", "value2") },
                { "SortedSetScoreAsync", () => db.SortedSetScoreAsync("ssestkey", "value1") },
                { "StringAppendAsync", () => db.StringAppendAsync("ssetkey", "value1") },
                { "StringBitCountAsync", () => db.StringBitCountAsync("ssetkey") },
                { "StringBitOperationAsync", () => db.StringBitOperationAsync(Bitwise.And, "ssetkey1", new RedisKey[] { "ssetkey2", "ssetkey3" }) },
                { "StringBitPositionAsync", () => db.StringBitPositionAsync("ssetkey1", true) },
                { "StringDecrementAsync", () => db.StringDecrementAsync("key", 1.45) },
                { "StringGetAsync", () => db.StringGetAsync("key") },
                { "StringGetBitAsync", () => db.StringGetBitAsync("key", 3) },
                { "StringGetRangeAsync", () => db.StringGetRangeAsync("key", 0, 1) },
                { "StringGetSetAsync", () => db.StringGetSetAsync("key", "value") },
                { "StringGetWithExpiryAsync", () => db.StringGetWithExpiryAsync("key") },
                { "StringIncrementAsync", () => db.StringIncrementAsync("key", 1) },
                { "StringLengthAsync", () => db.StringLengthAsync("key") },
                { "StringSetAsync", () => db.StringSetAsync(new KeyValuePair<RedisKey, RedisValue>[] { new KeyValuePair<RedisKey, RedisValue>("key", "value")}) },
                { "StringSetBitAsync", () => db.StringSetBitAsync("key", 0, true) },
                { "StringSetRangeAsync", () => db.StringSetRangeAsync("key", 3, "value") },
            };

            var pending = new TupleList<string, Task>();
            foreach (var item in tasks)
            {
                try
                {
                    pending.Add(item.Item1, item.Item2());
                }
                catch (Exception e)
                {
                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    Console.WriteLine($"{e.Message}");
                }
            }

            return pending;
        }

        private static object TaskResult(Task task)
        {
            var taskType = task.GetType();

            bool isTaskOfT =
                taskType.IsGenericType
                && taskType.GetGenericTypeDefinition() == typeof(Task<>);

            if (isTaskOfT)
            {
                return taskType.GetProperty("Result").GetValue(task);
            }
            else
            {
                task.Wait();
                return null;
            }
        }

        private static void RunCommands(TupleList<string, Func<object>> commands)
        {
            foreach (var cmd in commands)
            {
                var f = cmd.Item2;
                if (f == null)
                {
                    continue;
                }

                object result;
                try
                {
                    result = f();
                }
                catch (Exception e)
                {
                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    result = e.Message;
                }

                if (result is Task task)
                {
                    result = TaskResult(task);
                }

                Console.WriteLine($"{cmd.Item1}: {result}");
            }
        }

        private class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                Add(new Tuple<T1, T2>(item, item2));
            }
        }
    }
}
