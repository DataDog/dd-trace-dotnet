using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Processors;

namespace Benchmarks.Trace
{
    // This benchmark is currently disabled in CI (Not assigned to an agent)
    [MemoryDiagnoser]
    public class TraceProcessorBenchmark
    {
        private readonly ITraceProcessor _normalizerTraceProcessor;
        private readonly ITraceProcessor _trucantorTraceProcessor;
        private readonly ITraceProcessor _obfuscatorTraceProcessor;
        private SpanCollection _spans;

        public TraceProcessorBenchmark()
        {
            _normalizerTraceProcessor = new NormalizerTraceProcessor();
            _trucantorTraceProcessor = new TruncatorTraceProcessor();
            _obfuscatorTraceProcessor = new ObfuscatorTraceProcessor(true);
            
            var traceContext = new TraceContext(Tracer.Instance, null);
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
            var span = new Span(spanContext, DateTimeOffset.Now);
            span.ResourceName = "My Resource Name";
            span.Type = "sql";
            _spans = new SpanCollection(Enumerable.Repeat(span, 100).ToArray(), 100);
        }

        [Benchmark]
        public void NormalizerProcessor()
        {
            _normalizerTraceProcessor.Process(in _spans);
        }
        
        [Benchmark]
        public void TruncatorProcessor()
        {
            _trucantorTraceProcessor.Process(in _spans);
        }
        
        [Benchmark]
        public void ObfuscatorProcessor()
        {
            _obfuscatorTraceProcessor.Process(in _spans);
        }
        
        [Benchmark]
        public void NormalizeTag()
        {
            TraceUtil.NormalizeTag("THIS IS A NORMAL tAG to Normalize");
        }
    }
}
