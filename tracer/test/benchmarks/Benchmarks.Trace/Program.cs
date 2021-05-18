using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine($"Execution context: ");
            Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);

            if (args?.Any(a => a == "-jetbrains") == true)
            {
                ExecuteWithJetbrainsTools(args);
            }
            else
            {
                var config = DefaultConfig.Instance.AddExporter(DatadogExporter.Default);
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
            }
        }

        private static void ExecuteWithJetbrainsTools(string[] args)
        {
            HashSet<string> hashSet = new HashSet<string>(args.Where(a => a != "-jetbrains").Select(a => a.ToLowerInvariant()));
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
                            for (var i = 0; i < 100_000; i++)
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
                            for (var i = 0; i < 100_000; i++)
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
