using System;
using System.Globalization;
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
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            string parentIdHeader = null;
            string traceIdHeader = null;
            string samplingPriorityHeader = null;

            foreach (var keyVal in map)
            {
                if (keyVal.Key.Equals(HttpHeaderNames.ParentId, StringComparison.OrdinalIgnoreCase))
                {
                    parentIdHeader = keyVal.Value;
                }

                if (keyVal.Key.Equals(HttpHeaderNames.TraceId, StringComparison.OrdinalIgnoreCase))
                {
                    traceIdHeader = keyVal.Value;
                }

                if (keyVal.Key.Equals(HttpHeaderNames.SamplingPriority, StringComparison.OrdinalIgnoreCase))
                {
                    samplingPriorityHeader = keyVal.Value;
                }
            }

            if (!ulong.TryParse(traceIdHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var traceId) ||
                traceId == 0)
            {
                // if traceId is not provided or is zero, there is no span context
                return null;
            }

            ulong.TryParse(parentIdHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId);

            SpanContext ddSpanContext = new SpanContext(traceId, parentId);
            return new OpenTracingSpanContext(ddSpanContext);
        }

        public void Inject(OpenTracingSpanContext otSpanContext, object carrier)
        {
            ITextMap map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            var spanContext = otSpanContext.Context;
            var traceContext = spanContext.TraceContext;

            map.Set(HttpHeaderNames.ParentId, spanContext.SpanId.ToString(CultureInfo.InvariantCulture));
            map.Set(HttpHeaderNames.TraceId, spanContext.TraceId.ToString(CultureInfo.InvariantCulture));

            if (traceContext.SamplingPriority != null)
            {
                var samplingPriority = (int)traceContext.SamplingPriority;
                map.Set(HttpHeaderNames.SamplingPriority, samplingPriority.ToString(CultureInfo.InvariantCulture));
            }

            // lock sampling priority when span propagates.
            // if sampling priority is not set yet, this will determine
            // a value using a Sampler.
            traceContext.LockSamplingPriority();
        }
    }
}
