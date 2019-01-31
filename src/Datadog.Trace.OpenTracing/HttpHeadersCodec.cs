using System;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        public HttpHeadersCodec()
        {
        }

        public OpenTracingSpanContext Extract(object carrier)
        {
            ITextMap map = carrier as ITextMap;
            if (map == null)
            {
                throw new NotSupportedException("Carrier should have type ITextMap");
            }

            string parentIdHeader = null;
            string traceIdHeader = null;
            foreach (var keyVal in map)
            {
                if (keyVal.Key.Equals(HttpHeaderNames.ParentId, StringComparison.InvariantCultureIgnoreCase))
                {
                    parentIdHeader = keyVal.Value;
                }

                if (keyVal.Key.Equals(HttpHeaderNames.TraceId, StringComparison.InvariantCultureIgnoreCase))
                {
                    traceIdHeader = keyVal.Value;
                }
            }

            if (parentIdHeader == null)
            {
                throw new ArgumentException($"{HttpHeaderNames.ParentId} should be set.");
            }

            if (traceIdHeader == null)
            {
                throw new ArgumentException($"{HttpHeaderNames.TraceId} should be set.");
            }

            ulong parentId;
            try
            {
                parentId = Convert.ToUInt64(parentIdHeader);
            }
            catch (FormatException)
            {
                throw new FormatException($"{HttpHeaderNames.ParentId} should contain an unsigned integer value");
            }

            ulong traceId;
            try
            {
                traceId = Convert.ToUInt64(traceIdHeader);
            }
            catch (FormatException)
            {
                throw new FormatException($"{HttpHeaderNames.TraceId} should contain an unsigned integer value");
            }

            SpanContext ddSpanContext = new SpanContext(traceId, parentId);
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
