// <copyright file="PortableTimerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/test/Serilog.Sinks.PeriodicBatching.Tests/PortableTimerTests.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;

#pragma warning disable 1998

// ReSharper disable AccessToModifiedClosure

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class PortableTimerTests
    {
        [Fact]
        public void WhenItStartsItWaitsUntilHandled_OnDispose()
        {
            var wasCalled = false;

            var barrier = new Barrier(participantCount: 2);

            using (var timer = new PortableTimer(
                                    async _ =>
                                    {
                                        barrier.SignalAndWait();
                                        await Task.Delay(100);
                                        wasCalled = true;
                                    }))
            {
                timer.Start(TimeSpan.Zero);
                barrier.SignalAndWait();
            }

            wasCalled.Should().BeTrue();
        }

        [Fact]
        public void WhenWaitingShouldCancel_OnDispose()
        {
            var wasCalled = false;

            using (var timer = new PortableTimer(async _ =>
            {
                await Task.Delay(50);
                wasCalled = true;
            }))
            {
                timer.Start(TimeSpan.FromMilliseconds(20));
            }

            Thread.Sleep(100);

            wasCalled.Should().BeFalse();
        }

        [Fact]
        public void WhenActiveShouldCancel_OnDispose()
        {
            var wasCalled = false;

            var barrier = new Barrier(participantCount: 2);

            using (var timer = new PortableTimer(
                                    async token =>
                                    {
                                        // ReSharper disable once MethodSupportsCancellation
                                        barrier.SignalAndWait();
                                        // ReSharper disable once MethodSupportsCancellation
                                        await Task.Delay(20);

                                        wasCalled = true;
                                        Interlocked.MemoryBarrier();
                                        await Task.Delay(100, token);
                                    }))
            {
                timer.Start(TimeSpan.FromMilliseconds(20));
                barrier.SignalAndWait();
            }

            Thread.Sleep(100);
            Interlocked.MemoryBarrier();

            wasCalled.Should().BeTrue();
        }

        [Fact]
        public void WhenDisposedWillThrow_OnStart()
        {
            var wasCalled = false;
            var timer = new PortableTimer(async arg => { wasCalled = true; });
            timer.Start(TimeSpan.FromMilliseconds(100));
            timer.Dispose();

            wasCalled.Should().BeFalse();
            Assert.Throws<ObjectDisposedException>(() => timer.Start(TimeSpan.Zero));
        }

        [Fact]
        public void WhenOverlapsShouldProcessOneAtTime_OnTick()
        {
            var userHandlerOverlapped = false;

            PortableTimer timer = null;
            timer = new PortableTimer(
                async _ =>
                {
                    if (Monitor.TryEnter(timer))
                    {
                        try
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            timer.Start(TimeSpan.Zero);
                            Thread.Sleep(20);
                        }
                        finally
                        {
                            Monitor.Exit(timer);
                        }
                    }
                    else
                    {
                        userHandlerOverlapped = true;
                    }
                });

            timer.Start(TimeSpan.FromMilliseconds(1));
            Thread.Sleep(50);
            timer.Dispose();

            userHandlerOverlapped.Should().BeFalse();
        }

        [Fact]
        public void CanBeDisposedFromMultipleThreads()
        {
            PortableTimer timer = null;
            // ReSharper disable once PossibleNullReferenceException
            timer = new PortableTimer(async _ => timer.Start(TimeSpan.FromMilliseconds(1)));

            timer.Start(TimeSpan.Zero);
            Thread.Sleep(50);

            Parallel.For(0, Environment.ProcessorCount * 2, _ => timer.Dispose());
        }
    }
}
