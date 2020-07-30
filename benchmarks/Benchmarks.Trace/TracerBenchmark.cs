using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Tracer benchmarks
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    public class TracerBenchmark
    {
        /// <summary>
        /// Starts and finishes tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndSpan()
        {
            Tracer tracer = new Tracer();
            Span span = tracer.StartSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
            await tracer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts and finishes tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndSpanRecycle()
        {
            Tracer tracer = new Tracer();
            Span span = tracer.StartRecyclableSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
            await tracer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts and finishes an scope with tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndScope()
        {
            Tracer tracer = new Tracer();
            using (Scope scope = tracer.StartActive("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
            await tracer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts and finishes an scope with tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndScopeRecycle()
        {
            Tracer tracer = new Tracer();
            using (Scope scope = tracer.StartRecyclableActive("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
            await tracer.FlushAsync().ConfigureAwait(false);
        }
    }
}
