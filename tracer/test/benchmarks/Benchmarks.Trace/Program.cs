using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using Tony.BenchmarkDotnet.Jetbrains;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine($"Execution context: ");
            Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);

            var config = DefaultConfig.Instance;
            if (args?.Any(a => a == "-jetbrains") == true)
            {
                ExecuteWithJetbrainsTools(args);
                return;
            }

            const string jetBrainsDotTrace = "-jetbrains:dottrace";
            const string jetBrainsDotMemory = "-jetbrains:dotmemory";

            if (args?.Any(a => a == jetBrainsDotTrace) == true)
            {
                args = args.Where(a => a != jetBrainsDotTrace).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Trace);
            }
            else if (args?.Any(a => a == jetBrainsDotMemory) == true)
            {
                args = args.Where(a => a != jetBrainsDotMemory).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Memory);
            }
            
            config = config.WithDatadog()
                           .AddExporter(JsonExporter.FullCompressed);
            
            var agentName = Environment.GetEnvironmentVariable("AGENT_NAME");
            if (Enum.TryParse(agentName, out AgentFilterAttribute.Agent benchmarkAgent))
            {
                var attributeName = $"{benchmarkAgent}Attribute";
                Console.WriteLine($"Found agent name {agentName}; executing only benchmarks decorated with '{attributeName}");
                config.AddFilter(new AttributesFilter(new[] { attributeName }));
            }
            else
            {
                Console.WriteLine($"Unknown agent name {agentName}; executing all benchmarks");
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }

        private static void ExecuteWithJetbrainsTools(string[] args)
        {
            var numIter = 100_000;
            var numArg = args.FirstOrDefault(a => a.StartsWith("-n:", StringComparison.OrdinalIgnoreCase))?.Substring(3);
            if (int.TryParse(numArg, out var numArgInt))
            {
                numIter = numArgInt;
            }

            if ((JetBrains.Profiler.Api.MeasureProfiler.GetFeatures() &
                 JetBrains.Profiler.Api.MeasureFeatures.Ready) == 0 &&
                (JetBrains.Profiler.Api.MemoryProfiler.GetFeatures() &
                 JetBrains.Profiler.Api.MemoryFeatures.Ready) == 0)
            {
                Console.WriteLine("Waiting for jetbrains dotTrace/dotMemory to attach...");
                var currentProcess = Process.GetCurrentProcess();
                Console.WriteLine("Process Id: {0}, Name: {1}", currentProcess.Id, currentProcess.ProcessName);
                while ((JetBrains.Profiler.Api.MeasureProfiler.GetFeatures() &
                        JetBrains.Profiler.Api.MeasureFeatures.Ready) == 0 &&
                       (JetBrains.Profiler.Api.MemoryProfiler.GetFeatures() &
                        JetBrains.Profiler.Api.MemoryFeatures.Ready) == 0)
                {
                    Thread.Sleep(1000);
                }
            }
            Console.WriteLine("Connected. Running benchmarks (N: {0})", numIter);
            HashSet<string> hashSet = new HashSet<string>(args.Where(a => a != "-jetbrains" && !a.StartsWith("-n:", StringComparison.OrdinalIgnoreCase)).Select(a => a.ToLowerInvariant()));
            var benchmarkTypes = typeof(Program).Assembly.GetTypes().Where(t => hashSet.Contains(t.Name.ToLowerInvariant()));
            foreach (var benchmarkType in benchmarkTypes)
            {
                foreach (var method in benchmarkType.GetMethods())
                {
                    if (method.GetCustomAttributes(false).Any(att => att is BenchmarkAttribute))
                    {
                        var benchmarkInstance = Activator.CreateInstance(benchmarkType);
                        var groupName = string.Format("{0}.{1}", benchmarkType.FullName, method.Name);
                        Console.WriteLine("Running: " + groupName);
                        for (var i = 0; i < 1000; i++)
                        {
                            method.Invoke(benchmarkInstance, null);
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        if ((JetBrains.Profiler.Api.MeasureProfiler.GetFeatures() & JetBrains.Profiler.Api.MeasureFeatures.Ready) != 0)
                        {
                            Console.WriteLine("  Collecting Data...");
                            JetBrains.Profiler.Api.MeasureProfiler.StartCollectingData(groupName);
                            for (var i = 0; i < numIter; i++)
                            {
                                method.Invoke(benchmarkInstance, null);
                            }
                            JetBrains.Profiler.Api.MeasureProfiler.StopCollectingData();
                            JetBrains.Profiler.Api.MeasureProfiler.SaveData(groupName);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }

                        if ((JetBrains.Profiler.Api.MemoryProfiler.GetFeatures() & JetBrains.Profiler.Api.MemoryFeatures.Ready) != 0)
                        {
                            Console.WriteLine("  Getting memory snapshot...");
                            JetBrains.Profiler.Api.MemoryProfiler.ForceGc();
                            JetBrains.Profiler.Api.MemoryProfiler.CollectAllocations(true);
                            for (var i = 0; i < numIter; i++)
                            {
                                method.Invoke(benchmarkInstance, null);
                            }
                            JetBrains.Profiler.Api.MemoryProfiler.GetSnapshot(groupName);
                            JetBrains.Profiler.Api.MemoryProfiler.CollectAllocations(false);
                        }
                    }
                }
                Console.WriteLine("Done.");
            }
        }
    }
}
