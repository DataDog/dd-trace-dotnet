using BenchmarkDotNet.Configs;

#nullable enable

namespace Benchmarks.Trace.DatadogProfiler;

/// <summary>
/// Datadog profiler extensions
/// </summary>
public static class DatadogProfilerExtensions
{
    /// <summary>
    /// Configure the Datadog profiler diagnoser
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <returns>Same configuration instance</returns>
    public static IConfig WithDatadogProfiler(this IConfig config)
    {
        return config
              .AddDiagnoser(new DatadogProfilerDiagnoser());
    }
}
