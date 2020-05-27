using System;
using Datadog.Trace.Sampling;

namespace Datadog.Trace
{
    internal class TraceContextStrategy : ITraceContextStrategy
    {
        private readonly IDatadogTracer _tracer;
        private readonly ISampler _sampler;

        public TraceContextStrategy(IDatadogTracer tracer, ISampler sampler)
        {
            _tracer = tracer;
            _sampler = sampler;
        }

        public void Write(Span[] span) => _tracer.Write(span);

        public SamplingPriority GetSamplingPriority(Span span) => _sampler.GetSamplingPriority(span);
    }
}
