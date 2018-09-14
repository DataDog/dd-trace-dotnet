using System;
using StackExchange.Redis;

namespace Samples.RedisCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var redis = ConnectionMultiplexer.Connect("localhost");
            redis.GetDatabase().StringSet("KEY", "VALUE");
            var value = redis.GetDatabase().StringGetAsync("KEY").Result;
            Console.WriteLine(value.ToString());
        }
    }
}
