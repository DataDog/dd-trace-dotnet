using System.Diagnostics;

namespace StartupBenchmarks;

public class StartupBenchmarks
{
    [Benchmark("Tracer disabled")]
    public void NoTracer(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment, enable: false);
    }

    [Benchmark("Tracer enabled, default configuration", IsBaseline = true)]
    public void TracerEnabled(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment);
    }

    [Benchmark("DD_INSTRUMENTATION_TELEMETRY_ENABLED=0")]
    public void TracerEnabled_InstrumentationTelemetryDisabled(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment);
        EnableInstrumentationTelemetry(startInfo.Environment, enable: false);
    }

    [Benchmark("DD_CIVISIBILITY_ENABLED=0")]
    public void TracerEnabled_CiVisDisabled(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment);
        EnableCiVisibility(startInfo.Environment, enable: false);
    }

    [Benchmark("DD_TRACE_LOG_DIRECTORY=/dev/null")]
    public void TracerEnabled_LogToDevNull(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment);
        SetLoggingDirectory(startInfo.Environment, "/dev/null");
    }

    [Benchmark("All of the settings above combined")]
    public void TracerEnabled_All(ProcessStartInfo startInfo)
    {
        EnableTracer(startInfo.Environment);
        EnableInstrumentationTelemetry(startInfo.Environment, enable: false);
        EnableCiVisibility(startInfo.Environment, enable: false);
        SetLoggingDirectory(startInfo.Environment, "/dev/null");
    }

    private void EnableTracer(IDictionary<string, string?> startInfo, bool enable = true)
    {
        startInfo["CORECLR_ENABLE_PROFILING"] = enable ? "1" : "0";
    }

    private static void EnableCiVisibility(IDictionary<string, string?> startInfo, bool enable = true)
    {
        startInfo["DD_CIVISIBILITY_ENABLED"] = enable ? "1" : "0";
    }

    private static void EnableInstrumentationTelemetry(IDictionary<string, string?> startInfo, bool enable = true)
    {
        startInfo["DD_INSTRUMENTATION_TELEMETRY_ENABLED"] = enable ? "1" : "0";
    }

    private static void SetLoggingDirectory(IDictionary<string, string?> startInfo, string directory)
    {
        startInfo["DD_TRACE_LOG_DIRECTORY"] = directory;
    }
}
