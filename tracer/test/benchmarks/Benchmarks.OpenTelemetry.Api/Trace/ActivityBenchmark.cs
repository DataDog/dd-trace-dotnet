using System;
using System.Diagnostics;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

#if INSTRUMENTEDAPI
namespace Benchmarks.OpenTelemetry.InstrumentedApi.Trace;
#elif PROFILEDAPI
namespace Benchmarks.OpenTelemetry.ProfiledApi.Trace;
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

    // Pre-built so the benchmark measures Activity.SetTag()/Stop() cost, not string concatenation.
    private static readonly string[] ManyTagKeys = BuildTagKeys(32);

    private Setup.ActivityBenchmarkSetup activityBenchmarkSetup;

    /// <summary>
    /// Gets or sets the number of tags set on the Activity before it is stopped, for <see cref="StartSpan_ManyTags"/>.
    /// Sized to make the listener path's stop-time tag re-enumeration (<c>OtlpHelpers.AgentConvertSpan</c>)
    /// visible against interception's per-SetTag writes: 0 is the no-tag floor, 8 is a realistic span, 32 is
    /// the stress case where the re-enumeration slope should dominate.
    /// </summary>
    [Params(0, 8, 32)]
    public int TagCount { get; set; }

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

    /// <summary>
    /// Prices the listener path's stop-time tag copy: with the flag off, every tag set here lands in
    /// Activity's own storage and is only walked onto the Datadog span when the Activity stops
    /// (<c>ActivityHandlerCommon.CloseActivityScope</c> -&gt; <c>OtlpHelpers.AgentConvertSpan</c>), so the
    /// cost scales with <see cref="TagCount"/> at <c>Dispose()</c> time. With interception, each
    /// <c>SetTag</c> writes straight onto the span immediately, so there is nothing left to do at stop.
    /// </summary>
    [Benchmark]
    public void StartSpan_ManyTags()
    {
        using var activity = this.activityBenchmarkSource.StartActivity("operation");
        for (var i = 0; i < this.TagCount; i++)
        {
            activity?.SetTag(ManyTagKeys[i], i);
        }

        activity?.Dispose();
    }

    private static string[] BuildTagKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = "tag" + i.ToString(CultureInfo.InvariantCulture);
        }

        return keys;
    }
}
