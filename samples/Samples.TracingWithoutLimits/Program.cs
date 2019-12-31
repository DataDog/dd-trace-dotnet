using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Datadog.Trace;

namespace Samples.TracingWithoutLimits
{
    internal static class Program
    {
        // We want to keep most of the walking
        private static readonly string ServiceDogWalker = "dog_walker";
        private static readonly string ServiceCatWalker = "cat_walker";
        private static readonly string ServiceRatWalker = "rat_walker";

        private static readonly string RootWalkOperation = "root_walk";

        // We want to drop most of the running
        private static readonly string ServiceDogRunner = "dog_runner";
        private static readonly string ServiceCatRunner = "cat_runner";
        private static readonly string ServiceRatRunner = "rat_runner";

        private static readonly string RootRunOperation = "root_run";

        // Nested operations
        private static readonly string SubOperation = "sub";
        private static readonly string OpenOperation = "open";
        private static readonly string CloseOperation = "close";

        // No matches
        private static readonly string UnknownService = "unknown_service";
        private static readonly string UnknownOperation = "root_unknown";

        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        private static void Main()
        {
            var total = 500;

            PrepKeys(ServiceDogWalker, RootWalkOperation, total * 1m);
            PrepKeys(ServiceCatWalker, RootWalkOperation, total * 0.8m);
            PrepKeys(ServiceRatWalker, RootWalkOperation, total * 0.5m);
            PrepKeys(ServiceDogRunner, RootRunOperation, total * 0.2m);
            PrepKeys(ServiceCatRunner, RootRunOperation, total * 0.1m);
            PrepKeys(ServiceRatRunner, RootRunOperation, total * 0.0m);

            PrepKeys(UnknownService, UnknownOperation, null);

            // Configured rules:
            // [{"service":"rat.*","name":".*run.*","sample_rate":0},{"service":"dog.*","name":".+walk","sample_rate":1.0},{"service":"cat.*","name":".+walk","sample_rate":0.8},{"name":".+walk","sample_rate":0.5},{"service":"dog.*","sample_rate":0.2},{"service":"cat.*","sample_rate":0.1}]
            for (var i = 1; i <= total; i++)
            {
                Console.WriteLine($"{ConsoleTime()} Iteration {i} beginning.");

                // We should keep 100 %
                RunStuff(ServiceDogWalker, RootWalkOperation);

                // We should keep 80%
                RunStuff(ServiceCatWalker, RootWalkOperation);

                // We should keep 50%, because it matches the fallback ".+walk" rule
                RunStuff(ServiceRatWalker, RootWalkOperation);

                // We should keep 20%, as it matches the fallback dog rule
                RunStuff(ServiceDogRunner, RootRunOperation);

                // We should keep 10%, as it matches the fallback cat rule
                RunStuff(ServiceCatRunner, RootRunOperation);

                // We should keep 0%, we don't want running rats
                RunStuff(ServiceRatRunner, RootRunOperation);

                // This should fallback to the default behavior, determined by the agent
                RunStuff(UnknownService, UnknownOperation);

                Console.WriteLine($"{ConsoleTime()} Iteration {i} ending.");
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

        private static string ConsoleTime()
        {
            return $"[{DateTime.Now:hh:mm:ss:fff}]";
        }

        private static void RunStuff(string serviceName, string operationName)
        {
            Counts[Key(serviceName, operationName)]++;

            Scope root;

            using (root = Tracer.Instance.StartActive(operationName: operationName, serviceName: serviceName))
            {
                Thread.Sleep(3);

                using (var sub = Tracer.Instance.StartActive(operationName: SubOperation, serviceName: serviceName))
                {
                    Thread.Sleep(2);

                    using (var open = Tracer.Instance.StartActive(operationName: OpenOperation, serviceName: serviceName))
                    {
                        Thread.Sleep(2);
                    }

                    using (var close = Tracer.Instance.StartActive(operationName: CloseOperation, serviceName: serviceName))
                    {
                        Thread.Sleep(1);
                    }
                }

                Thread.Sleep(3);
            }

            var metrics = GetMetrics(root);
            var rulePsrKey = "_dd.rule_psr";
            var limitPsrKey = "_dd.limit_psr";
            var priorityKey = "_sampling_priority_v1";

            if (serviceName == UnknownService)
            {
                if (metrics.ContainsKey(rulePsrKey))
                {
                    throw new Exception($"{rulePsrKey} should not be set in the fallback behavior.");
                }
            }
            else if (!metrics.ContainsKey(rulePsrKey))
            {
                throw new Exception($"{rulePsrKey} must be set in a user defined rule.");
            }

            var priority = metrics[priorityKey];

            if (priority > 0f && !metrics.ContainsKey(limitPsrKey))
            {
                throw new Exception($"{limitPsrKey} must be set if a user defined rule is configured and the trace is sampled.");
            }

            Counts[Key(serviceName, operationName, priority)]++;
        }

        private static void PrepKeys(string service, string operation, decimal? expectedKeeps)
        {
            Counts.Add($"Expecting {expectedKeeps?.ToString() ?? "UNKNOWN"} P1s for {Key(service, operation)}", 0);
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

        private static ConcurrentDictionary<string, double> GetMetrics(Scope root)
        {
            ConcurrentDictionary<string, double> metrics = null;

            foreach (var property in typeof(Span)
               .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic))
            {
                if (property.Name == "Metrics")
                {
                    metrics = (ConcurrentDictionary<string, double>)property.GetValue(root.Span);
                    break;
                }
            }

            if (metrics == null)
            {
                throw new Exception("Couldn't find metrics dictionary.");
            }

            return metrics;
        }
    }
}
