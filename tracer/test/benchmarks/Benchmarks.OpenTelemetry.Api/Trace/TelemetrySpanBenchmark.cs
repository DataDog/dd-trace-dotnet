using System;
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
public class TelemetrySpanBenchmark
{
    private static readonly Exception exception = new Exception("Error");
    private static readonly DateTimeOffset timestamp = DateTimeOffset.UtcNow;
    private Tracer alwaysSampleTracer;
    private Setup.TelemetrySpanBenchmarkSetup telemetrySpanBenchmarkSetup;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.telemetrySpanBenchmarkSetup = new Setup.TelemetrySpanBenchmarkSetup();
        this.telemetrySpanBenchmarkSetup.GlobalSetup();

        this.alwaysSampleTracer = TracerProvider.Default.GetTracer("TelemetrySpanBenchmark_AlwaysOnSample");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.telemetrySpanBenchmarkSetup.GlobalCleanup();
    }

    [Benchmark(Baseline = true)]
    public void StartSpan()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.End();
    }

    [Benchmark]
    public void StartSpan_AddEvent_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.AddEvent("event", timestamp);
        span.End();
    }

    [Benchmark]
    public bool StartSpan_GetContext_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        var result = span.Context.IsRemote;
        span.End();

        return result;
    }

    [Benchmark]
    public void StartSpan_RecordException_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.RecordException(exception);
        span.End();
    }

    [Benchmark]
    public void StartSpan_SetStatus_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.SetStatus(Status.Ok);
        span.End();
    }

    [Benchmark]
    public void StartSpan_SetAttributes_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.SetAttribute("string", "value");
        span.SetAttribute("int", 42);
        span.SetAttribute("bool", true);
        span.SetAttribute("double", 3.14);
        span.End();
    }

    [Benchmark]
    public void StartSpan_UpdateName_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.UpdateName("updated");
        span.End();
    }
}
