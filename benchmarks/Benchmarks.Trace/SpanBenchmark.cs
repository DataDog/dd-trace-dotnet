using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    public class SpanBenchmark
    {
        private static Tracer _tracer = Tracer.Instance;

        /// <summary>
        /// Starts and finishes span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishSpan()
        {
            Span span = _tracer.StartSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
        }

        /// <summary>
        /// Starts and finishes span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishSpanRecycle()
        {
            Span span = _tracer.StartRecyclableSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
        }

        /// <summary>
        /// Starts and finishes an scope with span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishScope()
        {
            using (Scope scope = _tracer.StartActive("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
        }

        /// <summary>
        /// Starts and finishes an scope with span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishScopeRecycle()
        {
            using (Scope scope = _tracer.StartRecyclableActive("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
        }
    }
}
