// <copyright file="OtelThreadContextPublisherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.OtelThreadContext;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext
{
    public class OtelThreadContextPublisherTests
    {
        private const int SpanIdOffset = 16;
        private const int ValidOffset = 24;
        private const int TraceFlagsOffset = 25;
        private const int AttrsDataOffset = 28;

        [Fact]
        public void IsDisabledUnlessTheSettingIsTurnedOn()
        {
            var settings = new TracerSettings();

            settings.OtelThreadContextEnabled.Should().BeFalse("the feature must be opt-in");
            OtelThreadContextPublisher.Create(settings).Should().BeSameAs(NullOtelThreadContextPublisher.Instance);
        }

        [Fact]
        public void ResolvesTheThreadLocalSlotOnlyOncePerThread()
        {
            // This is the whole point of the design: the single native call happens once per OS thread,
            // and every context change after that is a managed write into unmanaged memory.
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            for (var i = 1; i <= 50; i++)
            {
                publisher.Set(CreateSpan(spanId: (ulong)i));
                publisher.Reset();
            }

            provider.CallCount.Should().Be(1);
        }

        [Fact]
        public void PublishesTheActiveContext()
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            var traceId = new TraceId(0x0123456789abcdefUL, 0xfedcba9876543210UL);
            publisher.Set(CreateSpan(traceId, spanId: 0x1122334455667788UL, samplingPriority: SamplingPriorityValues.AutoKeep));

            var record = provider.ReadPublishedRecord();

            record[ValidOffset].Should().Be(1);
            record[TraceFlagsOffset].Should().Be(1, "the trace is sampled");
            HexString.ToHexString(record.AsSpan(0, 16)).Should().Be(HexString.ToHexString(traceId, pad16To32: true));
            HexString.ToHexString(record.AsSpan(SpanIdOffset, 8)).Should().Be(HexString.ToHexString(0x1122334455667788UL));

            // with no TraceContext the local root span is the span itself
            Encoding.ASCII.GetString(record, AttrsDataOffset + 2, 16)
                    .Should().Be(HexString.ToHexString(0x1122334455667788UL));
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(SamplingPriorityValues.UserReject, 0)]
        [InlineData(SamplingPriorityValues.AutoReject, 0)]
        [InlineData(SamplingPriorityValues.AutoKeep, 1)]
        [InlineData(SamplingPriorityValues.UserKeep, 1)]
        public void MapsTheSamplingDecisionOntoTheSampledFlag(int? samplingPriority, byte expectedTraceFlags)
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            publisher.Set(CreateSpan(samplingPriority: samplingPriority));

            provider.ReadPublishedRecord()[TraceFlagsOffset].Should().Be(expectedTraceFlags);
        }

        [Fact]
        public void ResetLeavesTheRecordInvalidWithoutClearingTheContext()
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            publisher.Set(CreateSpan(spanId: 0x1122334455667788UL));
            var published = provider.ReadPublishedRecord();

            publisher.Reset();
            var reset = provider.ReadPublishedRecord();

            reset[ValidOffset].Should().Be(0, "a detached record must be skipped by readers");

            // only the flag changes; the pointer stays installed and the rest of the record is untouched
            published[ValidOffset] = 0;
            reset.Should().Equal(published);
        }

        [Fact]
        public void ResetBeforeAnySetDoesNotResolveTheSlot()
        {
            // A thread that never carried a span has a null slot, which already reads as "no context",
            // so there is nothing to allocate or publish.
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            publisher.Reset();

            provider.CallCount.Should().Be(0);
            provider.GetPublishedRecord().Should().Be(IntPtr.Zero);
        }

        [Fact]
        public void KeepsPublishingToTheSameRecordAcrossContextChanges()
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            publisher.Set(CreateSpan(spanId: 1));
            var first = provider.GetPublishedRecord();

            publisher.Set(CreateSpan(spanId: 2));
            var second = provider.GetPublishedRecord();

            second.Should().Be(first);
            HexString.ToHexString(provider.ReadPublishedRecord().AsSpan(SpanIdOffset, 8))
                     .Should().Be(HexString.ToHexString(2UL));
        }

        [Fact]
        public void EachThreadPublishesItsOwnRecord()
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            var barrier = new Barrier(2);
            var records = new IntPtr[2];
            var spanIds = new[] { 111UL, 222UL };
            var observed = new string[2];

            void Publish(int index)
            {
                barrier.SignalAndWait();
                publisher.Set(CreateSpan(spanId: spanIds[index]));
                records[index] = provider.GetPublishedRecord();
                observed[index] = HexString.ToHexString(provider.ReadPublishedRecord().AsSpan(SpanIdOffset, 8));
            }

            var threads = new[]
            {
                new Thread(() => Publish(0)),
                new Thread(() => Publish(1)),
            };

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();
            }

            provider.CallCount.Should().Be(2, "one slot per thread");
            records[0].Should().NotBe(IntPtr.Zero);
            records[0].Should().NotBe(records[1], "records must not be shared between threads");
            observed[0].Should().Be(HexString.ToHexString(spanIds[0]));
            observed[1].Should().Be(HexString.ToHexString(spanIds[1]));
        }

        [Fact]
        public void DisablesItselfWhenTheSlotIsUnavailable()
        {
            using var provider = new FakeOtelThreadContextSlotProvider(returnNull: true);
            var publisher = new OtelThreadContextPublisher(provider);

            publisher.Set(CreateSpan());

            publisher.IsEnabled.Should().BeFalse();

            // and it stops trying, rather than probing on every span
            publisher.Set(CreateSpan());
            publisher.Reset();
            provider.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task DoesNotForceASamplingDecision()
        {
            // Reading the sampled flag must not make the sampling decision happen earlier than it would
            // otherwise, because that is an observable change in tracer behaviour.
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            await using var tracer = TracerHelper.Create(new TracerSettings(), Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>());

            using var scope = tracer.StartActive("operation");
            var span = (Span)scope.Span;

            span.Context.TraceContext.SamplingPriority.Should().BeNull("no decision has been made yet");

            publisher.Set(span);

            span.Context.TraceContext.SamplingPriority.Should().BeNull("publishing must not trigger sampling");
            provider.ReadPublishedRecord()[TraceFlagsOffset].Should().Be(0, "an undecided trace is reported as not sampled");
        }

        [Fact]
        public async Task PublishesTheLocalRootSpanIdOfNestedSpans()
        {
            using var provider = new FakeOtelThreadContextSlotProvider();
            var publisher = new OtelThreadContextPublisher(provider);

            await using var tracer = TracerHelper.Create(new TracerSettings(), Mock.Of<IAgentWriter>(), Mock.Of<ITraceSampler>());

            using var root = tracer.StartActive("root");
            using var child = tracer.StartActive("child");

            var childSpan = (Span)child.Span;
            publisher.Set(childSpan);

            var record = provider.ReadPublishedRecord();

            HexString.ToHexString(record.AsSpan(SpanIdOffset, 8))
                     .Should().Be(HexString.ToHexString(childSpan.SpanId));
            Encoding.ASCII.GetString(record, AttrsDataOffset + 2, 16)
                    .Should().Be(HexString.ToHexString(root.Span.SpanId), "attribute 0 carries the local root span id");
        }

        private static Span CreateSpan(TraceId traceId = default, ulong spanId = 1, int? samplingPriority = null)
        {
            if (traceId == default)
            {
                traceId = new TraceId(0, spanId);
            }

            var context = new SpanContext(traceId, spanId, samplingPriority, serviceName: "test", origin: null);
            return new Span(context, DateTimeOffset.UtcNow);
        }
    }
}
