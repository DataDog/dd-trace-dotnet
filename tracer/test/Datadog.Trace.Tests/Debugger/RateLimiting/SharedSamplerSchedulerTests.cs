// <copyright file="SharedSamplerSchedulerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    public class SharedSamplerSchedulerTests : IDisposable
    {
        private SharedSamplerScheduler _scheduler;

        public SharedSamplerSchedulerTests()
        {
            _scheduler = new SharedSamplerScheduler();
        }

        public void Dispose()
        {
            _scheduler?.Dispose();
        }

        [Fact]
        public void Constructor_Succeeds()
        {
            using var scheduler = new SharedSamplerScheduler();
            scheduler.Should().NotBeNull();
        }

        [Fact]
        public void Schedule_NullCallback_Throws()
        {
            Action act = () => _scheduler.Schedule(null, TimeSpan.FromMilliseconds(100));
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Schedule_ZeroInterval_Throws()
        {
            Action act = () => _scheduler.Schedule(() => { }, TimeSpan.Zero);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Schedule_NegativeInterval_Throws()
        {
            Action act = () => _scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(-100));
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Schedule_ValidCallback_ReturnsSubscription()
        {
            var subscription = _scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(100));
            subscription.Should().NotBeNull();
            subscription.Dispose();
        }

        [Fact]
        public void Schedule_CallbackInvoked_OnTimerTick()
        {
            var invoked = 0;
            var resetEvent = new ManualResetEventSlim(false);

            var subscription = _scheduler.Schedule(
                () =>
                {
                    Interlocked.Increment(ref invoked);
                    resetEvent.Set();
                },
                TimeSpan.FromMilliseconds(50));

            // Wait for at least one invocation
            var signaled = resetEvent.Wait(TimeSpan.FromSeconds(2));

            signaled.Should().BeTrue("Callback should have been invoked");
            invoked.Should().BeGreaterOrEqualTo(1);

            subscription.Dispose();
        }

        [Fact]
        public void Schedule_MultipleCallbacks_SameInterval_AllInvoked()
        {
            var invoked1 = 0;
            var invoked2 = 0;
            var invoked3 = 0;
            var interval = TimeSpan.FromMilliseconds(50);

            var sub1 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked1), interval);
            var sub2 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked2), interval);
            var sub3 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked3), interval);

            Thread.Sleep(150); // Wait for multiple invocations

            invoked1.Should().BeGreaterOrEqualTo(1);
            invoked2.Should().BeGreaterOrEqualTo(1);
            invoked3.Should().BeGreaterOrEqualTo(1);

            sub1.Dispose();
            sub2.Dispose();
            sub3.Dispose();
        }

        [Fact]
        public void Schedule_DifferentIntervals_IndependentInvocation()
        {
            var invoked100 = 0;
            var invoked200 = 0;

            var sub1 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked100), TimeSpan.FromMilliseconds(100));
            var sub2 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked200), TimeSpan.FromMilliseconds(200));

            Thread.Sleep(450); // 100ms callback should fire ~4 times, 200ms ~2 times

            invoked100.Should().BeGreaterThan(invoked200);
            invoked100.Should().BeInRange(3, 6); // Approximately 4 times
            invoked200.Should().BeInRange(1, 3); // Approximately 2 times

            sub1.Dispose();
            sub2.Dispose();
        }

        [Fact]
        public void Subscription_Dispose_StopsInvocation()
        {
            var invoked = 0;
            var subscription = _scheduler.Schedule(() => Interlocked.Increment(ref invoked), TimeSpan.FromMilliseconds(50));

            Thread.Sleep(150);
            var countBeforeDispose = invoked;
            countBeforeDispose.Should().BeGreaterOrEqualTo(1);

            subscription.Dispose();
            Thread.Sleep(150);

            // Count should not increase (or increase minimally due to race)
            var countAfterDispose = invoked;
            (countAfterDispose - countBeforeDispose).Should().BeLessThan(2);
        }

        [Fact]
        public void Subscription_DisposeTwice_Safe()
        {
            var subscription = _scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(100));

            subscription.Dispose();
            subscription.Dispose();

            Action act = () => subscription.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Callback_ThrowsException_DoesNotCrashScheduler()
        {
            var goodInvoked = 0;
            var badInvoked = 0;

            var goodSub = _scheduler.Schedule(() => Interlocked.Increment(ref goodInvoked), TimeSpan.FromMilliseconds(50));
            var badSub = _scheduler.Schedule(
                () =>
                {
                    Interlocked.Increment(ref badInvoked);
                    throw new InvalidOperationException("Test exception");
                },
                TimeSpan.FromMilliseconds(50));

            Thread.Sleep(150);

            // Both should have been invoked despite exception
            goodInvoked.Should().BeGreaterOrEqualTo(1);
            badInvoked.Should().BeGreaterOrEqualTo(1);

            goodSub.Dispose();
            badSub.Dispose();
        }

        [Fact]
        public void Scheduler_Dispose_StopsAllCallbacks()
        {
            var invoked1 = 0;
            var invoked2 = 0;

            var sub1 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked1), TimeSpan.FromMilliseconds(50));
            var sub2 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked2), TimeSpan.FromMilliseconds(50));

            Thread.Sleep(100);
            var count1Before = invoked1;
            var count2Before = invoked2;

            _scheduler.Dispose();
            Thread.Sleep(150);

            // Counts should not increase significantly after disposal
            (invoked1 - count1Before).Should().BeLessThan(2);
            (invoked2 - count2Before).Should().BeLessThan(2);
        }

        [Fact]
        public void Scheduler_Dispose_MultipleCalls_Safe()
        {
            var scheduler = new SharedSamplerScheduler();

            scheduler.Dispose();
            scheduler.Dispose();

            Action act = () => scheduler.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Schedule_AfterDispose_Throws()
        {
            var scheduler = new SharedSamplerScheduler();
            scheduler.Dispose();

            Action act = () => scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(100));
            act.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void Schedule_ManyCallbacks_AllInvoked()
        {
            var callbackCount = 20;
            var counters = new int[callbackCount];
            var subscriptions = new List<IDisposable>();

            for (int i = 0; i < callbackCount; i++)
            {
                var index = i;
                var sub = _scheduler.Schedule(() => Interlocked.Increment(ref counters[index]), TimeSpan.FromMilliseconds(50));
                subscriptions.Add(sub);
            }

            Thread.Sleep(150);

            // All callbacks should have been invoked
            foreach (var count in counters)
            {
                count.Should().BeGreaterOrEqualTo(1);
            }

            foreach (var sub in subscriptions)
            {
                sub.Dispose();
            }
        }

        [Fact]
        public void ConcurrentSchedule_ThreadSafe()
        {
            var tasks = Enumerable.Range(0, 10)
                .Select(i => Task.Run(() =>
                {
                    var sub = _scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(100));
                    Thread.Sleep(10);
                    sub.Dispose();
                }))
                .ToArray();

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void ConcurrentDispose_ThreadSafe()
        {
            var subscriptions = new List<IDisposable>();

            for (int i = 0; i < 20; i++)
            {
                var sub = _scheduler.Schedule(() => { }, TimeSpan.FromMilliseconds(50));
                subscriptions.Add(sub);
            }

            var tasks = subscriptions.Select(sub => Task.Run(() => sub.Dispose())).ToArray();

            var act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void Schedule_SameInterval_SharesTimer()
        {
            // This is more of a conceptual test - we can't easily verify internal timer count
            // but we can verify behavior is consistent
            var interval = TimeSpan.FromMilliseconds(100);
            var invoked1 = 0;
            var invoked2 = 0;
            var invoked3 = 0;

            var sub1 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked1), interval);
            var sub2 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked2), interval);
            var sub3 = _scheduler.Schedule(() => Interlocked.Increment(ref invoked3), interval);

            Thread.Sleep(250);

            // All should have similar invocation counts (within 1 of each other)
            var counts = new[] { invoked1, invoked2, invoked3 };
            var min = counts.Min();
            var max = counts.Max();

            (max - min).Should().BeLessThanOrEqualTo(1, "All callbacks with same interval should fire approximately together");

            sub1.Dispose();
            sub2.Dispose();
            sub3.Dispose();
        }

        [Fact]
        public void LongRunningCallback_DoesNotBlockOthers()
        {
            var quickInvoked = 0;
            var slowInvoked = 0;

            var quickSub = _scheduler.Schedule(() => Interlocked.Increment(ref quickInvoked), TimeSpan.FromMilliseconds(50));
            var slowSub = _scheduler.Schedule(
                () =>
                {
                    Interlocked.Increment(ref slowInvoked);
                    Thread.Sleep(100); // Simulate slow callback
                },
                TimeSpan.FromMilliseconds(50));

            Thread.Sleep(200);

            // Quick callback should have been invoked multiple times despite slow callback
            quickInvoked.Should().BeGreaterOrEqualTo(2);
            slowInvoked.Should().BeGreaterOrEqualTo(1);

            quickSub.Dispose();
            slowSub.Dispose();
        }
    }
}
