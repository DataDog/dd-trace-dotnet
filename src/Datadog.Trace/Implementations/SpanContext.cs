using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
    /// </summary>
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

        /// <summary>
        /// The SpanContext of the parent span (if any)
        /// </summary>
        public SpanContext Parent { get; }

        /// <summary>
        /// The trace id
        /// </summary>
        public ulong TraceId { get; }

        /// <summary>
        /// The span id of the parent span
        /// </summary>
        public ulong? ParentId => Parent?.SpanId;

        /// <summary>
        /// The span id
        /// </summary>
        public ulong SpanId { get; }

        internal string ServiceName { get; }

        internal TraceContext TraceContext { get; }
    }
}
