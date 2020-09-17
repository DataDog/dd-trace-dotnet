using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [DatadogExporter]
    [MemoryDiagnoser]
    public class SpanBenchmark
    {
        private static readonly Tracer Tracer;

        public enum TagsTypes
        {
            Common
        }

        [Params(TagsTypes.Common)]
        public TagsTypes TagsType { get; set; }

        [Params(0, 1, 5)]
        public int NumberOfKeys { get; set; }

        private static string[] Keys = Enumerable.Range(1, 10).Select(i => i.ToString()).ToArray();

        static SpanBenchmark()
        {
            var settings = new TracerSettings
            {
                TraceEnabled = false,
                StartupDiagnosticLogEnabled = false
            };

            Tracer = new Tracer(settings, new DummyAgentWriter(), null, null, null);
        }

        ///// <summary>
        ///// Starts and finishes span benchmark
        ///// </summary>
        //[Benchmark]
        //public void StartFinishSpan()
        //{
        //    Span span = Tracer.StartSpan("operation", GetTagStorage());
        //    span.SetTraceSamplingPriority(SamplingPriority.UserReject);
        //    span.Finish();
        //}

        /// <summary>
        /// Starts and finishes span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishSpanWithTag()
        {
            Span span = Tracer.StartSpan("operation", GetTagStorage());
            // span.SetTraceSamplingPriority(SamplingPriority.UserReject);

            for (int i = 0; i < NumberOfKeys; i++)
            {
                span.SetTag(Keys[i], "world");
            }

            span.Finish();
        }

        [Benchmark]
        public void StartFinishSpanWithTagAndMetric()
        {
            Span span = Tracer.StartSpan("operation", GetTagStorage());
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);

            for (int i = 0; i < NumberOfKeys; i++)
            {
                span.SetTag(Keys[i], "world");
            }

            for (int i = 0; i < NumberOfKeys; i++)
            {
                span.SetMetric(Keys[i], 1.0);
            }

            span.Finish();
        }

        private ITags GetTagStorage()
        {
            if (TagsType == TagsTypes.Common)
            {
                return new CommonTags();
            }

            throw new InvalidOperationException();
        }

        ///// <summary>
        ///// Starts and finishes an scope with span benchmark
        ///// </summary>
        //[Benchmark]
        //public void StartFinishScope()
        //{
        //    using (Scope scope = Tracer.StartActive("operation"))
        //    {
        //        scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
        //    }
        //}
    }
}
