using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
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
    // - SpanContextPropagator.Inject()
    // - SpanContextPropagator.Extract()
    public class HeadersCollectionTests
    {
        [Fact]
        public void WebRequest_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            SpanContextPropagator.Instance.Inject(context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
            Assert.Equal(context.Origin, resultContext.Origin);
        }

        [Fact]
        public void NameValueCollection_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            IHeadersCollection headers = new NameValueCollection().Wrap();
            var context = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            SpanContextPropagator.Instance.Inject(context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
            Assert.Equal(context.Origin, resultContext.Origin);
        }

        [Fact]
        public void DictionaryHeadersCollection_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            IHeadersCollection headers = new DictionaryHeadersCollection();
            var context = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            SpanContextPropagator.Instance.Inject(context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
            Assert.Equal(context.Origin, resultContext.Origin);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("trace.id")]
        public void Extract_InvalidTraceId(string traceId)
        {
            const string spanId = "7";
            const string samplingPriority = "2";
            const string origin = "synthetics";

            var headers = InjectContext(traceId, spanId, samplingPriority, origin);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            // invalid traceId should return a null context even if other values are set
            Assert.Null(resultContext);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("span.id")]
        public void Extract_InvalidSpanId(string spanId)
        {
            const ulong traceId = 9;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var headers = InjectContext(
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId,
                ((int)samplingPriority).ToString(CultureInfo.InvariantCulture),
                origin);

            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(default(ulong), resultContext.SpanId);
            Assert.Equal(samplingPriority, resultContext.SamplingPriority);
            Assert.Equal(origin, resultContext.Origin);
        }

        [Theory]
        [InlineData("-2")]
        [InlineData("3")]
        [InlineData("sampling.priority")]
        public void Extract_InvalidSamplingPriority(string samplingPriority)
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const string origin = "synthetics";

            var headers = InjectContext(
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId.ToString(CultureInfo.InvariantCulture),
                samplingPriority,
                origin);

            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(spanId, resultContext.SpanId);
            Assert.Null(resultContext.SamplingPriority);
            Assert.Equal(origin, resultContext.Origin);
        }

        private static IHeadersCollection InjectContext(string traceId, string spanId, string samplingPriority, string origin)
        {
            IHeadersCollection headers = new DictionaryHeadersCollection();
            headers.Add(HttpHeaderNames.TraceId, traceId);
            headers.Add(HttpHeaderNames.ParentId, spanId);
            headers.Add(HttpHeaderNames.SamplingPriority, samplingPriority);
            headers.Add(HttpHeaderNames.Origin, origin);
            return headers;
        }
    }
}
