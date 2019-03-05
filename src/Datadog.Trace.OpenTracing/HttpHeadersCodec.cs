using System;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        public OpenTracingSpanContext Extract(object carrier)
        {
            ITextMap map = carrier as ITextMap;
            if (map == null)
            {
                throw new NotSupportedException("Carrier should have type ITextMap");
            }

            string parentIdHeader = null;
            string traceIdHeader = null;
            string samplingPriorityHeader = null;

            foreach (var keyVal in map)
            {
                if (keyVal.Key.Equals(HttpHeaderNames.ParentId, StringComparison.OrdinalIgnoreCase))
                {
                    parentIdHeader = keyVal.Value;
                    break;
                }

                if (keyVal.Key.Equals(HttpHeaderNames.TraceId, StringComparison.OrdinalIgnoreCase))
                {
                    traceIdHeader = keyVal.Value;
                    break;
                }

                if (keyVal.Key.Equals(HttpHeaderNames.SamplingPriority, StringComparison.OrdinalIgnoreCase))
                {
                    samplingPriorityHeader = keyVal.Value;
                    break;
                }
            }

            ulong.TryParse(parentIdHeader, out var parentId);
            ulong.TryParse(traceIdHeader, out var traceId);

            var samplingPriority = int.TryParse(samplingPriorityHeader, out int samplingPriorityValue)
                                                     ? (SamplingPriority?)samplingPriorityValue
                                                     : null;

            SpanContext ddSpanContext = new SpanContext(traceId, parentId, samplingPriority);
            return new OpenTracingSpanContext(ddSpanContext);
        }

        public void Inject(OpenTracingSpanContext spanContext, object carrier)
        {
            ITextMap map = carrier as ITextMap;
            if (map == null)
            {
                throw new NotSupportedException("Carrier should have type ITextMap");
            }

            map.Set(HttpHeaderNames.ParentId, spanContext.Context.SpanId.ToString());
            map.Set(HttpHeaderNames.TraceId, spanContext.Context.TraceId.ToString());
        }
    }
}
