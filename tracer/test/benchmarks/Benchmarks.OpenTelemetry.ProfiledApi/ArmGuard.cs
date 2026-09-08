using System;
using System.Diagnostics;
using System.Linq;

namespace Benchmarks.OpenTelemetry.ProfiledApi;

/// <summary>
/// Fails loudly, instead of silently producing a plausible-looking but meaningless comparison, if this
/// process is not actually running the arm it claims to. Do not trust a diff between two arms without this.
/// See plan step 4, "Assert the apparatus".
/// </summary>
internal static class ArmGuard
{
    private const string DatadogSpanCustomProperty = "__dd_span__";

    public static void Verify()
    {
        var expectInterceptionEnabled = string.Equals(
            Environment.GetEnvironmentVariable("DD_TRACE_OTEL_ACTIVITY_INTERCEPTION_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var datadogAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                        .FirstOrDefault(a => a.GetName().Name == "Datadog.Trace");

        if (datadogAssembly is null)
        {
            throw new InvalidOperationException(
                "ArmGuard: Datadog.Trace is not loaded in this process. The native profiler did not attach - " +
                "check CORECLR_ENABLE_PROFILING / CORECLR_PROFILER / CORECLR_PROFILER_PATH_64 / DD_DOTNET_TRACER_HOME " +
                "/ DD_NATIVELOADER_CONFIGFILE.");
        }

        Console.WriteLine($"ArmGuard: Datadog.Trace loaded from '{datadogAssembly.Location}' (version {datadogAssembly.GetName().Version}).");

        using var activitySource = new ActivitySource("ActivityBenchmark");
        using var activity = activitySource.StartActivity("__armguard_probe__");

        if (activity is null)
        {
            throw new InvalidOperationException(
                "ArmGuard: ActivitySource.StartActivity returned null - nothing is listening to this source. " +
                "Check DD_TRACE_OTEL_ENABLED=true is set for this job.");
        }

        // __dd_span__ is set only by ActivityStartIntegration.CreateAndLinkScope on the interception path;
        // the managed ActivityListener path never touches it. Do NOT use IsAllDataRequested here - both
        // paths set it, so it cannot distinguish the arms.
        var hasDatadogSpan = activity.GetCustomProperty(DatadogSpanCustomProperty) is not null;

        if (hasDatadogSpan != expectInterceptionEnabled)
        {
            throw new InvalidOperationException(
                $"ArmGuard: DD_TRACE_OTEL_ACTIVITY_INTERCEPTION_ENABLED={expectInterceptionEnabled} but the " +
                $"'{DatadogSpanCustomProperty}' custom property presence was '{hasDatadogSpan}'. This process " +
                "is not running the arm it claims to be - the benchmark numbers cannot be trusted.");
        }

        Console.WriteLine($"ArmGuard: confirmed interception={hasDatadogSpan} (expected {expectInterceptionEnabled}).");
    }
}
