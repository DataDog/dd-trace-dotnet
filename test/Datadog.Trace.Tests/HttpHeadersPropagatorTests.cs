using System.Net;
using System.Net.Http;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Xunit;

namespace Datadog.Trace.Tests
{
    // TODO: for now, these tests cover all of this,
    // but we should probably split them up into actual *unit* tests for:
    // - HttpHeadersCollection wrapper over HttpHeaders (Get, Set, Add, Remove)
    // - NameValueHeadersCollection wrapper over NameValueCollection (Get, Set, Add, Remove)
    // - IHeadersCollection.InjectSpanContext() extension method
    // - IHeadersCollection.ExtractSpanContext() extension method
    public class HttpHeadersPropagatorTests
    {
        [Fact]
        public void HttpRequestMessage_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            IHeadersCollection headers = new HttpRequestMessage().Headers.Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority);

            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
        }

        [Fact]
        public void HttpRequestMessage_InjectExtract_NoContext()
        {
            const int traceId = 0;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            IHeadersCollection headers = new HttpRequestMessage().Headers.Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority);

            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            // traceId 0 should return a null context even if other values are set
            Assert.Null(resultContext);
        }

        [Fact]
        public void WebRequest_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority);

            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
        }

        [Fact]
        public void WebRequest_InjectExtract_NoContext()
        {
            const int traceId = 0;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority);

            headers.InjectSpanContext(context);
            var resultContext = headers.ExtractSpanContext();

            // traceId 0 should return a null context even if other values are set
            Assert.Null(resultContext);
        }
    }
}
