using System;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using Benchmarks.Trace.Jetbrains;
using System.Reflection.Metadata;
using BenchmarkDotNet.Attributes;
using System.Reflection;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;
using System.Collections;

namespace Benchmarks.Trace
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine($"Execution context: ");
            Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);

            var config = DefaultConfig.Instance;

#if DEBUG
            // Debug benchmark classes here
            // Example: return Debug<StringAspectsBenchmark>("RunStringAspectBenchmark");
            
            // be able to debug benchmarks if started in debug mode
            config = config.WithOptions(ConfigOptions.DisableOptimizationsValidator);
#endif
            const string jetBrainsDotTrace = "-jetbrains:dottrace";
            const string jetBrainsDotTraceTimeline = "-jetbrains:dottrace:timeline";
            const string jetBrainsDotMemory = "-jetbrains:dotmemory";
            const string datadogProfiler = "-datadog:profiler";

            bool? useDatadogProfiler = null;
            if (args?.Any(a => a == jetBrainsDotTrace) == true)
            {
                Console.WriteLine("Setting Jetbrains trace collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotTrace).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Trace);
            }
            else if (args?.Any(a => a == jetBrainsDotTraceTimeline) == true)
            {
                Console.WriteLine("Setting Jetbrains timeline trace collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotTraceTimeline).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.TimelineTrace);
            }
            else if (args?.Any(a => a == jetBrainsDotMemory) == true)
            {
                Console.WriteLine("Setting Jetbrains memory collection... (could take time downloading collector binaries)");
                args = args.Where(a => a != jetBrainsDotMemory).ToArray();
                config = config.WithJetbrains(JetbrainsProduct.Memory);
            }
            else if (args?.Any(a => a == datadogProfiler) == true)
            {
                Console.WriteLine("Setting Datadog Profiler...");
                args = args.Where(a => a != datadogProfiler).ToArray();
                useDatadogProfiler = true;
            }

            config = config.WithDatadog(useDatadogProfiler)
                           .AddExporter(JsonExporter.FullCompressed);

            var agentName = Environment.GetEnvironmentVariable("AGENT_NAME");
            if (Enum.TryParse(agentName, out AgentFilterAttribute.Agent benchmarkAgent))
            {
                var attributeName = $"{benchmarkAgent}Attribute";
                Console.WriteLine($"Found agent name {agentName}; executing only benchmarks decorated with '{attributeName}'");
                config = config.AddFilter(new AttributesFilter(new[] { attributeName }));
            }
            else
            {
                Console.WriteLine($"Unknown agent name {agentName}; executing all benchmarks");
            }

            Console.WriteLine("Running tests...");
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
            return Environment.ExitCode;
        }

        private static int Debug<T>(string methodName, params object[] arguments)
            where T : class, new()
        {
            // Retrieve the Benchmark method
            var benchmarkMethod = typeof(T).GetMethod(methodName);
            var initMethod = typeof(T).GetMethods().FirstOrDefault(m => Attribute.GetCustomAttribute(m, typeof(IterationSetupAttribute)) != null);
            var cleanupMethod = typeof(T).GetMethods().FirstOrDefault(m => Attribute.GetCustomAttribute(m, typeof(IterationCleanupAttribute)) != null);

            //Retrieve Arguments
            MethodInfo argMethod = null;
            var argAttribute = Attribute.GetCustomAttribute(benchmarkMethod, typeof(ArgumentsSourceAttribute));
            if (argAttribute != null)
            {
                var argMethodName = argAttribute.GetType().GetProperty("Name").GetValue(argAttribute) as string;
                argMethod = typeof(T).GetMethod(argMethodName);
            }

            T instance = new T();
            if (arguments.Length > 0 || argMethod == null)
            {
                Debug(instance, benchmarkMethod, arguments, initMethod, cleanupMethod);
            }
            else
            {
                var argEnumerable = argMethod?.Invoke(instance, null) as IEnumerable;
                foreach (var arg in argEnumerable)
                {
                    Debug(instance, benchmarkMethod, new object[] { arg }, initMethod, cleanupMethod);
                }
            }

            return 0;
        }

        private static void Debug(object instance, MethodInfo method, object[] args, MethodInfo initMethod, MethodInfo cleanupMethod)
        {
            initMethod?.Invoke(instance, null);
            method.Invoke(instance, args.Length > 0 ? args : null);
            cleanupMethod?.Invoke(instance, null);
        }
    }
}
