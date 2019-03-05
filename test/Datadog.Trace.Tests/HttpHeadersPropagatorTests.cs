using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class HttpHeadersPropagatorTests
    {
        [Fact]
        public void HttpRequestMessage_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            HttpRequestHeaders httpHeaders = new HttpRequestMessage().Headers;
            var context = new SpanContext(traceId, spanId, samplingPriority);

            IHeadersCollection headers = httpHeaders.Wrap();
            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
        }

        [Fact]
        public void WebRequest_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            WebHeaderCollection webHeaders = WebRequest.CreateHttp("http://localhost").Headers;
            var context = new SpanContext(traceId, spanId, samplingPriority);

            IHeadersCollection headers = webHeaders.Wrap();
            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
        }
    }
}
