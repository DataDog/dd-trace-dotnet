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
[BenchmarkCategory(Constants.TracerCategory)]
public class TelemetrySpanBenchmark
{
    private static readonly Exception exception = new Exception("Error");
    private static readonly DateTimeOffset timestamp = DateTimeOffset.UtcNow;
    private Tracer alwaysSampleTracer;
    private Tracer neverSampleTracer;
    private Tracer noopTracer;
    private Setup.TelemetrySpanBenchmarkSetup telemetrySpanBenchmarkSetup;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.telemetrySpanBenchmarkSetup = new Setup.TelemetrySpanBenchmarkSetup();
        this.telemetrySpanBenchmarkSetup.GlobalSetup();

        this.alwaysSampleTracer = TracerProvider.Default.GetTracer("TelemetrySpanBenchmark_AlwaysOnSample");
        this.neverSampleTracer = TracerProvider.Default.GetTracer("TelemetrySpanBenchmark_AlwaysOffSample");
        this.noopTracer = TracerProvider.Default.GetTracer("TelemetrySpanBenchmark_Noop");
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
    public void StartSpan_AddEvent_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        span.AddEvent("event", timestamp);
        span.End();
    }

    [Benchmark]
    public void StartSpan_AddEvent_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
        span.AddEvent("event", timestamp);
        span.End();
    }

    [Benchmark]
    public bool StartSpan_GetContext_Sampled()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        var result = span.Context.IsValid;
        span.End();

        return result;
    }

    [Benchmark]
    public bool StartSpan_GetContext_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        var result = span.Context.IsValid;
        span.End();

        return result;
    }

    [Benchmark]
    public bool StartSpan_GetContext_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
        var result = span.Context.IsValid;
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
    public void StartSpan_RecordException_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        span.RecordException(exception);
        span.End();
    }

    [Benchmark]
    public void StartSpan_RecordException_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
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
    public void StartSpan_SetStatus_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        span.SetStatus(Status.Ok);
        span.End();
    }

    [Benchmark]
    public void StartSpan_SetStatus_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
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
    public void StartSpan_SetAttributes_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        span.SetAttribute("string", "value");
        span.SetAttribute("int", 42);
        span.SetAttribute("bool", true);
        span.SetAttribute("double", 3.14);
        span.End();
    }

    [Benchmark]
    public void StartSpan_SetAttributes_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
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

    [Benchmark]
    public void StartSpan_UpdateName_NotSampled()
    {
        using var span = this.neverSampleTracer.StartSpan("operation");
        span.UpdateName("updated");
        span.End();
    }

    [Benchmark]
    public void StartSpan_UpdateName_Noop()
    {
        using var span = this.noopTracer.StartSpan("operation");
        span.UpdateName("updated");
        span.End();
    }
}
