#nullable enable
using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
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

    private Datadog.Trace.Activity.DuckTypes.ActivitySource _duckSource;
    private ActivitySource _source;
    private ActivityListener _activityListener;

    [GlobalSetup]
    public void GlobalSetup()
    {
        TracerHelper.SetGlobalTracer();
        _source = new ActivitySource(SourceName);

        _activityListener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData };

        ActivitySource.AddActivityListener(_activityListener);

        _duckSource = new Datadog.Trace.Activity.DuckTypes.ActivitySource { Name = _source.Name, Version = _source.Version ?? string.Empty };
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _source.Dispose();
        _activityListener.Dispose();
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
        var parentMock = parent.DuckAs<IActivity6>()!;
        var childMock = child.DuckAs<IActivity6>()!;
        var handler = new DefaultActivityHandler();
        handler.ActivityStarted(SourceName, parentMock);
        handler.ActivityStarted(SourceName, childMock);
        child.Stop();
        handler.ActivityStopped(SourceName, childMock);
        parent.Stop();
        handler.ActivityStopped(SourceName, parentMock);
    }

    private Activity CreateActivity(Activity? parent = null)
    {
        var activity = parent is null
                           ? _source.CreateActivity("parent", System.Diagnostics.ActivityKind.Internal)
                           : _source.CreateActivity("child", System.Diagnostics.ActivityKind.Internal, parent!.Context);

        if (activity is null)
        {
            throw new Exception("Failed to create an activity");
        }

        activity.SetStartTime(_startTime);
        activity.SetIdFormat(ActivityIdFormat.W3C); // we have more logic for TraceId/SpanId activities

        activity.Start(); // creates necessary TraceId/SpanId that we need

        activity.SetEndTime(_endTime);

        activity.AddTag("tag", "value");
        activity.AddTag("tag[]", new bool[2] { true, false });

        return activity;
    }
}
