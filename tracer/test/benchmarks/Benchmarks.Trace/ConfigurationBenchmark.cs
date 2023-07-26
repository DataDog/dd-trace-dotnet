using BenchmarkDotNet.Attributes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent4]
public class ConfigurationBenchmark
{
    static ConfigurationBenchmark()
    {
        _ = new ConfigurationBenchmark().CreateImmutableTracerSettings();
    }

    [Benchmark]
    public ImmutableTracerSettings CreateImmutableTracerSettings()
    {
        var configurationSource = GlobalConfigurationSource.CreateDefaultConfigurationSource();
        var configurationTelemetry = new ConfigurationTelemetry();
        var tracerSettings = new TracerSettings(configurationSource, configurationTelemetry);
        var immutableTracerSettings = new ImmutableTracerSettings(tracerSettings, true);
        return immutableTracerSettings;
    }
}
