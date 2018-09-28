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

        private static void RunServiceStack(string prefix)
        {
            prefix += "ServiceStack.Redis.";

            Console.WriteLine($"Testing ServiceStack.Redis: {prefix}");
            using (var redisManager = new PooledRedisClientManager())
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
            using (var redis = ConnectionMultiplexer.Connect("localhost,allowAdmin=true"))
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
                catch(Exception e)
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
                if (cmd.Item2 == null)
                {
                    continue;
                }

                object result;
                try
                {
                    result = cmd.Item2();
                }
                catch (Exception ex)
                {
                    result = ex.Message;
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
