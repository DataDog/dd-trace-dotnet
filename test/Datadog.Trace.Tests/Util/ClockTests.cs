using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    [CollectionDefinition(nameof(ClockTests), DisableParallelization = true)]
    [Collection(nameof(ClockTests))]
    public class ClockTests
    {
        [Fact]
        public void Should_use_real_clock_if_not_overriden()
        {
            // If everything works, the fastpath should be used and this clock ignored
            using var lease = Clock.SetForCurrentThread(new SimpleClock());

            // Reset the override flag
            Clock.Reset();

            var now = Clock.UtcNow;

            Thread.Sleep(100);

            var then = Clock.UtcNow;

            Assert.NotEqual(now, then);
        }
    }
}
