// <copyright file="TelemetryControllerSchedulerTests.cs" company="Datadog">
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

public class TelemetryControllerSchedulerTests
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);
    private static readonly Task NeverComplete = Task.Delay(Timeout.Infinite);
    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly SimpleClock _clock = new();
    private readonly DelayFactory _delayFactory = new();
    private TelemetryController.Scheduler _scheduler;

    public TelemetryControllerSchedulerTests()
    {
        _scheduler = new TelemetryController.Scheduler(FlushInterval, () => NeverComplete, _processExit, _clock, _delayFactory);
    }

    [Fact]
    public async Task TypicalLoop()
    {
        _scheduler = GetScheduler();

        // t = 0; should not send initially
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // we expect an infinite flush interval, because initialization is not complete
        // we'll fast-forward to 5s for now
        var mutex = new ManualResetEventSlim();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(Timeout.InfiniteTimeSpan);
            _clock.UtcNow += FiveSeconds;
            mutex.Set();
            return NeverComplete;
        };

        var waitTask = _scheduler.WaitForNextInterval();
        waitTask.IsFaulted.Should().BeFalse();
        _scheduler.SetTracerInitialized();
        mutex.Wait();

        await waitTask;

        // t = 5s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // triggered by first initialization, Next flush at 65s
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();

        // wait for the next loop - delay should be FlushInterval
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(FlushInterval);
            _clock.UtcNow += FlushInterval;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();

        // t = 65s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();

        await _scheduler.WaitForNextInterval(); // next flush at 125s

        // t = 125s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();

        // we'll interrupt the next delay with a process exit signal
        mutex.Reset();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(FlushInterval);
            // Partial clock advancement
            _clock.UtcNow += FiveSeconds;
            mutex.Set();
            return NeverComplete;
        };

        waitTask = _scheduler.WaitForNextInterval();
        waitTask.IsFaulted.Should().BeFalse();
        _processExit.TrySetResult(true);
        mutex.Wait();
        await waitTask;

        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // final flush
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();
    }

    [Fact]
    public async Task TypicalLoop_WithLogsQueueTrigger()
    {
        var queueGenerator = new QueueTaskGenerator();
        _scheduler = GetScheduler(queueGenerator);

        // t = 0; should not send initially
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // we expect an infinite flush interval, because initialization is not complete
        // we'll fast-forward to 5s for now
        var delayMutex = new ManualResetEventSlim();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(Timeout.InfiniteTimeSpan);
            _clock.UtcNow += FiveSeconds;
            delayMutex.Set();
            return NeverComplete;
        };

        var waitTask = _scheduler.WaitForNextInterval();
        waitTask.IsFaulted.Should().BeFalse();
        _scheduler.SetTracerInitialized();
        delayMutex.Wait();

        await waitTask;

        // t = 5s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // triggered by first initialization, Next flush at 65s
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();

        // wait for the next loop - delay should be FlushInterval
        // fast-forward 5s for now, and fire the queue
        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += FiveSeconds;
            delayMutex.Set();
            return NeverComplete;
        };

        var queueTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        queueGenerator.Task = () => queueTcs.Task;

        waitTask = _scheduler.WaitForNextInterval();
        waitTask.IsFaulted.Should().BeFalse();
        delayMutex.Wait();
        queueTcs.SetResult(true); // this triggers the queue task

        await waitTask;

        // t = 10s;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // not a complete interval
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue(); // triggered by queue

        // same deal again
        delayMutex.Reset();
        queueTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        queueGenerator.Task = () => queueTcs.Task;

        waitTask = _scheduler.WaitForNextInterval();
        waitTask.IsFaulted.Should().BeFalse();
        delayMutex.Wait();
        queueTcs.SetResult(true); // this triggers the queue task

        // t = 15s;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // not a complete interval
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue(); // triggered by queue

        // now lets run the next interval properly
        queueGenerator.Task = () => NeverComplete;

        _delayFactory.Task = delay =>
        {
            delay.Should().Be(TimeSpan.FromSeconds(50));
            _clock.UtcNow += delay;
            return Task.CompletedTask;
        };
        await _scheduler.WaitForNextInterval(); // next flush at 125s

        // t = 125s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();
     }

    [Fact]
    public async Task DoesNotFlushTelemetryUntilInitialized()
    {
        // queue immediately indicates it's overfull
        var queue = new QueueTaskGenerator { Task = () => Task.CompletedTask };
        _scheduler = GetScheduler(queue);

        // increment a full flush interval each time
        _delayFactory.Task = delay =>
        {
            _clock.UtcNow += delay;
            return Task.CompletedTask;
        };

        _scheduler.ShouldFlushTelemetry.Should().BeFalse();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeFalse(); // even though queue is ready, can't flush yet

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeFalse();

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeFalse();

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeFalse();

        // don't increment this time
        _delayFactory.Task = delay => NeverComplete;

        _scheduler.SetTracerInitialized();
        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
        _scheduler.ShouldFlushRedactedErrorLogs.Should().BeTrue();
    }

    [Fact]
    public async Task CanChangeFlushInterval()
    {
        _scheduler = GetScheduler();

        // t = 0; should not send initially
        _scheduler.ShouldFlushTelemetry.Should().BeFalse();

        // partial advancement
        var mutex = new ManualResetEventSlim();
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(Timeout.InfiniteTimeSpan);
            _clock.UtcNow += FiveSeconds;
            mutex.Set();
            return NeverComplete;
        };

        var waitTask = _scheduler.WaitForNextInterval();
        _scheduler.SetTracerInitialized();
        mutex.Wait();

        await waitTask;

        // t = 5s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue(); // triggered by first initialization

        // wait for the next loop
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(FlushInterval);
            _clock.UtcNow += FiveSeconds;
            return Task.CompletedTask;
        };

        await _scheduler.WaitForNextInterval();

        // t = 10s;
        _scheduler.ShouldFlushTelemetry.Should().BeFalse(); // Next flush at 65s;

        // change flush interval to be every 5 seconds
        // note that this doesn't reset the "last run" time, which means the scheduler will try to run immediately
        _scheduler.SetFlushInterval(FiveSeconds);
        _delayFactory.Task = _ => throw new Exception("Unexpectedly created a delay task"); // this shouldn't be called

        await _scheduler.WaitForNextInterval();
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();

        // wait for the next loop
        _delayFactory.Task = delay =>
        {
            delay.Should().Be(FiveSeconds);
            _clock.UtcNow += FiveSeconds;
            return Task.CompletedTask;
        };

        // t = 15s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();

        await _scheduler.WaitForNextInterval();

        // t = 20s;
        _scheduler.ShouldFlushTelemetry.Should().BeTrue();
    }

    private TelemetryController.Scheduler GetScheduler(QueueTaskGenerator queueTaskGenerator = null)
        => new(FlushInterval, (queueTaskGenerator ?? new()).GetTask, _processExit, _clock, _delayFactory);

    private class DelayFactory : TelemetryController.Scheduler.IDelayFactory
    {
        public Func<TimeSpan, Task> Task { get; set; } = _ => System.Threading.Tasks.Task.CompletedTask;

        public Task Delay(TimeSpan delay) => Task(delay);
    }

    private class QueueTaskGenerator
    {
        public Func<Task> Task { get; set; } = () => NeverComplete;

        public Task GetTask() => Task();
    }
}
