using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    [Collection(nameof(Datadog.Trace.Tests.Logging))]
    public class NullLogRateLimiterTests : IDisposable
    {
        private readonly NullLogRateLimiter _rateLimiter;

        public NullLogRateLimiterTests()
        {
            _rateLimiter = new NullLogRateLimiter();
        }

        public void Dispose() => Clock.Reset();

        [Fact]
        public void ShouldLog_AlwaysReturnsTrue()
        {
            const string filePath = @"C:\some\path";
            const int lineNo = 123;
            Clock.SetForCurrentThread(new ConstantClock());

            for (var i = 0; i < 10; i++)
            {
                var shouldLog = _rateLimiter.ShouldLog(filePath, lineNo, out var skipCount);

                Assert.True(shouldLog, $"{nameof(shouldLog)} was false on iteration {i}");
                Assert.Equal(0u, skipCount);
            }
        }

        private class ConstantClock : IClock
        {
            public DateTime UtcNow { get; } = DateTime.UtcNow;
        }
    }
}
