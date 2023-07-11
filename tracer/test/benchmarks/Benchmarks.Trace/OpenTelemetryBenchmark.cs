using System.Collections.Specialized;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using ActivityListener = System.Diagnostics.ActivityListener;
using ActivitySamplingResult = System.Diagnostics.ActivitySamplingResult;
using ActivitySource = System.Diagnostics.ActivitySource;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent6]
public class OpenTelemetryBenchmark
{
    private static readonly ActivitySource _source;
    static OpenTelemetryBenchmark()
    {
        _source = new ActivitySource("ActivityBenchmark");

        var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(activityListener);

        var environmentVars = new NameValueCollection();
        environmentVars.Add("DD_TRACE_OTEL_ENABLED", "true");

        var settings = new TracerSettings(new NameValueConfigurationSource(environmentVars)) { StartupDiagnosticLogEnabled = false };

        Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));
        Datadog.Trace.ClrProfiler.Instrumentation.Initialize();
        var bench = new OpenTelemetryBenchmark();
        bench.CreateActivitySpan();
    }

    [Benchmark]
    public void CreateActivitySpan()
    {
        using (var activity = _source.StartActivity("name"))
        {
            activity.SetTag("key", "true");
        }
    }

    [Benchmark]
    public void CreateDatadogSpan()
    {
        using (var scope = Tracer.Instance.StartActive("name"))
        {
            scope.Span.SetTag("key", "true");
        }
    }
}
