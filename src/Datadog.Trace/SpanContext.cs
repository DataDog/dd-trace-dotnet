using Datadog.Trace.Logging;
using OpenTracing;
using System;
using System.Collections.Generic;

namespace Datadog.Trace
{
    internal class SpanContext : ISpanContext
    {
        private static ILog _log = LogProvider.For<SpanBuilder>();

        public SpanContext Parent { get; }

        public UInt64 TraceId { get; }

        public UInt64? ParentId { get { return Parent?.SpanId; } }

        public UInt64 SpanId { get; }

        public string ServiceName { get; }

        public ITraceContext TraceContext { get; }

        public SpanContext(ITraceContext traceContext, string serviceName)
        {
            ServiceName = serviceName;
            // TODO:bertrand pool the random objects
            Random r = new Random();
            var parent = traceContext.GetCurrentSpanContext();
            if (parent != null)
            {
                Parent = parent;
                TraceId = parent.TraceId;
                ServiceName = parent.ServiceName;
                TraceContext = parent.TraceContext;
                SpanId = r.NextUInt63();
        }
            else
            {
                TraceId = r.NextUInt63();
                SpanId = r.NextUInt63();
                TraceContext = traceContext;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            _log.Debug("SpanContext.GetBaggageItems is not implemented by Datadog.Trace");
            yield break;
        }
    }
}
