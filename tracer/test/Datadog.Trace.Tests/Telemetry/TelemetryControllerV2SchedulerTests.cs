// <copyright file="TelemetryControllerV2SchedulerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryControllerV2SchedulerTests
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AggregateInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HalfOfAggregateInterval = TimeSpan.FromSeconds(5);
    private static readonly Task NeverComplete = Task.Delay(Timeout.Infinite);
    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly SimpleClock _clock = new();
    private readonly DelayFactory _delayFactory = new();
    private TelemetryControllerV2.Scheduler _scheduler;

    public TelemetryControllerV2SchedulerTests()
    {
        _scheduler = new TelemetryControllerV2.Scheduler(FlushInterval, AggregateInterval, _processExit, _clock, _delayFactory);
    }

    [Fact]
    public async Task TypicalLoop()
    {
        _scheduler = GetScheduler();

        // t = 0; should not send initially
        _scheduler.ShouldAggregateMetrics.Should().BeFalse();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // we expect the aggregate flush delay, but we'll stop it before that happens
        var mutex = new ManualResetEventSlim();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += HalfOfAggregateInterval;
            mutex.Set();
            return NeverComplete;
        };

        var waitTask = _scheduler.WaitForNextInterval();
        _scheduler.SetTracerInitialized();
        mutex.Wait();

        await waitTask;

        // t = 5s;
        _scheduler.ShouldAggregateMetrics.Should().BeFalse(); // delay not expired;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // triggered by first initialization;

        // wait for the next loop - delay should be HalfOfAggregateInterval
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(HalfOfAggregateInterval);
            _clock.UtcNow += HalfOfAggregateInterval;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();

        // t = 10s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        // standard aggregation delay now
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += AggregateInterval;
            return Task.CompletedTask;
        };
        await _scheduler.WaitForNextInterval();

        // t = 20s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        await _scheduler.WaitForNextInterval();
        // t = 30s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        await _scheduler.WaitForNextInterval();
        // t = 40s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        await _scheduler.WaitForNextInterval();
        // t = 50s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        await _scheduler.WaitForNextInterval();
        // t = 60s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        // expect a 5s delay now
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(HalfOfAggregateInterval);
            _clock.UtcNow += HalfOfAggregateInterval;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();
        // t = 65s;
        _scheduler.ShouldAggregateMetrics.Should().BeFalse(); // delay not expired, next aggregate at 70s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // Flush time, next flush at 125s!

        await _scheduler.WaitForNextInterval();
        // t = 70s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // next flush at 125s

        // standard aggregation delay again
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += AggregateInterval;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();

        // t = 80s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // next flush at 125s

        // we'll interrupt the next delay with a process exit signal
        mutex.Reset();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            // Partial clock advancement
            _clock.UtcNow += HalfOfAggregateInterval;
            mutex.Set();
            return NeverComplete;
        };

        waitTask = _scheduler.WaitForNextInterval();
        _processExit.TrySetResult(true);
        mutex.Wait();
        await waitTask;

        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // final flush
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // final flush
    }

    [Fact]
    public async Task AggregatesMetricsWhenClockExpires()
    {
        // should not send initially
        _scheduler.ShouldAggregateMetrics.Should().BeFalse();

        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += delay;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldAggregateMetrics.Should().BeTrue();

        // increment by _almost_ the required time
        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += delay.Subtract(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        };
        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldAggregateMetrics.Should().BeFalse();

        // ok, the final second
        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += TimeSpan.FromSeconds(1);
            return Task.CompletedTask;
        };
        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldAggregateMetrics.Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotFlushTelemetryUntilInitialized()
    {
        // increment a full flush interval each time
        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += delay;
            return Task.CompletedTask;
        };

        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // don't increment this time
        _delayFactory.Task = delay => NeverComplete;

        _scheduler.SetTracerInitialized();
        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
    }

    [Fact]
    public async Task CanChangeFlushInterval()
    {
        _scheduler = GetScheduler();

        // t = 0; should not send initially
        _scheduler.ShouldAggregateMetrics.Should().BeFalse();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // for simplicity, advance the clock and set tracer initialized at the same time
        var mutex = new ManualResetEventSlim();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += AggregateInterval;
            mutex.Set();
            return NeverComplete;
        };

        var waitTask = _scheduler.WaitForNextInterval();
        _scheduler.SetTracerInitialized();
        mutex.Wait();

        await waitTask;

        // t = 10s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // triggered by first initialization

        // wait for the next loop
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += AggregateInterval;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();

        // t = 20s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 70s;

        // change flush interval to be same as metric interval
        // note that this doesn't reset the "last run" time, which means the scheduler will try to run immediately
        _scheduler.SetFlushInterval(AggregateInterval);
        _delayFactory.Task = _ => throw new Exception("Unexpectedly created a delay task"); // this shouldn't be called

        await _scheduler.WaitForNextInterval();

        // t = 20s;
        _scheduler.ShouldAggregateMetrics.Should().BeFalse(); // we're still at the same time as far as the scheduler is concerned, so no expiry
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // missed the timer, so run now

        // Back to normal now
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(AggregateInterval);
            _clock.UtcNow += AggregateInterval;
            return Task.CompletedTask;
        };
        await _scheduler.WaitForNextInterval();

        // t = 30s;
        _scheduler.ShouldAggregateMetrics.Should().BeTrue(); // delay expired, aggregate time;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // timer expired, flush time;
    }

    private TelemetryControllerV2.Scheduler GetScheduler()
        => new(FlushInterval, AggregateInterval, _processExit, _clock, _delayFactory);

    private class DelayFactory : TelemetryControllerV2.Scheduler.IDelayFactory
    {
        public Func<TimeSpan, Task> Task { get; set; } = _ => System.Threading.Tasks.Task.CompletedTask;

        public Task Delay(TimeSpan delay) => Task(delay);
    }
}
