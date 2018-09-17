using System;
using ServiceStack.Redis;
using StackExchange.Redis;

namespace Samples.RedisCore
{
    class Program
    {
        static void Main(string[] args)
        {
            //using (var redis = ConnectionMultiplexer.Connect("localhost"))
            //{
            //    for (var i = 0; i < 100; i++)
            //    {
            //        redis.GetDatabase().StringSet($"StackExchange.Redis.KEY-{i}", $"VALUE {i}");
            //        var value = redis.GetDatabase().StringGetAsync($"StackExchange.Redis.KEY-{i}").Result;
            //        Console.WriteLine(value.ToString());
            //    }
            //}
            using (var redisManager = new PooledRedisClientManager())
            using (var redis = redisManager.GetClient())
            {
                for (var i = 0; i < 100; i++)
                {
                    redis.Set($"ServiceStack.Redis.KEY-{i}", $"VALUE {i}");
                    var value = redis.Get<string>($"ServiceStack.Redis.KEY-{i}");
                    Console.WriteLine(value);
                }
            }
        }
    }
}
