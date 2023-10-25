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

        // Some very, very approximate tests here :)

        [Fact]
        public async Task WhenRunning_AndAnEventIsQueued_ItIsWrittenToABatchOnDispose()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");

            sink.EnqueueLog(evt);
            await sink.DisposeAsync();

            sink.Batches.Count.Should().Be(1);
            sink.Batches.TryPeek(out var batch).Should().BeTrue();
            batch.Should().BeEquivalentTo(new List<DirectSubmissionLogEvent> { evt });
        }

        [Fact]
        public void WhenRunning_AndAnEventIsQueued_ItIsWrittenToABatch()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");

            sink.EnqueueLog(evt);
            var batches = WaitForBatches(sink);

            batches.Count.Should().Be(1);
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

            // slightly arbitrary time to wait
            Thread.Sleep(1_000);
            sink.Batches.Should().BeEmpty();
        }

        [Fact]
        public void AfterMultipleFailures_SinkIsPermanentlyDisabled()
        {
            var mutex = new ManualResetEventSlim();
            var emitResults = Enumerable.Repeat(false, FailuresBeforeCircuitBreak);
            var sink = new InMemoryBatchedSink(
                DefaultBatchingOptions,
                () => mutex.Set(),
                emitResults.ToArray());
            sink.Start();
            var evt = new TestEvent("Some event");

            for (var i = 0; i < FailuresBeforeCircuitBreak; i++)
            {
                sink.EnqueueLog(evt);
                WaitForBatches(sink, batchCount: i + 1);
            }

            mutex.Wait(30_000).Should().BeTrue($"Sink should be disabled after {FailuresBeforeCircuitBreak} faults");
        }

        [Fact]
        public async Task SinkDoesNotStartEmittingUntilStartIsCalled()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);

            await Task.Delay(5_000);
            sink.Batches.Should().BeEmpty();

            sink.Start();
            var batches = WaitForBatches(sink);
            batches.Should().ContainSingle();
        }

        [SkippableFact]
        public async Task AfterInitialSuccessThenMultipleFailures_SinkIsTemporarilyDisabled()
        {
            var emitResults = new[] { true }
               .Concat(Enumerable.Repeat(false, FailuresBeforeCircuitBreak));

            var sink = new InMemoryBatchedSink(
                DefaultBatchingOptions,
                emitResults: emitResults.ToArray());
            sink.Start();
            var evt = new TestEvent("Some event");

            // Initial success ensures we don't permanently disable the sink
            _output.WriteLine("Queueing first event and waiting for sink");
            sink.EnqueueLog(evt);
            var batches = WaitForBatches(sink, batchCount: 1);
            _output.WriteLine($"Found {batches.Count} batches");

            // There's a race condition here which is tricky to avoid - after the batch is emitted,
            // there's a short delay before the result is processed and logging is disabled. By
            // hooking into the sink DelayEvents, we can make sure the result is processed _before_
            // we add the next event
            var mutex = new ManualResetEventSlim();
            sink.DelayEventAction = x =>
            {
                _output.WriteLine($"Flushing delayed for {x} seconds");
                mutex.Set();
            };

            // Put the sink in a broken status (temporary)
            for (var i = 1; i < FailuresBeforeCircuitBreak; i++)
            {
                _output.WriteLine($"Queueing broken event {i}");
                mutex.Reset();
                sink.EnqueueLog(evt);
                mutex.Wait(TimeSpan.FromSeconds(1));
                batches = WaitForBatches(sink, batchCount: i + 1); // +1 because of initial success event
                _output.WriteLine($"Found {batches.Count} batches");
            }

            _output.WriteLine($"Queueing broken event {FailuresBeforeCircuitBreak} to break the circuit");
            mutex.Reset();
            sink.EnqueueLog(evt);
            mutex.Wait(TimeSpan.FromSeconds(1));
            batches = WaitForBatches(sink, batchCount: FailuresBeforeCircuitBreak + 1);
            _output.WriteLine($"Found {batches.Count} batches (should now be broken)");

            // This should be ignored if the circuit breaker is still broken
            sink.EnqueueLog(evt);

            // wait for 2 flushes to be sure
            await sink.FlushAsync();
            await sink.FlushAsync();

            // ensure we still _don't_ have any more batches
            sink.Batches.Count.Should().Be(FailuresBeforeCircuitBreak + 1);

            // queue another log now circuit is partially open
            _output.WriteLine($"Queueing event in partially open sink");
            sink.EnqueueLog(evt);
            batches = WaitForBatches(sink, batchCount: FailuresBeforeCircuitBreak + 2);
            _output.WriteLine($"Found {batches.Count} batches");
        }

        [Fact]
        public async Task ClosingAfterStart_PreventsEmitting_AndCantBeRestarted()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);
            sink.Start();
            WaitForBatches(sink).Should().HaveCount(1);

            sink.CloseImmediately();
            await Task.Delay(500);
            sink.EnqueueLog(evt);

            await Task.Delay(2_000);
            sink.Batches.Should().HaveCountLessOrEqualTo(1);
            await sink.DisposeAsync();

            await Task.Delay(2_000);
            sink.Batches.Should().HaveCountLessOrEqualTo(1);
        }

        [Fact]
        public void ClosingImmediatelyCallsDisableSinkAction()
        {
            var mutex = new ManualResetEventSlim();
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions, () => mutex.Set());
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);
            sink.Start();

            sink.CloseImmediately();
            mutex.Wait(TimeSpan.FromSeconds(3)).Should().BeTrue();
        }

        [Fact]
        public async Task ClosingImmediatelyPreventsEmitting()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            var evt = new TestEvent("Some event");
            sink.EnqueueLog(evt);

            sink.CloseImmediately();
            sink.Start();

            await Task.Delay(5_000);
            sink.Batches.Should().BeEmpty();
        }

        private static ConcurrentStack<IList<DirectSubmissionLogEvent>> WaitForBatches(InMemoryBatchedSink pbs, int batchCount = 1)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var batches = pbs.Batches;
            while (batches.Count < batchCount && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(TinyWait);
                batches = pbs.Batches;
            }

            return batches;
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
                bool[] emitResults = null)
                : base(sinkOptions, disableSinkAction)
            {
                _emitResults = emitResults ?? Array.Empty<bool>();
            }

            public ConcurrentStack<IList<DirectSubmissionLogEvent>> Batches { get; } = new();

            public Action<TimeSpan> DelayEventAction { get; set; } = _ => { };

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
                DelayEventAction(delayUntilNextFlush);
            }
        }
    }
}
