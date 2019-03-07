using System;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    /// The SpanContext contains all the information needed to express relationships between spans inside or outside the process boundaries.
    /// </summary>
    public class SpanContext : ISpanContext
    {
        private static ILog _log = LogProvider.For<SpanContext>();
        private static ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

        internal SpanContext(IDatadogTracer tracer, ISpanContext parent)
            : this(GetTraceContext(tracer, parent), parent)
        {
        }

        internal SpanContext(TraceContext traceContext, ISpanContext parent)
        {
            TraceContext = traceContext;
            Parent = parent;

            TraceId = parent?.TraceId > 0
                          ? parent.TraceId
                          : _random.Value.NextUInt63();

            SpanId = _random.Value.NextUInt63();
        }

        /// <summary>
        /// Gets the SpanContext of the parent span (if any)
        /// </summary>
        public ISpanContext Parent { get; }

        /// <summary>
        /// Gets the trace id
        /// </summary>
        public ulong TraceId { get; }

        /// <summary>
        /// Gets the span id of the parent span
        /// </summary>
        public ulong? ParentId => Parent?.SpanId;

        /// <summary>
        /// Gets the span id
        /// </summary>
        public ulong SpanId { get; }

        // This may be null if SpanContext was extracted from another process context
        internal TraceContext TraceContext { get; }

        internal static TraceContext GetTraceContext(IDatadogTracer tracer, ISpanContext parent)
        {
            TraceContext traceContext;

            switch (parent)
            {
                case SpanContext context:
                    traceContext = context.TraceContext ?? new TraceContext(tracer);
                    break;
                case PropagationContext propagatedContext:
                    traceContext = new TraceContext(tracer)
                    {
                        SamplingPriority = propagatedContext.SamplingPriority
                    };
                    break;
                default:
                    throw new ArgumentException("Type of parent is not a supported.", nameof(parent));
            }

            return traceContext;
        }
    }
}
