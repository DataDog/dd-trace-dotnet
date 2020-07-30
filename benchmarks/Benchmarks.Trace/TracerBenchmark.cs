using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Datadog.Trace;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.ExtensionMethods;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Tracer benchmarks
    /// </summary>
    [DatadogExporter]
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    public class TracerBenchmark
    {
        static Action<Tracer> _runShutdownTasks;

        static TracerBenchmark()
        {
            _runShutdownTasks = (Action<Tracer>)typeof(Tracer)
                .GetMethod("RunShutdownTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .CreateDelegate(typeof(Action<Tracer>));
        }

        /// <summary>
        /// Starts and finishes tracer benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishTracerAndSpan()
        {
            Tracer tracer = new Tracer();
            Span span = tracer.StartSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
            _runShutdownTasks(tracer);
        }

        /// <summary>
        /// Starts and finishes an scope with tracer benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishTracerAndScope()
        {
            Tracer tracer = new Tracer();
            using (Scope scope = tracer.StartActive("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
            _runShutdownTasks(tracer);
        }
    }
}
