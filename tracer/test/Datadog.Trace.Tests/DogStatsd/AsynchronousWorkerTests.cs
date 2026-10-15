// <copyright file="AsynchronousWorkerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.StatsdClient.Worker;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DogStatsd;

/// <summary>
/// The worker of the vendored StatsdClient sleeps up to <see cref="AsynchronousWorker{T}.MaxWaitDuration"/> between the polls
/// of an empty queue: Flush() and DisposeAsync() (the shutdown of the tracer) wake it up instead of waiting for the end of
/// that sleep.
/// </summary>
public class AsynchronousWorkerTests
{
    [Fact]
    public async Task FlushAndDisposeWakeUpAnIdleWorker()
    {
        const int flushes = 20;
        var handler = new Handler();
        var worker = new AsynchronousWorker<int>(handler, new Waiter(), workerThreadCount: 1, maxItemCount: 10, blockingQueueTimeout: null);

        // Idle long enough for the worker to sleep MaxWaitDuration between its polls.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < flushes; i++)
        {
            worker.Flush();
        }

        await worker.DisposeAsync();
        stopwatch.Stop();

        // DisposeAsync flushes too
        handler.Flushes.Should().Be(flushes + 1);

        // Without being woken up, each flush waits for the end of the worker's sleep: about MaxWaitDuration.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromTicks(AsynchronousWorker<int>.MaxWaitDuration.Ticks * flushes / 2));
    }

    private class Handler : IAsynchronousWorkerHandler<int>
    {
        private int _flushes;

        public int Flushes => Volatile.Read(ref _flushes);

        public void OnNewValue(int v)
        {
        }

        public bool OnIdle() => true;

        public void Flush() => Interlocked.Increment(ref _flushes);
    }
}
