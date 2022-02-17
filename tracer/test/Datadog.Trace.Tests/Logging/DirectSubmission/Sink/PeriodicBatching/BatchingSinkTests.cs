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

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class BatchingSinkTests
    {
        private const int DefaultQueueLimit = 100_000;
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CircuitBreakPeriod = TimeSpan.FromSeconds(1);

        private static readonly BatchingSinkOptions DefaultBatchingOptions
            = new(batchSizeLimit: 2, queueLimit: DefaultQueueLimit, period: TinyWait, circuitBreakPeriod: CircuitBreakPeriod);

        private readonly ITestOutputHelper _output;

        public BatchingSinkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Some very, very approximate tests here :)

        [Fact]
        public void WhenRunning_AndAnEventIsQueued_ItIsWrittenToABatchOnDispose()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");

            sink.EnqueueLog(evt);
            sink.Dispose();

            sink.Batches.Count.Should().Be(1);
            sink.Batches.TryPeek(out var batch).Should().BeTrue();
            batch.Should().BeEquivalentTo(new List<DatadogLogEvent> { evt });
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
            batch.Should().BeEquivalentTo(new List<DatadogLogEvent> { evt });
        }

        [Fact]
        public void AfterDisposed_AndAnEventIsQueued_ItIsNotWrittenToABatch()
        {
            var sink = new InMemoryBatchedSink(DefaultBatchingOptions);
            sink.Start();
            var evt = new TestEvent("Some event");
            sink.Dispose();
            sink.EnqueueLog(evt);

            // slightly arbitrary time to wait
            Thread.Sleep(1_000);
            sink.Batches.Should().BeEmpty();
        }

        [Fact]
        public void AfterMultipleFailures_SinkIsPermanentlyDisabled()
        {
            var mutex = new ManualResetEventSlim();
            var emitResults = Enumerable.Repeat(false, BatchingSink.FailuresBeforeCircuitBreak);
            var sink = new InMemoryBatchedSink(
                DefaultBatchingOptions,
                () => mutex.Set(),
                emitResults.ToArray());
            sink.Start();
            var evt = new TestEvent("Some event");

            for (var i = 0; i < BatchingSink.FailuresBeforeCircuitBreak; i++)
            {
                sink.EnqueueLog(evt);
                WaitForBatches(sink, batchCount: i + 1);
            }

            mutex.Wait(10_000).Should().BeTrue($"Sink should be disabled after {BatchingSink.FailuresBeforeCircuitBreak} faults");
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

        [Fact]
        public void AfterInitialSuccessThenMultipleFailures_SinkIsTemporarilyDisabled()
        {
            var emitResults = new[] { true }
               .Concat(Enumerable.Repeat(false, BatchingSink.FailuresBeforeCircuitBreak));

            var sink = new InMemoryBatchedSink(
                DefaultBatchingOptions,
                emitResults: emitResults.ToArray());
            sink.Start();
            var evt = new TestEvent("Some event");

            // Initial success ensures we don't permanently disable the sink
            _output.WriteLine("Queueing first event and waiting for sink");
            sink.EnqueueLog(evt);
            WaitForBatches(sink, batchCount: 1);

            // Put the sink in a broken status (temporary)
            for (var i = 0; i < BatchingSink.FailuresBeforeCircuitBreak; i++)
            {
                _output.WriteLine($"Queueing broken event {i + 1}");
                sink.EnqueueLog(evt);
                WaitForBatches(sink, batchCount: i + 2);
            }

            // initial enqueue should be ignored
            _output.WriteLine($"Queueing event when broken circuit");
            sink.EnqueueLog(evt);
            Thread.Sleep(CircuitBreakPeriod);
            Thread.Sleep(CircuitBreakPeriod);
            // ensure we _don't_ have any more batches
            sink.Batches.Count.Should().Be(BatchingSink.FailuresBeforeCircuitBreak + 1);

            // queue another log now circuit is partially open
            _output.WriteLine($"Queueing event in partially open sink");
            sink.EnqueueLog(evt);
            WaitForBatches(sink, batchCount: BatchingSink.FailuresBeforeCircuitBreak + 2);
        }

        private static ConcurrentStack<IList<DatadogLogEvent>> WaitForBatches(InMemoryBatchedSink pbs, int batchCount = 1)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            var batches = pbs.Batches;
            while (batches.Count < batchCount && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(TinyWait);
                batches = pbs.Batches;
            }

            return batches;
        }

        internal class TestEvent : DatadogLogEvent
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

            public ConcurrentStack<IList<DatadogLogEvent>> Batches { get; } = new();

            protected override Task<bool> EmitBatch(Queue<DatadogLogEvent> events)
            {
                Batches.Push(events.ToList());
                _emitCount++;
                var result = _emitCount < _emitResults.Length
                                 ? _emitResults[_emitCount]
                                 : true;

                return Task.FromResult(result);
            }
        }
    }
}
