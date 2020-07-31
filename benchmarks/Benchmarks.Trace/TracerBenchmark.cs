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
        /// 
        /// </summary>
        public TracerBenchmark()
        {
            RecyclableSpan.Return(RecyclableSpan.Get(null, null));
        }

        /// <summary>
        /// Starts and finishes an scope with tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndScope()
        {
            var tracer = Tracer.Instance;
            for (var f = 0; f < 5; f++)
            {
                for (var i = 0; i < 10; i++)
                {
                    using (Scope scope = tracer.StartActive("operation"))
                    {
                        scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
                    }
                }
                await tracer.FlushAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Starts and finishes an scope with tracer benchmark
        /// </summary>
        [Benchmark]
        public async Task StartFinishTracerAndScopeRecycle()
        {
            var tracer = Tracer.Instance;
            for (var f = 0; f < 5; f++)
            {
                for (var i = 0; i < 10; i++)
                {
                    using (Scope scope = tracer.StartRecyclableActive("operation"))
                    {
                        scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
                    }
                }
                await tracer.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
