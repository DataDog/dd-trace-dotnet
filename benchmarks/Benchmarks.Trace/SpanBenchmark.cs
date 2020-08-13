using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [MemoryDiagnoser]
    public class SpanBenchmark
    {
        private static Tracer _tracer;

        static SpanBenchmark()
        {
            _tracer = new Tracer(null, new DummyAgentWriter(), null, null, null);
        }

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
    }
}
