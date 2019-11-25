using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Core.Tools;
using Newtonsoft.Json;
using StackExchange.Redis;
using Exception = System.Exception;

namespace Performance.Redis
{
    class Program
    {
        private static readonly string RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();
        private static string ReallyBigString = "";
        private static ConnectionMultiplexer Multiplexer = null;

        static void Main(string[] args)
        {
            string prefix = "";
            if (args.Length > 0)
            {
                prefix = args[0];
            }

            RunStackExchange(prefix);
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

        private static void RunStackExchange(string prefix)
        {
            prefix += "StackExchange.Redis.";

            Console.WriteLine($"Testing StackExchange.Redis {prefix}");

            bool profilerEnabled = false;
            var environmentVariables = Environment.GetEnvironmentVariables();
            var corEnabledKey = "COR_ENABLE_PROFILING";
            var coreclrEnabledKey = "CORECLR_ENABLE_PROFILING";

            if (IsCoreClr() && (string)environmentVariables[coreclrEnabledKey] == "1")
            {
                profilerEnabled = true;
            }
            else if ((string)environmentVariables[corEnabledKey] == "1")
            {
                profilerEnabled = true;
            }

            var threadCount = 20;
            var iterationsPerThread = 50_000;
            var expectedIterations = 50_000; // threadCount * iterationsPerThread;
            var performance = new Performance()
            {
                ProfilerEnabled = profilerEnabled,
                TargetFramework = RuntimeFrameworkDescription,
                OperationCount = expectedIterations,
                OperationName = "StringSet -> StringGet"
            };

            var exceptionDictionary = new ConcurrentDictionary<string, int>();

            try
            {
                var firstCallTimer = new Stopwatch();

                firstCallTimer.Start();
                DoStringSetGetCalls();
                firstCallTimer.Stop();
                expectedIterations--;
                performance.FirstCallMilliseconds = firstCallTimer.ElapsedMilliseconds;

                //var threadRepresentation = Enumerable.Range(0, threadCount).ToArray();
                //var threads =
                //    threadRepresentation
                //       .Select(
                //            idx => new Thread(
                //                thread =>
                //                {
                //                    try
                //                    {
                //                        var i = 0;
                //                        while (i++ < iterationsPerThread)
                //                        {
                //                            DoStringSetGetCalls(redis);
                //                        }
                //                    }
                //                    catch
                //                    {
                //                    }
                //                }))
                //       .ToList();

                //foreach (var thread in threads)
                //{
                //    thread.Start();
                //}

                //while (threads.Any(x => x.IsAlive))
                //{
                //    Thread.Sleep(1000);
                //}

                for (var i = 0; i < 5_000; i++)
                {
                    ReallyBigString += "-";
                }

                var timer = new Stopwatch();

                performance.TestStart = DateTime.Now;
                timer.Start();

                while (expectedIterations-- > 0)
                {
                    try
                    {
                        DoStringSetGetCalls();

                    }
                    catch (Exception ex)
                    {
                        exceptionDictionary.AddOrUpdate(ex.Message, key => 1, (key, count) => (count++));
                        Multiplexer?.Dispose(); // Let's be sure to start fresh TODO, is this best?
                        Multiplexer = null;
                    }

                    ReallyBigString += ".";
                }

                timer.Stop();
                performance.TestEnd = DateTime.Now;
                performance.TotalMilliseconds = timer.ElapsedMilliseconds;

                performance.AverageCallMilliseconds = performance.TotalMilliseconds / (performance.OperationCount - 1);
            }
            finally
            {
                Multiplexer?.Dispose();
            }

            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();

            var fileFriendlyDate = performance.TestStart.ToString("yyyy-dd-M--HH-mm-ss");
            var writePath = Path.Combine(solutionDirectory, "performance");
            var fileName = $"stackexchangeredis_{performance.ProfilerVersion}_{fileFriendlyDate}";
            if (profilerEnabled)
            {
                fileName += "_Profiled";
            }
            else
            {
                fileName += "_NonProfiled";
            }

            var filePath = Path.Combine(writePath, $"{fileName}.json");

            var json = JsonConvert.SerializeObject(performance, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private static void DoStringSetGetCalls()
        {
            var redis = GetMultiplexer();
            var db = redis.GetDatabase();
            const string Script = "redis.call('set', @key, @value)";

            var uniqueKey = Guid.NewGuid();
            var uniqueKeyText = uniqueKey.ToString();

            var prepared = LuaScript.Prepare(Script);
            db.ScriptEvaluate(prepared, new { key = (RedisKey)uniqueKeyText, value = ReallyBigString });
        }

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core");
        }

        private class Performance
        {
            public string ProfilerVersion { get; set; } = EnvironmentTools.GetCurrentTracerVersion();

            public string TargetFramework { get; set; }

            public bool ProfilerEnabled { get; set; }

            public string OperationName { get; set; }

            public DateTime TestStart { get; set; }

            public DateTime TestEnd { get; set; }

            public decimal FirstCallMilliseconds { get; set; }

            public decimal AverageCallMilliseconds { get; set; }

            public decimal TotalMilliseconds { get; set; }

            public decimal OperationCount { get; set; }

            public string ExceptionInformation { get; set; }
        }
    }
}
