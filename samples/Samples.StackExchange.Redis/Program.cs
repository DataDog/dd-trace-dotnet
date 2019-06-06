using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Samples.StackExchangeRedis
{
    class Program
    {
        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 0)
            {
                prefix = args[0];
            }

            RunStackExchange(prefix);
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
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
                    { "INCR", () => db.StringIncrement($"{prefix}INCR") },
                    { "INCR", () => db.StringIncrement($"{prefix}INCR", 1.25) },
                    { "GET", () => db.StringGet($"{prefix}INCR") },

#if (STACKEXCHANGEREDIS_1_2_2 && !DEFAULT_SAMPLES)
                    { "DDCUSTOM", () => db.Execute("DDCUSTOM", "COMMAND") },
                    { "ECHO", () => db.Execute("ECHO", "Hello World") },
                    { "SLOWLOG", () => db.Execute("SLOWLOG", "GET") },
                    { "TIME", () => db.Execute("TIME") },
#endif
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
                    redis.GetServer(Host()).FlushDatabase(1);
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

#if (STACKEXCHANGEREDIS_1_2_2 && !DEFAULT_SAMPLES)
                { "Execute", () => db.Execute("DDCUSTOM", "COMMAND") },
#endif

#if (STACKEXCHANGEREDIS_1_2_0 && !DEFAULT_SAMPLES)
                { "GeoAdd", () => db.GeoAdd($"{prefix}Geo", new GeoEntry(1.5, 2.5, "member")) },
                { "GeoDistance", () => db.GeoDistance($"{prefix}Geo", "member1", "member2") },
                { "GeoHash", () => db.GeoHash($"{prefix}Geo", "member") },
                { "GeoPosition", () => db.GeoPosition($"{prefix}Geo", "member") },
                { "GeoRadius", () => db.GeoRadius($"{prefix}Geo", "member", 2.3) },
                { "GeoRemove", () => db.GeoRemove($"{prefix}Geo", "member") },
#endif

                { "HashDecrement", () => db.HashDecrement($"{prefix}Hash", "hashfield", 4.5) },
                { "HashDelete", () => db.HashDelete($"{prefix}Hash", "hashfield") },
                { "HashExists", () => db.HashExists($"{prefix}Hash", "hashfield") },
                { "HashGet", () => db.HashGet($"{prefix}Hash", "hashfield") },
                { "HashGetAll", () => db.HashGetAll($"{prefix}Hash") },
                { "HashIncrement", () => db.HashIncrement($"{prefix}Hash", "hashfield") },
                { "HashKeys", () => db.HashKeys($"{prefix}Hash") },
                { "HashLength", () => db.HashLength($"{prefix}Hash") },
#if (STACKEXCHANGEREDIS_1_0_228 && !DEFAULT_SAMPLES)
                { "HashScan", () => db.HashScan($"{prefix}Hash", "*", 5, CommandFlags.None) },
#endif
                { "HashSet", () => { db.HashSet($"{prefix}Hash", ApiSafeCreateHashSetEntryList(new KeyValuePair<RedisValue, RedisValue>[] { new KeyValuePair<RedisValue, RedisValue>("hashfield", "hashvalue") })); return null; } },
                { "HashValues", () => db.HashValues($"{prefix}Hash") },

#if (STACKEXCHANGEREDIS_1_0_242 && !DEFAULT_SAMPLES)
                { "HyperLogLogAdd", () => db.HyperLogLogAdd($"{prefix}HyperLogLog", "value") },
                { "HyperLogLogLength", () => db.HyperLogLogLength($"{prefix}HyperLogLog") },
                { "HyperLogLogMerge", () => { db.HyperLogLogMerge($"{prefix}HyperLogLog2", new RedisKey[] { $"{prefix}HyperLogLog" }); return null; } },
#endif

                { "KeyDelete", () => db.KeyDelete($"{prefix}Key") },
                { "KeyDump", () => db.KeyDump($"{prefix}Key") },
                { "KeyExists", () => db.KeyExists($"{prefix}Key") },
                { "KeyExpire", () => db.KeyExpire($"{prefix}Key", DateTime.Now) },
#if (STACKEXCHANGEREDIS_1_0_297 && !DEFAULT_SAMPLES)
                { "KeyMigrate", () => { db.KeyMigrate($"{prefix}Key", db.IdentifyEndpoint());  return null; } },
#endif
                { "KeyMove", () =>  db.KeyMove($"{prefix}Key", 1) },
                { "KeyPersist", () => db.KeyPersist($"{prefix}Key") },
#if (STACKEXCHANGEREDIS_1_0_219 && !DEFAULT_SAMPLES)
                { "KeyRandom", () => db.KeyRandom() },
#endif
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
#if (STACKEXCHANGEREDIS_1_0_245 && !DEFAULT_SAMPLES)
                { "Publish", () => db.Publish(ApiSafeCreateRedisChannel("value"), "message") },
#endif
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

#if (STACKEXCHANGEREDIS_1_0_206 && !DEFAULT_SAMPLES)
                { "Sort", () => db.Sort($"{prefix}Key") },
                { "SortAndStore", () => db.SortAndStore($"{prefix}Key2", $"{prefix}Key") },
                { "SortedSetCombineAndStore", () => db.SortedSetCombineAndStore(SetOperation.Union, $"{prefix}SortedSet2", $"{prefix}SortedSet", $"{prefix}SortedSet2") },
#endif

                { "SortedSetAdd", () => db.SortedSetAdd($"{prefix}SortedSet", ApiSafeCreateSortedSetEntryList(new KeyValuePair<RedisValue, double>[] { new KeyValuePair<RedisValue, double>("element", 1) })) },
                { "SortedSetDecrement", () => db.SortedSetDecrement($"{prefix}SortedSet", "element", 0.5) },
                { "SortedSetIncrement", () => db.SortedSetIncrement($"{prefix}SortedSet", "element", 0.5) },
                { "SortedSetLength", () => db.SortedSetLength($"{prefix}SortedSet") },
                { "SortedSetRangeByRank", () => db.SortedSetRangeByRank($"{prefix}SortedSet") },
                { "SortedSetRangeByRankWithScores", () => db.SortedSetRangeByRankWithScores($"{prefix}SortedSet") },
                { "SortedSetRangeByScore", () => db.SortedSetRangeByScore($"{prefix}SortedSet") },
                { "SortedSetRangeByScoreWithScores", () => db.SortedSetRangeByScoreWithScores($"{prefix}SortedSet") },
                { "SortedSetRank", () => db.SortedSetRank($"{prefix}SortedSet", "element") },
                { "SortedSetRemove", () => db.SortedSetRemove($"{prefix}SortedSet", "element") },
                { "SortedSetRemoveRangeByRank", () => db.SortedSetRemoveRangeByRank($"{prefix}SortedSet", 0, 1) },
                { "SortedSetRemoveRangeByScore", () => db.SortedSetRemoveRangeByScore($"{prefix}SortedSet", 1, 2) },
#if (STACKEXCHANGEREDIS_1_0_228 && !DEFAULT_SAMPLES)
                { "SortedSetScan", () => db.SortedSetScan($"{prefix}SortedSet", "*", 5) },
#endif
                { "SortedSetScore", () => db.SortedSetScore($"{prefix}SortedSet", "element") },

#if (STACKEXCHANGEREDIS_1_0_273 && !DEFAULT_SAMPLES)
                { "SortedSetLengthByValue", () => db.SortedSetLengthByValue($"{prefix}SortedSet", "value1", "value2") },
                { "SortedSetRangeByValue", () => db.SortedSetRangeByValue($"{prefix}SortedSet") },
                { "SortedSetRemoveRangeByValue", () => db.SortedSetRemoveRangeByValue($"{prefix}SortedSet", 1, 2) },
#endif

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

#if (STACKEXCHANGEREDIS_1_2_2 && !DEFAULT_SAMPLES)
                { "ExecuteAsync", () => db.ExecuteAsync("DDCUSTOM", "COMMAND") },
#endif

#if (STACKEXCHANGEREDIS_1_2_0 && !DEFAULT_SAMPLES)
                { "GeoAddAsync", () => db.GeoAddAsync($"{prefix}GeoAddAsync", new GeoEntry(1.5, 2.5, "member")) },
                { "GeoDistanceAsync",  () => db.GeoDistanceAsync($"{prefix}GeoDistanceAsync", "member1", "member2") },
                { "GeoHashAsync", () => db.GeoHashAsync($"{prefix}GeoHashAsync", "member") },
                { "GeoPositionAsync", () => db.GeoPositionAsync($"{prefix}GeoPositionAsync", "member") },
                { "GeoRadiusAsync", () => db.GeoRadiusAsync($"{prefix}GeoRadiusAsync", "member", 2.3) },
                { "GeoRemoveAsync", () => db.GeoRemoveAsync($"{prefix}GeoRemoveAsync", "member") },
#endif

                { "HashDecrementAsync", () => db.HashDecrementAsync($"{prefix}HashDecrementAsync", "hashfield", 4.5) },
                { "HashDeleteAsync", () => db.HashDeleteAsync($"{prefix}HashDeleteAsync", "hashfield") },
                { "HashExistsAsync", () => db.HashExistsAsync($"{prefix}HashExistsAsync", "hashfield") },
                { "HashGetAllAsync", () => db.HashGetAllAsync($"{prefix}HashGetAllAsync") },
                { "HashIncrementAsync", () => db.HashIncrementAsync($"{prefix}HashIncrementAsync", "hashfield", 1.5) },
                { "HashKeysAsync", () => db.HashKeysAsync($"{prefix}HashKeysAsync") },
                { "HashLengthAsync", () => db.HashLengthAsync($"{prefix}HashLengthAsync") },
                { "HashSetAsync", () => db.HashSetAsync($"{prefix}HashSetAsync", ApiSafeCreateHashSetEntryList(new KeyValuePair<RedisValue, RedisValue>[] { new KeyValuePair<RedisValue, RedisValue>("x", "y") })) },
                { "HashValuesAsync", () => db.HashValuesAsync($"{prefix}HashValuesAsync") },

#if (STACKEXCHANGEREDIS_1_0_242 && !DEFAULT_SAMPLES)
                { "HyperLogLogAddAsync", () => db.HyperLogLogAddAsync($"{prefix}HyperLogLogAddAsync", "value") },
                { "HyperLogLogLengthAsync", () => db.HyperLogLogLengthAsync($"{prefix}HyperLogLogLengthAsync") },
                { "HyperLogLogMergeAsync", () => db.HyperLogLogMergeAsync($"{prefix}HyperLogLogMergeAsync", new RedisKey[] { "key1", "key2" }) },
#endif

                { "IdentifyEndpointAsync", () => db.IdentifyEndpointAsync() },

                { "KeyDeleteAsync", () => db.KeyDeleteAsync("key") },
                { "KeyDumpAsync", () => db.KeyDumpAsync("key") },
                { "KeyExistsAsync", () => db.KeyExistsAsync("key") },
                { "KeyExpireAsync", () => db.KeyExpireAsync("key", DateTime.Now) },
                // () => db.KeyMigrateAsync("key", ???)
                { "KeyMoveAsync", () => db.KeyMoveAsync("key", 1) },
                { "KeyPersistAsync", () => db.KeyPersistAsync("key") },
#if (STACKEXCHANGEREDIS_1_0_219 && !DEFAULT_SAMPLES)
                { "KeyRandomAsync", () => db.KeyRandomAsync() },
#endif
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

#if (STACKEXCHANGEREDIS_1_0_245 && !DEFAULT_SAMPLES)
                { "PublishAsync", () => db.PublishAsync(ApiSafeCreateRedisChannel("channel"), "somemessage") },
#endif
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

#if (STACKEXCHANGEREDIS_1_0_206 && !DEFAULT_SAMPLES)
                { "SortAndStoreAsync", () => db.SortAndStoreAsync("setkey2", "setkey") },
                { "SortAsync", () => db.SortAsync("setkey") },
                { "SortedSetCombineAndStoreAsync", () => db.SortedSetCombineAndStoreAsync(SetOperation.Union, "ssetkey1", "ssetkey2", "ssetkey3") },
#endif

                { "SortedSetAddAsync", () => db.SortedSetAddAsync("ssetkey", ApiSafeCreateSortedSetEntryList(new KeyValuePair<RedisValue, double>[] { new KeyValuePair<RedisValue, double>("value1", 1.5), new KeyValuePair<RedisValue, double>("value2", 2.5) })) },
                { "SortedSetDecrementAsync", () => db.SortedSetDecrementAsync("ssetkey", "value1", 1) },
                { "SortedSetIncrementAsync", () => db.SortedSetIncrementAsync("ssetkey", "value2", 1) },
                { "SortedSetLengthAsync", () => db.SortedSetLengthAsync("ssetkey") },
                { "SortedSetRangeByRankAsync", () => db.SortedSetRangeByRankAsync("ssetkey") },
                { "SortedSetRangeByRankWithScoresAsync", () => db.SortedSetRangeByRankWithScoresAsync("ssetkey") },
                { "SortedSetRangeByScoreAsync", () => db.SortedSetRangeByScoreAsync("ssetkey") },
                { "SortedSetRangeByScoreWithScoresAsync", () => db.SortedSetRangeByScoreWithScoresAsync("ssetkey") },
                { "SortedSetRankAsync", () => db.SortedSetRankAsync("ssetkey", "value1") },
                { "SortedSetRemoveAsync", () => db.SortedSetRemoveAsync("ssetkey", "value1") },
                { "SortedSetRemoveRangeByRankAsync", () => db.SortedSetRemoveRangeByRankAsync("ssetkey", 0, 1) },
                { "SortedSetRemoveRangeByScoreAsync", () => db.SortedSetRemoveRangeByScoreAsync("ssetkey", 0, 1) },
                { "SortedSetScoreAsync", () => db.SortedSetScoreAsync("ssestkey", "value1") },

#if (STACKEXCHANGEREDIS_1_0_273 && !DEFAULT_SAMPLES)
                { "SortedSetLengthByValueAsync", () => db.SortedSetLengthByValueAsync("ssetkey", "value1", "value2") },
                { "SortedSetRangeByValueAsync", () => db.SortedSetRangeByValueAsync("ssetkey") },
                { "SortedSetRemoveRangeByValueAsync", () => db.SortedSetRemoveRangeByValueAsync("ssetkey", "value1", "value2") },
#endif

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


        private static RedisChannel ApiSafeCreateRedisChannel(string value)
        {
#if STACKEXCHANGEREDIS_1_0_371
            return new RedisChannel(value, RedisChannel.PatternMode.Auto);
#else
            return value;
#endif
        }

        private static dynamic ApiSafeCreateHashSetEntryList(KeyValuePair<RedisValue, RedisValue>[] entries) // KeyValuePair<RedisValue, RedisValue>[] hashFields
        {
#if STACKEXCHANGEREDIS_1_0_231
            return entries.Select(tuple => new HashEntry(tuple.Key, tuple.Value)).ToArray();
#else
            return entries;
#endif
        }

        private static dynamic ApiSafeCreateSortedSetEntryList(KeyValuePair<RedisValue, double>[] entries)
        {
#if STACKEXCHANGEREDIS_1_0_231
            return entries.Select(tuple => new SortedSetEntry(tuple.Key, tuple.Value)).ToArray();
#else
            return entries;
#endif
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
