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
public class TracerBenchmark
{
    private Tracer alwaysSampleTracer;
    private Setup.TracerBenchmarkSetup tracerBenchmarkSetup;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.tracerBenchmarkSetup = new Setup.TracerBenchmarkSetup();
        this.tracerBenchmarkSetup.GlobalSetup();

        this.alwaysSampleTracer = TracerProvider.Default.GetTracer("TracerBenchmark_AlwaysOnSample");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.tracerBenchmarkSetup.GlobalCleanup();
    }

    [Benchmark]
    public void StartSpan()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        span.End();
    }

    [Benchmark]
    public void StartActiveSpan()
    {
        using var span = this.alwaysSampleTracer.StartActiveSpan("operation");
        span.End();
    }

    [Benchmark]
    public void StartRootSpan()
    {
        using var span = this.alwaysSampleTracer.StartRootSpan("operation");
        span.End();
    }

    [Benchmark]
    public void StartSpan_GetCurrentSpan()
    {
        TelemetrySpan span;

        using (this.alwaysSampleTracer.StartSpan("operation"))
        {
            span = Tracer.CurrentSpan;
        }
    }

    [Benchmark]
    public void StartSpan_SetActive()
    {
        using var span = this.alwaysSampleTracer.StartSpan("operation");
        using (Tracer.WithSpan(span))
        {
        }

        span.End();
    }
}
