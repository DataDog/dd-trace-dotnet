using System.Collections.Specialized;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using ActivityListener = System.Diagnostics.ActivityListener;
using ActivitySamplingResult = System.Diagnostics.ActivitySamplingResult;
using ActivitySource = System.Diagnostics.ActivitySource;

namespace Benchmarks.Trace;

// Benchmarks commented out to look into better way of benchmarking on CI as it may not work with auto-instrumentation

// [MemoryDiagnoser]
// [BenchmarkAgent6]
public class OpenTelemetryAutoBenchmark
{
    internal static ActivitySource ActivitySource { get; set; }
    internal static Datadog.Trace.Tracer DatadogTracer { get; set; }
    internal static OpenTelemetry.Trace.Tracer OpenTelemetryTracer { get; set; }
    static OpenTelemetryAutoBenchmark()
    {
        ActivitySource = new ActivitySource("ActivityBenchmark");

        var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(activityListener);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                      .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dd-trace-dotnet"))
                                      .AddSource("ActivityBenchmark")
                                      .AddConsoleExporter()
                                      .Build();

        OpenTelemetryTracer = tracerProvider.GetTracer("dd-trace-dotnet");
        var environmentVars = new NameValueCollection();
        environmentVars.Add("DD_TRACE_OTEL_ENABLED", "true");

        var settings = new TracerSettings(new NameValueConfigurationSource(environmentVars)) { StartupDiagnosticLogEnabled = false };

        Datadog.Trace.Tracer.UnsafeSetTracerInstance(new Datadog.Trace.Tracer(settings, new DummyAgentWriter(), null, null, null));
        Datadog.Trace.ClrProfiler.Instrumentation.Initialize();
    }

    // [Benchmark]
    public void CreateActivitySpan()
    {
        using (var activity = ActivitySource.StartActivity("name"))
        {
            activity.SetTag("key", "true");
        }
    }

    // [Benchmark]
    public void CreateOpenTelemetrySpan()
    {
        using (var openTelemetry = OpenTelemetryTracer.StartActiveSpan("name"))
        {
            openTelemetry.SetAttribute("key", "true");
        }
    }
}
