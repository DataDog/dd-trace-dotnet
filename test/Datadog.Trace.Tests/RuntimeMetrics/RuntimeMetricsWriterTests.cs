using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    public class RuntimeMetricsWriterTests
    {
        [Fact]
        public void PushEvents()
        {
            var listener = new Mock<IRuntimeMetricsListener>();

            var mutex = new ManualResetEventSlim();

            listener.Setup(l => l.Refresh())
                .Callback(() => mutex.Set());

            using (new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), 10, _ => listener.Object))
            {
                Assert.True(mutex.Wait(10000), "Method Refresh() wasn't called on the listener");
            }
        }

        [Fact]
        public void ShouldSwallowFactoryExceptions()
        {
            Func<IDogStatsd, IRuntimeMetricsListener> factory = _ => throw new InvalidOperationException("This exception should be caught");

            var writer = new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), 10, factory);
            writer.Dispose();
        }

        [Fact]
        public void ShouldCaptureFirstChanceExceptions()
        {
            var statsd = new Mock<IDogStatsd>();

            using (new RuntimeMetricsWriter(statsd.Object, Timeout.Infinite, _ => Mock.Of<IRuntimeMetricsListener>()))
            {
                try
                {
                    throw new CustomException();
                }
                catch
                {
                    // ignored
                }

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, 1, It.IsAny<double>(), new[] { "exception_type:CustomException" }),
                    Times.Once);
            }
        }

        [Fact]
        public void CleanupResources()
        {
            var statsd = new Mock<IDogStatsd>();
            var runtimeListener = new Mock<IRuntimeMetricsListener>();

            new RuntimeMetricsWriter(statsd.Object, Timeout.Infinite, _ => runtimeListener.Object).Dispose();

            runtimeListener.Verify(l => l.Dispose(), Times.Once);

            statsd.ResetCalls();

            try
            {
                throw new CustomException();
            }
            catch
            {
                // ignored
            }

            statsd.Verify(
                s => s.Increment(MetricsNames.ExceptionsCount, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<string[]>()),
                Times.Never);
        }

        private class CustomException : Exception
        {
        }
    }
}
