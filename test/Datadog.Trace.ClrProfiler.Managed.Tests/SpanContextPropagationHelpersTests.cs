using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Headers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SpanContextPropagationHelpersTests
    {
        [Fact]
        public void HttpRequestMessage_InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var request = new HttpRequestMessage();
            var context = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            SpanContextPropagator.Instance.Inject(context, new ReflectionHttpHeadersCollection(request.Headers));
            var resultContext = SpanContextPropagator.Instance.Extract(new ReflectionHttpHeadersCollection(request.Headers));

            Assert.NotNull(resultContext);
            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
            Assert.Equal(context.SamplingPriority, resultContext.SamplingPriority);
            Assert.Equal(context.Origin, resultContext.Origin);
        }
    }
}
