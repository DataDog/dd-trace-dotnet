#nullable enable
using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using ActivityIdFormat = System.Diagnostics.ActivityIdFormat;
using ActivityListener = System.Diagnostics.ActivityListener;
using ActivitySamplingResult = System.Diagnostics.ActivitySamplingResult;
using ActivitySource = System.Diagnostics.ActivitySource;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
public class ActivityBenchmark
{
    private const string SourceName = "BenchmarkSource";
    private static readonly DateTime _startTime = DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime;
    private static readonly DateTime _endTime = DateTimeOffset.FromUnixTimeSeconds(5).UtcDateTime;

    private ActivitySource? _source;
    private ActivityListener? _activityListener;

    // The delegates ActivityListener.Initialize() emits, so the benchmark drives the real listener->handler pipeline
    private Action<Activity>? _onStarted;
    private Action<Activity>? _onStopped;

    [GlobalSetup]
    public void GlobalSetup()
    {
        TracerHelper.SetGlobalTracer();
        _source = new ActivitySource(SourceName);

        _activityListener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData };

        ActivitySource.AddActivityListener(_activityListener);

        // Register the source against the handler chain (resolves to DefaultActivityHandler), the
        // same way ActivityListenerHandler.OnShouldListenTo would be called from the emitted
        // ShouldListenTo delegate during real initialization.
        ActivityListenerHandler.OnShouldListenTo(_source.DuckCast<IActivitySource>());

        // Benchmarks.Trace references DiagnosticSource 6.0+, which Initialize() maps to the Activity6 shape
        _onStarted = (Action<Activity>)ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate(typeof(Activity), ActivityShape.Activity6);
        _onStopped = (Action<Activity>)ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate(typeof(Activity), ActivityShape.Activity6);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _source!.Dispose();
        _activityListener!.Dispose();
        TracerHelper.CleanupGlobalTracer();
    }

    [Benchmark(Baseline = true)]
    public void StartStopWithChild_Baseline()
    {
        using var parent = CreateActivity();
        using (var child = CreateActivity(parent))
        {
            child.Stop();
        }
        parent.Stop();
    }

    [Benchmark]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public void StartStopWithChild()
    {
        using var parent = CreateActivity();
        using var child = CreateActivity(parent);
        _onStarted!(parent);
        _onStarted(child);
        child.Stop();
        _onStopped!(child);
        parent.Stop();
        _onStopped(parent);
    }

    [Benchmark]
    public void StartStopWithChild_Hierarchical()
    {
        using var parent = CreateActivity(idFormat: ActivityIdFormat.Hierarchical);
        using var child = CreateActivity(parent, idFormat: ActivityIdFormat.Hierarchical);
        _onStarted!(parent);
        _onStarted(child);
        child.Stop();
        _onStopped!(child);
        parent.Stop();
        _onStopped(parent);
    }

    private Activity CreateActivity(Activity? parent = null, ActivityIdFormat idFormat = ActivityIdFormat.W3C)
    {
        var activity = parent is null
                           ? _source!.CreateActivity("parent", System.Diagnostics.ActivityKind.Internal)
                           : _source!.CreateActivity("child", System.Diagnostics.ActivityKind.Internal, parent!.Context);

        if (activity is null)
        {
            throw new Exception("Failed to create an activity");
        }

        activity.SetStartTime(_startTime);
        activity.SetIdFormat(idFormat); // we have more logic for TraceId/SpanId activities

        activity.Start(); // creates necessary TraceId/SpanId that we need

        activity.SetEndTime(_endTime);

        activity.AddTag("tag", "value");
        activity.AddTag("tag[]", new bool[2] { true, false });

        return activity;
    }
}
