using System;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class TimeUtilsTests
    {
        [Fact]
        public void ToUnixTimeNanoseconds_UnixEpoch_Zero()
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(0);
            Assert.Equal(0, date.ToUnixTimeNanoseconds());
        }

        [Fact]
        public void ToUnixTimeNanoseconds_Now_CorrectMillisecondRoundedValue()
        {
            var date = DateTimeOffset.UtcNow;
            Assert.Equal(date.ToUnixTimeMilliseconds(), date.ToUnixTimeNanoseconds() / 1000000);
        }
    }
}
