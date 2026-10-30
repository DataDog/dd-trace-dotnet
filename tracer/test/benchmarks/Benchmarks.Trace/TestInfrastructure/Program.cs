using System;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
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
                           .AddExporter(JsonExporter.FullCompressed)
                           .AddFilter(new SimpleFilter(IsRealAspNetCoreBenchmark));

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

            // Retrieve the [GlobalSetup] and [GlobalCleanup] methods, if any
            var globalSetupMethod = typeof(T).GetMethods().FirstOrDefault(m => Attribute.GetCustomAttribute(m, typeof(GlobalSetupAttribute)) != null);
            var globalCleanupMethod = typeof(T).GetMethods().FirstOrDefault(m => Attribute.GetCustomAttribute(m, typeof(GlobalCleanupAttribute)) != null);

            //Retrieve Arguments
            MethodInfo argMethod = null;
            var argAttribute = Attribute.GetCustomAttribute(benchmarkMethod, typeof(ArgumentsSourceAttribute));
            if (argAttribute != null)
            {
                var argMethodName = argAttribute.GetType().GetProperty("Name").GetValue(argAttribute) as string;
                argMethod = typeof(T).GetMethod(argMethodName);
            }

            T instance = new T();

            // Execute [GlobalSetup] method
            globalSetupMethod?.Invoke(instance, null);

            // Execute iterations
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

            // Execute [GlobalCleanup] method
            globalCleanupMethod?.Invoke(instance, null);

            return 0;
        }

        private static void Debug(object instance, MethodInfo method, object[] args, MethodInfo initMethod, MethodInfo cleanupMethod)
        {
            initMethod?.Invoke(instance, null);
            method.Invoke(instance, args.Length > 0 ? args : null);
            cleanupMethod?.Invoke(instance, null);
        }

#if NETCOREAPP3_0_OR_GREATER
        // The real AspNetCoreBenchmark body (a TestServer round trip) is
        // compiled into this assembly on .NET Core 3.0 and later.
        private const bool AspNetCoreBenchmarkIsReal = true;
#else
        private const bool AspNetCoreBenchmarkIsReal = false;
#endif

#if NET6_0_OR_GREATER
        // The real SingleSpanAspNetCoreBenchmark body is compiled into this
        // assembly on .NET 6.0 and later.
        private const bool SingleSpanAspNetCoreBenchmarkIsReal = true;
#else
        private const bool SingleSpanAspNetCoreBenchmarkIsReal = false;
#endif

        /// <summary>
        /// Skips the ASP.NET Core benchmarks on target runtimes where their
        /// bodies compile to a "return null;" stub because ASP.NET Core is
        /// unsupported there (see AutoInstrumentation/AspNetCoreBenchmark.cs).
        /// Those stubs measure the BenchmarkDotNet loop overhead instead of a
        /// scenario. Only the null bodies are filtered out: the real bodies, a
        /// TestServer round trip through the tracer, run on ASP.NET Core 3.0
        /// and later for SendRequest, and on .NET 6.0 and later for
        /// SingleSpanAspNetCore.
        /// </summary>
        private static bool IsRealAspNetCoreBenchmark(BenchmarkCase benchmarkCase)
        {
            var type = benchmarkCase.Descriptor.Type;

            if (type != typeof(AspNetCoreBenchmark) &&
                type != typeof(SingleSpanAspNetCoreBenchmark))
            {
                return true;
            }

            var runtime = benchmarkCase.Job.Environment.Runtime;

            if (runtime is null)
            {
                // Without an explicit target runtime the benchmark runs on the
                // host framework, so the body compiled into this assembly runs.
                return type == typeof(AspNetCoreBenchmark)
                    ? AspNetCoreBenchmarkIsReal
                    : SingleSpanAspNetCoreBenchmarkIsReal;
            }

            switch (runtime.RuntimeMoniker)
            {
                case RuntimeMoniker.Net472:
                case RuntimeMoniker.Net48:
                case RuntimeMoniker.Net481:
                case RuntimeMoniker.NetCoreApp20:
                case RuntimeMoniker.NetCoreApp21:
                case RuntimeMoniker.NetCoreApp22:
                    // ASP.NET Core is unsupported: both bodies are stubs.
                    return false;

                case RuntimeMoniker.NetCoreApp30:
                case RuntimeMoniker.NetCoreApp31:
                case RuntimeMoniker.Net50:
                    // SendRequest is real on ASP.NET Core 3.0 and later, but
                    // SingleSpanAspNetCore needs .NET 6.0 and is still a stub.
                    return type != typeof(SingleSpanAspNetCoreBenchmark);

                default:
                    // .NET 6.0 and later: both bodies are real.
                    return true;
            }
        }
    }
}
