using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    public class SpanContext
    {
        private static ILog _log = LogProvider.For<SpanContext>();

        internal SpanContext(IDatadogTracer tracer, SpanContext parent, string serviceName)
        {
            // TODO:bertrand pool the random objects
            Random r = new Random();
            if (parent != null)
            {
                Parent = parent;
                TraceId = parent.TraceId;
                TraceContext = parent.TraceContext;
            }
            else
            {
                TraceId = r.NextUInt63();
                TraceContext = new TraceContext(tracer);
            }

            SpanId = r.NextUInt63();
            ServiceName = serviceName ?? parent?.ServiceName ?? tracer.DefaultServiceName;
        }

        internal SpanContext(IDatadogTracer tracer, ulong traceId, ulong spanId)
        {
            TraceId = traceId;
            SpanId = spanId;
            ServiceName = tracer.DefaultServiceName;
            TraceContext = new TraceContext(tracer);
        }

        internal SpanContext(SpanContext spanContext)
        {
            TraceId = spanContext.TraceId;
            SpanId = spanContext.SpanId;
            ServiceName = spanContext.ServiceName;
            TraceContext = spanContext.TraceContext;
        }

        public SpanContext Parent { get; }

        public ulong TraceId { get; }

        public ulong? ParentId => Parent?.SpanId;

        public ulong SpanId { get; }

        public string ServiceName { get; }

        internal TraceContext TraceContext { get; }
    }
}
