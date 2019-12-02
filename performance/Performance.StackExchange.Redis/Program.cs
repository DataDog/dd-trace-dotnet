using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Performance.StackExchange.Redis
{
    class Program
    {
        private static string ReallyBigString = "";
        private static ConnectionMultiplexer Multiplexer = null;

        static async Task Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 0)
            {
                prefix = args[0];
            }

            await RunStackExchange(prefix);
        }

        private static ConnectionMultiplexer GetMultiplexer()
        {
            if (Multiplexer == null)
            {
                Multiplexer = ConnectionMultiplexer.Connect(Host() + ",allowAdmin=true");
            }

            return Multiplexer;
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST") ?? "localhost:6389";
        }

        private static async Task RunStackExchange(string prefix)
        {
            prefix += "StackExchange.Redis.";

            Console.WriteLine($"Testing StackExchange.Redis {prefix}");

            var environmentVariables = Environment.GetEnvironmentVariables();
            var corEnabledKey = "COR_ENABLE_PROFILING";
            var coreclrEnabledKey = "CORECLR_ENABLE_PROFILING";

            var threadCount = 20;
            var iterationsPerThread = 20_000;
            var expectedIterations = threadCount * iterationsPerThread;
            var performance = new PerformanceBenchmark()
            {
                OperationCount = expectedIterations,
                OperationName = "Redis Eval Set String"
            };

            if (performance.IsCoreClr() && (string)environmentVariables[coreclrEnabledKey] == "1")
            {
                performance.ProfilerEnabled = true;
            }
            else if ((string)environmentVariables[corEnabledKey] == "1")
            {
                performance.ProfilerEnabled = true;
            }

            // Ensure the string is of a reasonable size
            for (var i = 0; i < 10_000; i++)
            {
                ReallyBigString += "-";
            }

            var concurrencyHelper = new ConcurrencyHelper();
            concurrencyHelper.RegisterLevel(DoEvalSetOnLargeString, iterations: iterationsPerThread, friendlyName: performance.OperationName, numberOfRegisters: threadCount);

            try
            {
                var firstCallTimer = new Stopwatch();

                firstCallTimer.Start();
                DoEvalSetOnLargeString();
                firstCallTimer.Stop();
                performance.FirstCallMilliseconds = firstCallTimer.ElapsedMilliseconds;

                await concurrencyHelper.Start();

                performance.TestStart = concurrencyHelper.Started;
                performance.TestEnd = concurrencyHelper.Finished;
                performance.TotalMilliseconds = concurrencyHelper.GetTotalRuntime();
                performance.AverageCallMilliseconds = concurrencyHelper.GetAverageActionRuntime();
            }
            finally
            {
                Multiplexer?.Dispose();
            }

            performance.ExceptionCounts = concurrencyHelper.GetExceptionSummary();

            performance.Save("Performance.StackExchange.Redis");
        }

        private static void DoEvalSetOnLargeString()
        {
            var redis = GetMultiplexer();
            var db = redis.GetDatabase();
            const string script = "redis.call('set', @key, @value)";
            var uniqueKeyText = Guid.NewGuid().ToString();
            var prepared = LuaScript.Prepare(script);
            db.ScriptEvaluate(prepared, new { key = (RedisKey)uniqueKeyText, value = ReallyBigString });
        }
    }
}
