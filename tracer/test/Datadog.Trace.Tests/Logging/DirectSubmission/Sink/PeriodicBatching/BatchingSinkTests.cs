// <copyright file="BatchingSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using BatchingSink = Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching.BatchingSink<Datadog.Trace.Logging.DirectSubmission.Sink.DirectSubmissionLogEvent>;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class BatchingSinkTests
    {
        private const int FailuresBeforeCircuitBreak = 10;
        private const int DefaultQueueLimit = 100_000;
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CircuitBreakPeriod = TimeSpan.FromSeconds(1);

        private static readonly BatchingSinkOptions DefaultBatchingOptions
            = new(
                batchSizeLimit: 2,
                queueLimit: DefaultQueueLimit,
                period: TinyWait,
                circuitBreakPeriod: CircuitBreakPeriod,
                failuresBeforeCircuitBreak: FailuresBeforeCircuitBreak);

        private readonly ITestOutputHelper _output;

        public BatchingSinkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task WhenRunning_AndAnEventIsQueued_ItIsWrittenToABatchOnDispose()
        {
            await using var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");

            sink.EnqueueLog(evt);
            await sink.RunFinalFlushAsync();

            sink.Batches.Count.Should().Be(1);
            sink.Batches.TryPeek(out var batch).Should().BeTrue();
            batch.Should().BeEquivalentTo(new List<DirectSubmissionLogEvent> { evt });
        }

        [Fact]
        public async Task WhenRunning_AndAnEventIsQueued_ItIsWrittenToABatch()
        {
            await using var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");

            sink.EnqueueLog(evt);
            await sink.RunOneIterationAsync(noDelay: true);

            sink.Batches.Count.Should().Be(1);
            sink.Batches.TryPeek(out var batch).Should().BeTrue();
            batch.Should().BeEquivalentTo(new List<DirectSubmissionLogEvent> { evt });
        }

        [Fact]
        public async Task AfterDisposed_AndAnEventIsQueued_ItIsNotWrittenToABatch()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");
            await sink.DisposeAsync();
            sink.EnqueueLog(evt);

            await sink.RunOneIterationAsync(noDelay: true);

            sink.Batches.Should().BeEmpty();
        }

        [Fact]
        public async Task AfterMultipleFailures_SinkIsPermanentlyDisabled()
        {
            var disableActionCalled = false;
            var emitResults = Enumerable.Repeat(false, FailuresBeforeCircuitBreak).ToArray();
            await using var sink = new InMemoryBatchedSink(
                DefaultBatchingOptions,
                () => disableActionCalled = true,
                emitResults);
            sink.Start();
            var evt = new TestEvent("Some event");

            CircuitStatus lastStatus = default;
            for (var i = 0; i < FailuresBeforeCircuitBreak; i++)
            {
                sink.EnqueueLog(evt);
                lastStatus = await sink.RunOneIterationAsync(noDelay: true);
            }

            lastStatus.Should().Be(CircuitStatus.PermanentlyBroken);
            disableActionCalled.Should().BeTrue();
            sink.Batches.Count.Should().Be(FailuresBeforeCircuitBreak);
        }

        [Fact]
        public async Task SinkDoesNotStartEmittingUntilStartIsCalled()
        {
            // This is the one test that needs the real background loop, because it's
            // verifying the gate at the top of FlushBuffersTaskLoopAsync.
            await using var sink = new InMemoryBatchedSink(DefaultBatchingOptions, startBackgroundLoop: true);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);

            // FlushAsync queues a TCS that an iteration sets. Without Start(), the loop is
            // parked at the _tracerInitialized gate and the TCS never completes.
            // In overloaded CI, this doesn't _prove_ that it waits, but if it fails, it proves it _doesn't_.
            var pendingFlush = sink.FlushAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var winner = await Task.WhenAny(pendingFlush, timeoutTask);
            winner.Should().Be(timeoutTask, "sink should not flush before Start");
            sink.Batches.Should().BeEmpty();

            sink.Start();
            await pendingFlush; // gate released; pending flush completes
            sink.Batches.Should().ContainSingle();
        }

        [Fact]
        public async Task AfterInitialSuccessThenMultipleFailures_SinkIsTemporarilyDisabled()
        {
            var emitResults = new[] { true }
                .Concat(Enumerable.Repeat(false, FailuresBeforeCircuitBreak))
                .ToArray();

            await using var sink = new InMemoryBatchedSink(DefaultBatchingOptions, emitResults: emitResults);
            sink.Start();
            var evt = new TestEvent("Some event");

            // Initial success ensures the circuit becomes Broken (not PermanentlyBroken) after the failures.
            sink.EnqueueLog(evt);
            var status = await sink.RunOneIterationAsync(noDelay: true);
            status.Should().Be(CircuitStatus.Closed);
            sink.Batches.Count.Should().Be(1);
            _output.WriteLine($"After initial success: status={status}, batches={sink.Batches.Count}");

            // The 10th failure transitions the circuit to Broken.
            for (var i = 1; i <= FailuresBeforeCircuitBreak; i++)
            {
                sink.EnqueueLog(evt);
                status = await sink.RunOneIterationAsync(noDelay: true);
                _output.WriteLine($"Failure {i}: status={status}, batches={sink.Batches.Count}");
                sink.Batches.Count.Should().Be(i + 1);
                var expected = i < FailuresBeforeCircuitBreak ? CircuitStatus.Closed : CircuitStatus.Broken;
                status.Should().Be(expected);
            }

            // While Broken, _enqueueLogEnabled is false — this enqueue must be dropped.
            sink.EnqueueLog(evt);

            // The next iteration sees an empty queue, MarkSkipped transitions Broken -> HalfBroken.
            status = await sink.RunOneIterationAsync(noDelay: true);
            status.Should().Be(CircuitStatus.HalfBroken);
            sink.Batches.Count.Should().Be(FailuresBeforeCircuitBreak + 1); // dropped enqueue confirmed

            // In HalfBroken, enqueues are accepted again; EmitBatch returns true (past emitResults),
            // MarkSuccess transitions HalfBroken -> Closed.
            sink.EnqueueLog(evt);
            status = await sink.RunOneIterationAsync(noDelay: true);
            status.Should().Be(CircuitStatus.Closed);
            sink.Batches.Count.Should().Be(FailuresBeforeCircuitBreak + 2);
        }

        [Fact]
        public async Task ClosingAfterStart_PreventsEmitting_AndCantBeRestarted()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);
            sink.Start();
            await sink.RunOneIterationAsync(noDelay: true);
            sink.Batches.Should().ContainSingle();

            sink.CloseImmediately();
            sink.EnqueueLog(evt);
            await sink.RunOneIterationAsync(noDelay: true);
            sink.Batches.Should().ContainSingle();

            await sink.DisposeAsync();
            sink.Batches.Should().ContainSingle();
        }

        [Fact]
        public void ClosingImmediatelyCallsDisableSinkAction()
        {
            var disableActionCallCount = 0;
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions, () => Interlocked.Increment(ref disableActionCallCount));
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);
            sink.Start();

            sink.CloseImmediately();
            disableActionCallCount.Should().Be(1);
        }

        [Fact]
        public async Task ClosingImmediatelyPreventsEmitting()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);

            sink.CloseImmediately();
            sink.Start();

            await sink.RunOneIterationAsync(noDelay: true);
            sink.Batches.Should().BeEmpty();
        }

        internal class TestEvent : DirectSubmissionLogEvent
        {
            private readonly string _evt;

            public TestEvent(string evt)
            {
                _evt = evt;
            }

            public override void Format(StringBuilder sb, LogFormatter formatter)
            {
                sb.Append(_evt);
            }
        }

        private class InMemoryBatchedSink : BatchingSink
        {
            private readonly bool[] _emitResults;
            private int _emitCount = -1;

            public InMemoryBatchedSink(
                BatchingSinkOptions sinkOptions,
                Action disableSinkAction = null,
                bool[] emitResults = null,
                bool startBackgroundLoop = false)
                : base(sinkOptions, disableSinkAction, startBackgroundLoop)
            {
                _emitResults = emitResults ?? Array.Empty<bool>();
            }

            public ConcurrentStack<IList<DirectSubmissionLogEvent>> Batches { get; } = new();

            protected override Task<bool> EmitBatch(Queue<DirectSubmissionLogEvent> events)
            {
                Batches.Push(events.ToList());
                _emitCount++;
                var result = _emitCount < _emitResults.Length
                                 ? _emitResults[_emitCount]
                                 : true;

                return Task.FromResult(result);
            }

            protected override void FlushingEvents(int queueSizeBeforeFlush)
            {
            }

            protected override void DelayEvents(TimeSpan delayUntilNextFlush)
            {
            }
        }
    }
}
