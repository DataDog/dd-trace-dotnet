// <copyright file="RuntimeMetricsWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    public unsafe class RuntimeMetricsWriterTests
    {
        [Fact]
        public void PushEvents()
        {
            IRuntimeMetricsListenerFunctionPointerListener.Reset();
            var listener = IRuntimeMetricsListenerFunctionPointerListener.Listener;

            var mutex = new ManualResetEventSlim();

            listener.Setup(l => l.Refresh())
                .Callback(() => mutex.Set());

            using (new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), &IRuntimeMetricsListenerFunctionPointerListener.InitializeListener))
            {
                Assert.True(mutex.Wait(10000), "Method Refresh() wasn't called on the listener");
            }
        }

        [Fact]
        public void ShouldSwallowFactoryExceptions()
        {
            var writer = new RuntimeMetricsWriter(Mock.Of<IDogStatsd>(), TimeSpan.FromMilliseconds(10), &InitializeListener);
            writer.Dispose();

            static IRuntimeMetricsListener InitializeListener(IDogStatsd statsd, TimeSpan timeSpan)
                => throw new InvalidOperationException("This exception should be caught");
        }

        [Fact]
        public void ShouldCaptureFirstChanceExceptions()
        {
            var statsd = new Mock<IDogStatsd>();
            IRuntimeMetricsListenerFunctionPointerListener.Reset();

            using (var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), &IRuntimeMetricsListenerFunctionPointerListener.InitializeListener))
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

        [Fact]
        public void CleanupResources()
        {
            var statsd = new Mock<IDogStatsd>();
            IRuntimeMetricsListenerFunctionPointerListener.Reset();
            var runtimeListener = IRuntimeMetricsListenerFunctionPointerListener.Listener;

            var writer = new RuntimeMetricsWriter(statsd.Object, TimeSpan.FromMilliseconds(Timeout.Infinite), &IRuntimeMetricsListenerFunctionPointerListener.InitializeListener);
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

#pragma warning disable SA1204 // Static elements should appear before instance elements
        private static class IRuntimeMetricsListenerFunctionPointerListener
        {
            public static Mock<IRuntimeMetricsListener> Listener { get; private set; } = new Mock<IRuntimeMetricsListener>();

            public static IRuntimeMetricsListener InitializeListener(IDogStatsd statsd, TimeSpan timeSpan)
            {
                return Listener.Object;
            }

            public static void Reset()
            {
                Listener = new Mock<IRuntimeMetricsListener>();
            }
        }
    }
}
