using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class StopwatchTests
    {
        [Fact]
        public void ComputesCorrectTimespan()
        {
            var sw = Stopwatch.StartNew();

            Thread.Sleep(50);

            sw.Stop();

            // Extract the internal ticks
            var stopwatchTicks = (long)typeof(Stopwatch)
                .GetMethod("GetRawElapsedTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sw, null);

            var elapsed = StopwatchHelpers.GetElapsed(stopwatchTicks);

            Assert.Equal(sw.Elapsed, elapsed);
        }
    }
}
