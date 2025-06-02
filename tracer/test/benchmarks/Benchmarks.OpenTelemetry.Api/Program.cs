using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;

#if INSTRUMENTEDAPI
namespace Benchmarks.OpenTelemetry.InstrumentedApi;
#else
namespace Benchmarks.OpenTelemetry.Api;
#endif

internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine($"Execution context: ");
        Console.WriteLine("CurrentCulture is {0}.", CultureInfo.CurrentCulture.Name);

        var config = DefaultConfig.Instance;

#if DEBUG
        // Debug benchmark classes here
        // Example: return Debug<TracerBenchmark>("StartActiveSpan");
        // return Debug<Trace.ActivityBenchmark>("StartSpan");

        // be able to debug benchmarks if started in debug mode
        config = config.WithOptions(ConfigOptions.DisableOptimizationsValidator);
#endif

        config = config.WithDatadog()
                       .AddExporter(JsonExporter.FullCompressed);

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
        globalSetupMethod.Invoke(instance, null);

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
        globalCleanupMethod.Invoke(instance, null);

        return 0;
    }

    private static void Debug(object instance, MethodInfo method, object[] args, MethodInfo initMethod, MethodInfo cleanupMethod)
    {
        initMethod?.Invoke(instance, null);
        method.Invoke(instance, args.Length > 0 ? args : null);
        cleanupMethod?.Invoke(instance, null);
    }
}
