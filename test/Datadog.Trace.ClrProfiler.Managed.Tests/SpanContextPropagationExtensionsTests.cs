using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Extensions;
using Datadog.Trace.Headers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SpanContextPropagationExtensionsTests
    {
        [Fact]
        public void HttpRequestMessage_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            var request = new HttpRequestMessage();
            var context = new SpanContext(traceId, spanId, samplingPriority);

            SpanContextPropagator.Instance.InjectHttpHeadersWithReflection(context, (object)request.Headers);
            var resultContext = SpanContextPropagator.Instance.ExtractHttpHeadersWithReflection(request.Headers);

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("trace.id")]
        public void Extract_InvalidTraceId(string traceId)
        {
            const string spanId = "7";
            const string samplingPriority = "2";

            var request = new HttpRequestMessage();
            InjectContext(request.Headers, traceId, spanId, samplingPriority);
            var resultContext = SpanContextPropagator.Instance.ExtractHttpHeadersWithReflection(request.Headers);

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

            var request = new HttpRequestMessage();
            InjectContext(
                request.Headers,
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId,
                ((int)samplingPriority).ToString(CultureInfo.InvariantCulture));

            var resultContext = SpanContextPropagator.Instance.ExtractHttpHeadersWithReflection(request.Headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(default(ulong), resultContext.SpanId);
            Assert.Equal(samplingPriority, resultContext.SamplingPriority);
        }

        [Theory]
        [InlineData("-2")]
        [InlineData("3")]
        [InlineData("sampling.priority")]
        public void Extract_InvalidSamplingPriority(string samplingPriority)
        {
            const ulong traceId = 9;
            const ulong spanId = 7;

            var request = new HttpRequestMessage();
            InjectContext(
                request.Headers,
                traceId.ToString(CultureInfo.InvariantCulture),
                spanId.ToString(CultureInfo.InvariantCulture),
                samplingPriority);

            var resultContext = SpanContextPropagator.Instance.ExtractHttpHeadersWithReflection(request.Headers);

            Assert.NotNull(resultContext);
            Assert.Equal(traceId, resultContext.TraceId);
            Assert.Equal(spanId, resultContext.SpanId);
            Assert.Null(resultContext.SamplingPriority);
        }

        private static void InjectContext(HttpRequestHeaders headers, string traceId, string spanId, string samplingPriority)
        {
            headers.Add(HttpHeaderNames.TraceId, traceId);
            headers.Add(HttpHeaderNames.ParentId, spanId);
            headers.Add(HttpHeaderNames.SamplingPriority, samplingPriority);
        }
    }
}
