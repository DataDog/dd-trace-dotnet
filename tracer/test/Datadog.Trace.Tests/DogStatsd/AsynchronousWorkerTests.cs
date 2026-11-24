// <copyright file="AsynchronousWorkerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        const int flushes = 40;
        var handler = new Handler();
        var worker = new AsynchronousWorker<int>(handler, new Waiter(), workerThreadCount: 1, maxItemCount: 10, blockingQueueTimeout: null);

        // Without being woken up, a flush waits for the end of the worker's sleep, whose duration doubles from MinWaitDuration
        // up to MaxWaitDuration (a flush doesn't reset it): only the first 6 flushes take less than half of MaxWaitDuration.
        var durations = new List<TimeSpan>();
        for (var i = 0; i < flushes; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            worker.Flush();
            durations.Add(stopwatch.Elapsed);
        }

        await worker.DisposeAsync();

        // DisposeAsync flushes too
        handler.Flushes.Should().Be(flushes + 1);

        // Woken up, they're immediate: a third of them is enough, so a busy machine can delay the wake-ups of the others.
        var halfMaxWaitDuration = TimeSpan.FromTicks(AsynchronousWorker<int>.MaxWaitDuration.Ticks / 2);
        durations.Count(duration => duration < halfMaxWaitDuration).Should().BeGreaterThanOrEqualTo(flushes / 3, $"flushes took {string.Join(", ", durations)}");
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
