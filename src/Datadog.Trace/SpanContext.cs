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

        internal SpanContext(ISpanContext parent, ITraceContext traceContext)
        {
            TraceContext = traceContext;
            Parent = parent;
            SpanId = _random.Value.NextUInt63();

            TraceId = parent?.TraceId > 0
                          ? parent.TraceId
                          : _random.Value.NextUInt63();
        }

        /// <summary>
        /// Gets the parent context.
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
        internal ITraceContext TraceContext { get; }
    }
}
