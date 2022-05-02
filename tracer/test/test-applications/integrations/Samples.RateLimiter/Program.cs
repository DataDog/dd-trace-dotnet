using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

#nullable enable annotations

namespace Samples.RateLimiter
{
    internal static class Program
    {
        private static readonly string ServiceDogWalker = "dog_walker";
        private static readonly string RootWalkOperation = "root_walk";

        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        private static void Main()
        {
            var numberOfSeconds = 5;
            var maxMilliseconds = numberOfSeconds * 1000;
            var configuredLimitPerSecond = int.Parse(Environment.GetEnvironmentVariables()["DD_TRACE_RATE_LIMIT"].ToString());

            Console.WriteLine($"Ready to run for {numberOfSeconds} seconds.");
            Console.WriteLine($"Configured rate limit of {configuredLimitPerSecond}");

            SampleHelpers.ConfigureTracer(ServiceDogWalker);

            PrepKeys(ServiceDogWalker, RootWalkOperation, configuredLimitPerSecond * numberOfSeconds);
            var timer = new Stopwatch();

            timer.Start();

            while (true)
            {
                if (timer.ElapsedMilliseconds >= maxMilliseconds)
                {
                    timer.Stop();
                    break;
                }

                RunStuff(ServiceDogWalker, RootWalkOperation);
            }

            Console.WriteLine();

            foreach (var key in Counts.Keys)
            {
                var isExpect = key.Contains("Expecting");

                if (isExpect)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{key}");
                }
                else
                {
                    Console.WriteLine($"{key}: {Counts[key]}");
                }
            }
        }

        private static void RunStuff(string serviceName, string operationName)
        {
            Console.Write(".");

            Counts[Key(serviceName, operationName)]++;
            IDisposable root;
            using (root = SampleHelpers.CreateScope(operationName: operationName))
            {
                Thread.Sleep(3);

                using (var sub = SampleHelpers.CreateScope(operationName: "sub"))
                {
                    Thread.Sleep(2);
                }

                Thread.Sleep(3);
            }

            var metrics = GetMetrics(root);
            var priorityKey = "_sampling_priority_v1";
            var priority = metrics[priorityKey];
            Counts[Key(serviceName, operationName, priority)]++;
        }

        private static void PrepKeys(string service, string operation, decimal? expectedKeeps)
        {
            Counts.Add($"Expecting max of {expectedKeeps?.ToString() ?? "UNKNOWN"} P1s for {Key(service, operation)}", 0);
            Counts.Add(Key(service, operation), 0);
            Counts.Add(Key(service, operation, 1), 0);
            Counts.Add(Key(service, operation, 0), 0);
        }

        private static string Key(string service, string operation, double? priority = null)
        {
            if (priority == null)
            {
                return $"{service}_{operation}";
            }

            return $"{service}_{operation}_{priority}";
        }

        private static ConcurrentDictionary<string, double> GetMetrics(object root)
        {
            ConcurrentDictionary<string, double> metrics = SampleHelpers.GetMetrics(root);

            if (metrics == null)
            {
                throw new Exception("Couldn't find metrics dictionary.");
            }

            return metrics;
        }
    }
}
