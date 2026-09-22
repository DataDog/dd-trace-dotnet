// <copyright file="ConsoleControlHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Threading;
using Datadog.Trace.PlatformHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class ConsoleControlHandlerTests
    {
        private const int CtrlCEvent = 0;
        private const int CtrlBreakEvent = 1;

        private static readonly TimeSpan ThirtySeconds = TimeSpan.FromSeconds(30);

        [Theory]
        [InlineData(CtrlCEvent)]
        [InlineData(CtrlBreakEvent)]
        public void InvokesTheCallbackForCtrlCAndCtrlBreak(int controlType)
        {
            var invocations = 0;
            var handler = new ConsoleControlHandler(() => Interlocked.Increment(ref invocations), ThirtySeconds, ThirtySeconds);

            handler.HandleControlEvent(controlType).Should().BeFalse();

            invocations.Should().Be(1);
        }

        [Theory]
        [InlineData(2)] // CTRL_CLOSE_EVENT
        [InlineData(3)] // undefined
        [InlineData(4)] // undefined
        [InlineData(5)] // CTRL_LOGOFF_EVENT
        [InlineData(6)] // CTRL_SHUTDOWN_EVENT
        public void IgnoresEventsThatCancelKeyPressWouldNotHaveRaised(int controlType)
        {
            var invocations = 0;
            var handler = new ConsoleControlHandler(() => Interlocked.Increment(ref invocations), ThirtySeconds, ThirtySeconds);

            handler.HandleControlEvent(controlType).Should().BeFalse();

            invocations.Should().Be(0);
        }

        [Fact]
        public void DoesNotPropagateExceptionsFromTheCallback()
        {
            // An exception escaping into the native caller would tear the process down.
            var handler = new ConsoleControlHandler(() => throw new InvalidOperationException("Expected"), ThirtySeconds, ThirtySeconds);

            handler.HandleControlEvent(CtrlCEvent).Should().BeFalse();
        }

        [Fact]
        public void DoesNotWaitForeverForACallbackThatNeverCompletes()
        {
            // Deliberately not disposed: the callback may still be inside Wait() when the test returns.
            var release = new ManualResetEventSlim(false);

            try
            {
                var handler = new ConsoleControlHandler(
                    () => release.Wait(),
                    callbackStartTimeout: TimeSpan.FromMilliseconds(100),
                    callbackTimeout: TimeSpan.FromMilliseconds(500));

                handler.HandleControlEvent(CtrlCEvent).Should().BeFalse();
            }
            finally
            {
                // Don't leave the thread pool thread blocked once the test is over
                release.Set();
                release.Wait();
                release.Dispose();
            }
        }

        [Fact]
        public void UnregisterIsIdempotent()
        {
            var handler = ConsoleControlHandler.TryRegister(() => { });
            handler.Should().NotBeNull();

            handler.Unregister();
            handler.Unregister();
        }
    }
}
#endif
