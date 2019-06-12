using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class CorrelationIdentifierTests
    {
        [Fact]
        public void Ids_NonzeroWithActiveSpan_ZeroWithNoActiveSpan()
        {
            var scope = Tracer.Instance.StartActive("ActiveSpan");
            var span = scope.Span;

            Assert.Equal<ulong>(span.SpanId, CorrelationIdentifier.SpanId);
            Assert.Equal<ulong>(span.TraceId, CorrelationIdentifier.TraceId);

            scope.Close();

            Assert.Equal<ulong>(0, CorrelationIdentifier.SpanId);
            Assert.Equal<ulong>(0, CorrelationIdentifier.TraceId);
        }
    }
}
