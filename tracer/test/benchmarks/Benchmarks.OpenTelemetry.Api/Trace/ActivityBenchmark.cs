using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

#if INSTRUMENTEDAPI
namespace Benchmarks.OpenTelemetry.InstrumentedApi.Trace;
#else
namespace Benchmarks.OpenTelemetry.Api.Trace;
#endif

/// <summary>
/// Span benchmarks
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
public class ActivityBenchmark
{
    private readonly ActivitySource activityBenchmarkSource = new("ActivityBenchmark");
    private static readonly Exception exception = new Exception("Error");
    private static readonly DateTimeOffset timestamp = DateTimeOffset.UtcNow;
    private Setup.ActivityBenchmarkSetup activityBenchmarkSetup;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.activityBenchmarkSetup = new Setup.ActivityBenchmarkSetup();
        this.activityBenchmarkSetup.GlobalSetup();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.activityBenchmarkSetup.GlobalCleanup();
        this.activityBenchmarkSource?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void StartSpan()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity?.Dispose();
    }

    [Benchmark]
    public void StartSpan_AddEvent_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity?.AddEvent(new("event", timestamp));
        activity?.Dispose();
    }

    [Benchmark]
    public bool? StartSpan_GetContext_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        var result = activity?.Context.IsRemote;
        activity?.Dispose();

        return result;
    }

#if NET9_0
    [Benchmark]
    public void StartSpan_RecordException_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity?.AddException(exception, timestamp: timestamp);
        activity?.Dispose();
    }
#endif

    [Benchmark]
    public void StartSpan_SetStatus_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.Dispose();
    }

    [Benchmark]
    public void StartSpan_SetAttributes_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity?.SetTag("string", "value");
        activity?.SetTag("int", 42);
        activity?.SetTag("bool", true);
        activity?.SetTag("double", 3.14);
        activity?.Dispose();
    }

    [Benchmark]
    public void StartSpan_UpdateName_Sampled()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        activity!.DisplayName = "updated";
        activity.Dispose();
    }
}
