using BenchmarkDotNet.Attributes;
using Benchmarks.OpenTelemetry.Api.Setup;
using OpenTelemetry.Trace;

namespace Benchmarks.OpenTelemetry.Api.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class TracerBenchmark
    {
        private Tracer alwaysSampleTracer;
        private Tracer noopTracer;
        private TracerBenchmarkSetup tracerBenchmarkSetup;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.tracerBenchmarkSetup = new TracerBenchmarkSetup();
            this.tracerBenchmarkSetup.GlobalSetup();

            this.alwaysSampleTracer = TracerProvider.Default.GetTracer("TracerBenchmark_AlwaysOnSample");
            this.noopTracer = TracerProvider.Default.GetTracer("TracerBenchmark_Noop");
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

        [Benchmark(Baseline = true)]
        public void StartSpan_Noop()
        {
            using var span = this.noopTracer.StartSpan("operation");
            span.End();
        }
    }
}
