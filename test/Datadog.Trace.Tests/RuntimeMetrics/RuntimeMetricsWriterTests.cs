using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.RuntimeMetrics
{
    [CollectionDefinition(nameof(RuntimeMetricsWriterTests), DisableParallelization = true)]
    [Collection(nameof(RuntimeMetricsWriterTests))]
    public class RuntimeMetricsWriterTests
    {
        [Fact]
        public void PushEvents()
        {
            var listener = new Mock<IRuntimeMetricsListener>();

            var mutex = new ManualResetEventSlim();

            listener.Setup(l => l.Refresh())
                .Callback(() => mutex.Set());

            using (new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), (_, d) => listener.Object))
            {
                Assert.True(mutex.Wait(10000), "Method Refresh() wasn't called on the listener");
            }
        }

        [Fact]
        public void ShouldSwallowFactoryExceptions()
        {
            Func<IDogStatsd, TimeSpan, IRuntimeMetricsListener> factory = (_, d) => throw new InvalidOperationException("This exception should be caught");

            var writer = new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), factory);
            writer.Dispose();
        }

        [Fact]
        public void ShouldCaptureFirstChanceExceptions()
        {
            var statsd = new Mock<IDogStatsd>();

            using (var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), (_, d) => Mock.Of<IRuntimeMetricsListener>()))
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        throw new CustomException1();
                    }
                    catch
                    {
                        // ignored
                    }

                    if (i % 2 == 0)
                    {
                        try
                        {
                            throw new CustomException2();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<string[]>()),
                    Times.Never);

                writer.PushEvents();

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, 10, It.IsAny<double>(), new[] { "exception_type:CustomException1" }),
                    Times.Once);

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, 5, It.IsAny<double>(), new[] { "exception_type:CustomException2" }),
                    Times.Once);

                statsd.ResetCalls();

                // Make sure stats are reset when pushed
                writer.PushEvents();

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<string[]>()),
                    Times.Never);
            }
        }

        [Fact]
        public void CleanupResources()
        {
            var statsd = new Mock<IDogStatsd>();
            var runtimeListener = new Mock<IRuntimeMetricsListener>();

            var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), (_, d) => runtimeListener.Object);
            writer.Dispose();

            runtimeListener.Verify(l => l.Dispose(), Times.Once);

            // Make sure that the writer unsubscribed from the global exception handler
            try
            {
                throw new CustomException1();
            }
            catch
            {
                // ignored
            }

            writer.ExceptionCounts.TryGetValue(nameof(CustomException1), out var count);

            Assert.Equal(0, count);
        }

        private class CustomException1 : Exception
        {
        }

        private class CustomException2 : Exception
        {
        }
    }
}
