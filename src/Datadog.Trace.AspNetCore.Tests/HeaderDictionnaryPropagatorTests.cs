using Microsoft.AspNetCore.Http;
using Xunit;

namespace Datadog.Trace.AspNetCore.Tests
{
    public class HeaderDictionnaryPropagatorTests
    {
        [Fact]
        public void InjectExtract_Identity()
        {
            const int traceId = 9;
            const int spanId = 7;
            var context = new SpanContext(traceId, spanId);
            var headers = new HeaderDictionary();

            headers.Inject(context);

            var resultContext = headers.Extract();

            Assert.Equal(context.SpanId, resultContext.SpanId);
            Assert.Equal(context.TraceId, resultContext.TraceId);
        }
    }
}
