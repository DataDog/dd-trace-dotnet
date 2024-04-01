// <copyright file="RuntimeMetricsWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
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

            using (new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), false, (statsd, timeSpan, inAppContext) => listener.Object))
            {
                Assert.True(mutex.Wait(10000), "Method Refresh() wasn't called on the listener");
            }
        }

        [Fact]
        public void ShouldSwallowFactoryExceptions()
        {
            var writer = new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), false, (statsd, timeSpan, inAppContext) => throw new InvalidOperationException("This exception should be caught"));
            writer.Dispose();
        }

        [Fact]
        public void ShouldCaptureFirstChanceExceptions()
        {
            var statsd = new Mock<IDogStatsd>();
            var listener = new Mock<IRuntimeMetricsListener>();

            using (var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), false, (statsd, timeSpan, inAppContext) => listener.Object))
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

                statsd.Invocations.Clear();

                // Make sure stats are reset when pushed
                writer.PushEvents();

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, It.IsAny<int>(), It.IsAny<double>(), new[] { "exception_type:CustomException1" }),
                    Times.Never);

                statsd.Verify(
                    s => s.Increment(MetricsNames.ExceptionsCount, It.IsAny<int>(), It.IsAny<double>(), new[] { "exception_type:CustomException2" }),
                    Times.Never);
            }
        }

        [SkippableFact]
        public async Task ShouldCaptureProcessMetrics()
        {
            // This test is specifically targeting process metrics collected with PSS
            SkipOn.Platform(SkipOn.PlatformValue.Linux);
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var statsd = new Mock<IDogStatsd>();
            var listener = new Mock<IRuntimeMetricsListener>();

            using (new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromSeconds(1), false, (_, _, _) => listener.Object))
            {
                var expectedNumberOfThreads = Process.GetCurrentProcess().Threads.Count;

                var tcs = new TaskCompletionSource<bool>();

                double? actualNumberOfThreads = null;
                double? userCpuTime = null;
                double? kernelCpuTime = null;
                double? memoryUsage = null;

                statsd.Setup(s => s.Gauge(MetricsNames.ThreadsCount, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string[]>()))
                    .Callback<string, double, double, string[]>((_, value, _, _) => actualNumberOfThreads = value);

                statsd.Setup(s => s.Gauge(MetricsNames.CommittedMemory, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string[]>()))
                      .Callback<string, double, double, string[]>((_, value, _, _) => memoryUsage = value);

                statsd.Setup(s => s.Gauge(MetricsNames.CpuUserTime, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string[]>()))
                      .Callback<string, double, double, string[]>((_, value, _, _) => userCpuTime = value);

                statsd.Setup(s => s.Gauge(MetricsNames.CpuSystemTime, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string[]>()))
                      .Callback<string, double, double, string[]>((_, value, _, _) => kernelCpuTime = value);

                // CPU percentage is the last pushed event
                statsd.Setup(s => s.Gauge(MetricsNames.CpuPercentage, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string[]>()))
                      .Callback<string, double, double, string[]>((_, _, _, _) => tcs.TrySetResult(true));

                // Spin a bit to eat CPU
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (sw.Elapsed < TimeSpan.FromMilliseconds(50))
                {
                    Thread.SpinWait(10);
                }

                var timeout = Task.Delay(TimeSpan.FromSeconds(30));

                await Task.WhenAny(tcs.Task, timeout);

                tcs.Task.IsCompleted.Should().BeTrue();

                actualNumberOfThreads.Should().NotBeNull();

                // A margin of 500 threads seem like a lot, but we have tests that spawn a large number of threads to try to find race conditions
                actualNumberOfThreads.Should().NotBeNull().And.BeGreaterThan(0).And.BeInRange(expectedNumberOfThreads - 500, expectedNumberOfThreads + 500);

                // CPU time and memory usage can vary wildly, so don't try too hard to validate
                userCpuTime.Should().NotBeNull().And.BeGreaterThan(0);

                // Unfortunately we can't guarantee that the process will be eating kernel time, so greater or equal
                kernelCpuTime.Should().NotBeNull().And.BeGreaterThanOrEqualTo(0);

                // Between 10MB and 100GB seems realistic.
                // If in the future the tests runner really get below 10MB, congratulations!
                // If it gets above 100GB, God save us all.
                memoryUsage.Should().NotBeNull().And.BeInRange(10.0 * 1024 * 1024, 100.0 * 1024 * 1024 * 1024);
            }
        }

        [Fact]
        public void CleanupResources()
        {
            var statsd = new Mock<IDogStatsd>();
            var listener = new Mock<IRuntimeMetricsListener>();

            var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), false, (statsd, timeSpan, inAppContext) => listener.Object);
            writer.Dispose();

            listener.Verify(l => l.Dispose(), Times.Once);

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
