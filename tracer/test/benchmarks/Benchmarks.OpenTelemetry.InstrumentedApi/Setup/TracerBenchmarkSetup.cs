using System;
using System.Linq;

namespace Benchmarks.OpenTelemetry.InstrumentedApi.Setup;

internal class TracerBenchmarkSetup
{
    static void PrintSetup()
    {
        Console.WriteLine($"Info: COR_PROFILER={Environment.GetEnvironmentVariable("COR_PROFILER")}");
        Console.WriteLine($"Info: COR_ENABLE_PROFILING={Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING")}");
        Console.WriteLine($"Info: COR_PROFILER_PATH={Environment.GetEnvironmentVariable("COR_PROFILER_PATH")}");

        Console.WriteLine($"Info: CORECLR_PROFILER={Environment.GetEnvironmentVariable("CORECLR_PROFILER")}");
        Console.WriteLine($"Info: CORECLR_ENABLE_PROFILING={Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING")}");
        Console.WriteLine($"Info: CORECLR_PROFILER_PATH={Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH")}");

        Console.WriteLine($"Info: DD_DOTNET_TRACER_HOME={Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME")}");
        Console.WriteLine($"Info: DD_TRACE_OTEL_ENABLED={Environment.GetEnvironmentVariable("DD_TRACE_OTEL_ENABLED")}");

        Console.WriteLine($"Datadog.Trace loaded = {AppDomain.CurrentDomain.GetAssemblies().Any(s => s.GetName().Name == "Datadog.Trace")}");
    }

    internal void GlobalSetup()
    {
        PrintSetup();
    }

    internal void GlobalCleanup()
    {
    }
}
