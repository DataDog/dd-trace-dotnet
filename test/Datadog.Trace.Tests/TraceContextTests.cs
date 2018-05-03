using System;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TraceContextTests
    {
        private Mock<IDatadogTracer> _tracerMock = new Mock<IDatadogTracer>();

        [Fact]
        public void UtcNow_GivesLegitTime()
        {
            var traceContext = new TraceContext(_tracerMock.Object);

            var now = traceContext.UtcNow();
            var expectedNow = DateTimeOffset.UtcNow;

            Assert.True(expectedNow.Subtract(now) < TimeSpan.FromMilliseconds(30));
        }

        [Fact]
        public void UtcNow_IsMonotonic()
        {
            var traceContext = new TraceContext(_tracerMock.Object);

            var t1 = traceContext.UtcNow();
            var t2 = traceContext.UtcNow();

            Assert.True(t2.Subtract(t1) > TimeSpan.Zero);
        }
    }
}
