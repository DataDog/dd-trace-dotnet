// <copyright file="BatchingSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/test/Serilog.Sinks.PeriodicBatching.Tests/PeriodicBatchingSinkTests.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Sink.PeriodicBatching
{
    public class BatchingSinkTests
    {
        private static readonly int TinyWaitMs = 200;
        private static readonly int MicroWaitMs = 1;

        // Some very, very approximate tests here :)

        [Fact]
        public void WhenAnEventIsEnqueuedItIsWrittenToABatch_OnFlush()
        {
            var pbs = new InMemoryBatchedSink(TimeSpan.Zero, new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs));
            var evt = "Some event";
            pbs.EnqueueLog(evt);
            pbs.Dispose();

            pbs.Batches.Count.Should().Be(1);
            pbs.Batches[0].Count.Should().Be(1);
            pbs.Batches[0][0].Should().BeSameAs(evt);
            pbs.IsDisposed.Should().BeTrue();
            pbs.WasCalledAfterDisposal.Should().BeFalse();
        }

        [Fact]
        public void WhenAnEventIsEnqueuedItIsWrittenToABatch_OnTimer()
        {
            var pbs = new InMemoryBatchedSink(TimeSpan.Zero, new BatchingSinkOptions(batchSizeLimit: 2, periodMs: TinyWaitMs));
            var evt = "Some event";
            pbs.EnqueueLog(evt);
            Thread.Sleep(TinyWaitMs + TinyWaitMs);
            pbs.Stop();
            pbs.Dispose();

            pbs.Batches.Count.Should().Be(1);
            pbs.IsDisposed.Should().BeTrue();
            pbs.WasCalledAfterDisposal.Should().BeFalse();
        }

        [Fact]
        public void WhenAnEventIsEnqueuedItIsWrittenToABatch_FlushWhileRunning()
        {
            var batchEmitDelay = TimeSpan.FromMilliseconds(TinyWaitMs + TinyWaitMs);
            var pbs = new InMemoryBatchedSink(batchEmitDelay, new BatchingSinkOptions(batchSizeLimit: 2, periodMs: MicroWaitMs));

            var evt = "Some event";
            pbs.EnqueueLog(evt);
            Thread.Sleep(TinyWaitMs);
            pbs.Dispose();

            pbs.Batches.Count.Should().Be(1);
            pbs.IsDisposed.Should().BeTrue();
            pbs.WasCalledAfterDisposal.Should().BeFalse();
        }

        internal class InMemoryBatchedSink : BatchingSink, IDisposable
        {
            private readonly TimeSpan _batchEmitDelay;
            private readonly object _stateLock = new();
            private bool _stopped;

            public InMemoryBatchedSink(TimeSpan batchEmitDelay, BatchingSinkOptions sinkOptions)
                : base(sinkOptions)
            {
                _batchEmitDelay = batchEmitDelay;
            }

            // Postmortem only
            public bool WasCalledAfterDisposal { get; private set; }

            public IList<IList<string>> Batches { get; } = new List<IList<string>>();

            public bool IsDisposed { get; private set; }

            public void Stop()
            {
                lock (_stateLock)
                {
                    _stopped = true;
                }
            }

            protected override Task EmitBatch(Queue<string> events)
            {
                lock (_stateLock)
                {
                    if (_stopped)
                    {
                        return Task.FromResult(0);
                    }

                    if (IsDisposed)
                    {
                        WasCalledAfterDisposal = true;
                    }

                    Thread.Sleep(_batchEmitDelay);
                    Batches.Add(events.ToList());
                }

                return Task.FromResult(0);
            }

            protected override void AdditionalDispose()
            {
                lock (_stateLock)
                {
                    IsDisposed = true;
                }
            }
        }
    }
}
