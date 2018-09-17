using System;
using StackExchange.Redis;

namespace Samples.RedisCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var redis = ConnectionMultiplexer.Connect("localhost");
            for (var i = 0; i < 100; i++)
            {
                redis.GetDatabase().StringSet($"KEY-{i}", $"VALUE {i}");
                var value = redis.GetDatabase().StringGetAsync($"KEY-{i}").Result;
                Console.WriteLine(value.ToString());
            }
        }
    }
}
