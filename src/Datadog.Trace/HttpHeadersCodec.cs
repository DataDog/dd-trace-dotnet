using OpenTracing;
using OpenTracing.Propagation;
using System;

namespace Datadog.Trace
{
    class HttpHeadersCodec : ICodec
    {
        public IDatadogTracer _tracer;

        public HttpHeadersCodec(IDatadogTracer tracer)
        {
            _tracer = tracer;
        }

        public SpanContext Extract(object carrier)
        {
            ITextMap map = carrier as ITextMap;
            if(map == null)
            {
                throw new UnsupportedFormatException("Carrier should have type ITextMap");
            }
            var parentIdHeader = map.Get(Constants.HttpHeaderParentId);
            var traceIdHeader = map.Get(Constants.HttpHeaderTraceId);
            if(parentIdHeader == null)
            {
                throw new ArgumentException($"{Constants.HttpHeaderParentId} should be set.");
            }
            if(traceIdHeader == null)
            {
                throw new ArgumentException($"{Constants.HttpHeaderTraceId} should be set.");
            }
            ulong parentId;
            try
            {
                parentId = Convert.ToUInt64(parentIdHeader);
            }
            catch (FormatException)
            {
                throw new FormatException($"{Constants.HttpHeaderParentId} should contain an unsigned integer value");
            }
            ulong traceId;
            try
            {
                traceId = Convert.ToUInt64(traceIdHeader);
            }
            catch (FormatException)
            {
                throw new FormatException($"{Constants.HttpHeaderTraceId} should contain an unsigned integer value");
            }
            return new SpanContext(_tracer.GetTraceContext(), traceId, parentId);
        }

        public void Inject(SpanContext spanContext, object carrier)
        {
            ITextMap map = carrier as ITextMap;
            if(map == null)
            {
                throw new UnsupportedFormatException("Carrier should have type ITextMap");
            }
            map.Set(Constants.HttpHeaderParentId, spanContext.SpanId.ToString());
            map.Set(Constants.HttpHeaderTraceId, spanContext.TraceId.ToString());
        }
    }
}
