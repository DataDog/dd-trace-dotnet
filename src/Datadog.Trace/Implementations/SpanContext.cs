using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;

namespace Datadog.Trace
{
    internal class SpanContext : ISpanContext
    {
        private static ILog _log = LogProvider.For<SpanContext>();

        public SpanContext(ITraceContext traceContext, string serviceName)
        {
            // TODO:bertrand pool the random objects
            Random r = new Random();
            var parent = traceContext.GetCurrentSpanContext();
            if (parent != null)
            {
                Parent = parent;
                TraceId = parent.TraceId;
                TraceContext = traceContext;
                SpanId = r.NextUInt63();
                ServiceName = serviceName ?? parent.ServiceName ?? traceContext.DefaultServiceName;
            }
            else
            {
                TraceId = r.NextUInt63();
                SpanId = r.NextUInt63();
                TraceContext = traceContext;
                ServiceName = serviceName ?? traceContext.DefaultServiceName;
            }
        }

        public SpanContext(ITraceContext traceContext, ulong traceId, ulong spanId)
        {
            TraceContext = traceContext;
            TraceId = traceId;
            SpanId = spanId;
            ServiceName = traceContext.DefaultServiceName;
        }

        public SpanContext Parent { get; }

        public ulong TraceId { get; }

        public ulong? ParentId => Parent?.SpanId;

        public ulong SpanId { get; }

        public string ServiceName { get; }

        public ITraceContext TraceContext { get; }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            _log.Debug("SpanContext.GetBaggageItems is not implemented by Datadog.Trace");
            yield break;
        }
    }
}
