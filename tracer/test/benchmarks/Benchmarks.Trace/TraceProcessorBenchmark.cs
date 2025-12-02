using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Processors;

namespace Benchmarks.Trace
{
    // This benchmark is currently disabled in CI (Not assigned to an agent)
    [MemoryDiagnoser]
    public class TraceProcessorBenchmark
    {
        private ITraceProcessor _normalizerTraceProcessor;
        private ITraceProcessor _trucantorTraceProcessor;
        private ITraceProcessor _obfuscatorTraceProcessor;
        private ArraySegment<Span> _spans;
        private Tracer _tracer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _normalizerTraceProcessor = new NormalizerTraceProcessor();
            _trucantorTraceProcessor = new TruncatorTraceProcessor();
            _obfuscatorTraceProcessor = new ObfuscatorTraceProcessor(true);

            _tracer = TracerHelper.CreateTracer();
            var traceContext = new TraceContext(_tracer, null);
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
            var span = new Span(spanContext, DateTimeOffset.Now);
            span.ResourceName = "My Resource Name";
            span.Type = "sql";
            _spans = new ArraySegment<Span>(Enumerable.Repeat(span, 100).ToArray());
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _tracer.TracerManager.ShutdownAsync().GetAwaiter().GetResult();
        }

        [Benchmark]
        public void NormalizerProcessor()
        {
            _normalizerTraceProcessor.Process(_spans);
        }
        
        [Benchmark]
        public void TruncatorProcessor()
        {
            _trucantorTraceProcessor.Process(_spans);
        }
        
        [Benchmark]
        public void ObfuscatorProcessor()
        {
            _obfuscatorTraceProcessor.Process(_spans);
        }
        
        [Benchmark]
        public void NormalizeTag()
        {
            TraceUtil.NormalizeTag("THIS IS A NORMAL tAG to Normalize");
        }
    }
}
