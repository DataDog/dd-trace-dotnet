// <copyright file="AgentWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Agent
{
    public class AgentWriterTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly AgentWriter _agentWriter;
        private readonly Mock<IApi> _api;

        public AgentWriterTests(ITestOutputHelper output)
        {
            _output = output;
            _api = new Mock<IApi>();
            _agentWriter = new AgentWriter(_api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => _agentWriter.FlushAndCloseAsync();

        [Fact]
        public async Task SpanSampling_CanComputeStats_ShouldNotSend_WhenSpanSamplingDoesNotMatch()
        {
            var api = new Mock<IApi>();
            var settings = SpanSamplingRule("*", "*", 0.0f); // don't sample any rule
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object, statsAggregator);

            await using var tracer = TracerHelper.Create(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.UserReject, mechanism: SamplingMechanism.Manual, rate: null, limiterRate: null);
            span.Finish(); // triggers the span sampler to run
            var traceChunk = new SpanCollection([span]);

            agent.WriteTrace(traceChunk);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Never);

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_SingleMatchedSpan_WhenStatsDrops()
        {
            var api = new Mock<IApi>();
            byte[] actualData = [];
            var actualDroppedP0Traces = 0L;
            var actualDroppedP0Spans = 0L;
            api.Setup(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Callback((ArraySegment<byte> traces, int _, bool _, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool _) =>
                {
                    actualData = CopyPayload(traces);
                    actualDroppedP0Traces = numberOfDroppedP0Traces;
                    actualDroppedP0Spans = numberOfDroppedP0Spans;
                })
                .ReturnsAsync(true);

            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var settings = SpanSamplingRule("*", "*");
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object, statsAggregator);
            await using var tracer = TracerHelper.Create(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.UserReject, mechanism: SamplingMechanism.Manual, rate: null, limiterRate: null);
            span.Finish();
            var traceChunk = new SpanCollection([span]);

            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // Build the expectation after the flush: serializing the chunk sets TraceContext.TracesKeepRate,
            // which TraceChunkModel snapshots, so a model built earlier would be missing _dd.tracer_kr.
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(new TraceChunkModel(traceChunk, SamplingPriorityValues.UserKeep, isFirstChunkInPayload: true), SpanFormatterResolver.Instance);

            var expectedDroppedP0Traces = 1;
            var expectedDroppedP0Spans = 0;
            api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
            AssertPayloadEqual(actualData, expectedData1);
            actualDroppedP0Traces.Should().Be(expectedDroppedP0Traces);
            actualDroppedP0Spans.Should().Be(expectedDroppedP0Spans);

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_MultipleMatchedSpans_WhenStatsDrops()
        {
            var api = new Mock<IApi>();
            byte[] actualData = [];
            var actualDroppedP0Traces = 0L;
            var actualDroppedP0Spans = 0L;
            api.Setup(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Callback((ArraySegment<byte> traces, int _, bool _, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool _) =>
                {
                    actualData = CopyPayload(traces);
                    actualDroppedP0Traces = numberOfDroppedP0Traces;
                    actualDroppedP0Spans = numberOfDroppedP0Spans;
                })
                .ReturnsAsync(true);

            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var settings = SpanSamplingRule("*", "*");
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object, statsAggregator);
            await using var tracer = TracerHelper.Create(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.UserReject, mechanism: SamplingMechanism.Manual, rate: null, limiterRate: null);
            var rootSpanContext = new SpanContext(null, traceContext, "service");
            var rootSpan = new Span(rootSpanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            var keptChildSpan = new Span(new SpanContext(rootSpanContext, traceContext, "service"), DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(rootSpan); // IS single span sampled
            traceContext.AddSpan(keptChildSpan); // IS single span sampled

            rootSpan.Finish();
            keptChildSpan.Finish();

            var expectedChunk = new SpanCollection([rootSpan, keptChildSpan]);

            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // Build the expectation after the flush: serializing the chunk sets TraceContext.TracesKeepRate,
            // which TraceChunkModel snapshots, so a model built earlier would be missing _dd.tracer_kr.
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(new TraceChunkModel(expectedChunk, SamplingPriorityValues.UserKeep, isFirstChunkInPayload: true), SpanFormatterResolver.Instance);

            var expectedDroppedP0Traces = 1;
            var expectedDroppedP0Spans = 0;
            api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
            AssertPayloadEqual(actualData, expectedData1);
            actualDroppedP0Traces.Should().Be(expectedDroppedP0Traces);
            actualDroppedP0Spans.Should().Be(expectedDroppedP0Spans);

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_MultipleMatchedSpans_WhenStatsDropsOne()
        {
            var api = new MockApi();
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);

            var settings = SpanSamplingRule("*", "operation");
            var agentWriter = AgentWriterHelper.CreateWithManualFlush(api, statsAggregator);
            await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.UserReject, mechanism: SamplingMechanism.Manual, rate: null, limiterRate: null);
            var rootSpanContext = new SpanContext(null, traceContext, "testhost");
            var rootSpan = new Span(rootSpanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            var droppedChildSpan = new Span(new SpanContext(rootSpanContext, traceContext, "testhost"), DateTimeOffset.UtcNow) { OperationName = "drop_me" };
            var droppedChildSpan2 = new Span(new SpanContext(rootSpanContext, traceContext, "testhost"), DateTimeOffset.UtcNow) { OperationName = "drop_me_also" };
            var keptChildSpan = new Span(new SpanContext(rootSpanContext, traceContext, "testhost"), DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(rootSpan); // IS single span sampled
            traceContext.AddSpan(droppedChildSpan); // is NOT single span sampled
            traceContext.AddSpan(droppedChildSpan2); // is NOT single span sampled
            traceContext.AddSpan(keptChildSpan); // IS single span sampled

            // run spans that will be kept through the span sampler - so that we can get the correct tags on them for asserting
            traceContext.CurrentTraceSettings.SpanSampler!.MakeSamplingDecision(rootSpan);
            traceContext.CurrentTraceSettings.SpanSampler!.MakeSamplingDecision(keptChildSpan);

            var spans = new[] { rootSpan, droppedChildSpan, droppedChildSpan2, keptChildSpan };
            var traceChunk = new SpanCollection(spans);
            agentWriter.WriteTrace(traceChunk);
            await agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // expecting a single trace, but there should have been two spans
            api.DroppedP0TracesCount.Should().Be(1);
            api.DroppedP0SpansCount.Should().Be(2);

            api.Traces.Should().HaveCount(1);
            api.Traces[0].Should().HaveCount(2);

            await agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task PushStats()
        {
            var spans = CreateTraceChunk(1);
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => spans);
            var agent = AgentWriterHelper.CreateWithManualFlush(Mock.Of<IApi>(), statsAggregator);

            WriteTraceAndWait(agent, spans);

            statsAggregator.AddedSpans.Should().Contain(spans).Which.Count.Should().Be(1);

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            byte[] actualPayload = [];
            _api.Setup(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Callback((ArraySegment<byte> traces, int _, bool _, long _, long _, bool _) =>
                {
                    actualPayload = CopyPayload(traces);
                })
                .ReturnsAsync(true);

            var spans = CreateTraceChunk(1);
            var traceChunk = new TraceChunkModel(spans);
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(spans);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
            AssertPayloadEqual(actualPayload, expectedData1);

            _api.Invocations.Clear();
            actualPayload = [];

            spans = CreateTraceChunk(1, 2);
            traceChunk = new TraceChunkModel(spans);
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(spans);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
            AssertPayloadEqual(actualPayload, expectedData2);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new AgentWriter(_api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FaultyApi()
        {
            // The flush thread should be able to recover from an error when calling the API
            // Also, it should free the faulty buffer
            var api = new Mock<IApi>();
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object);

            api.Setup(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
               .Returns(() => throw new InvalidOperationException());

            agent.WriteTrace(CreateTraceChunk(1));

            await agent.FlushTracesAsync();

            agent.ActiveBuffer.Should().BeSameAs(agent.FrontBuffer);
            agent.FrontBuffer.IsEmpty.Should().BeTrue();
            agent.BackBuffer.IsEmpty.Should().BeTrue();

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task BufferStaysWritableWhileItsPayloadIsBeingSent()
        {
            // Flushing detaches the payload instead of holding the buffer for the duration of the
            // send, so the serialization thread can keep writing to the very buffer being flushed.
            // Before that change the buffer was locked across the send, and a trace arriving at the
            // wrong moment could find both buffers unavailable and be dropped.
            var api = new Mock<IApi>();
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp);

            using var sendStarted = new ManualResetEventSlim();
            using var releaseSend = new ManualResetEventSlim();
            var alreadyBlocked = 0;

            api.Setup(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
               .Callback(() =>
                {
                    // Only stall the first send, so that shutdown isn't blocked
                    if (Interlocked.Exchange(ref alreadyBlocked, 1) == 0)
                    {
                        sendStarted.Set();
                        releaseSend.Wait(30_000);
                    }
                })
               .Returns(Task.FromResult(true));

            agent.WriteTrace(CreateTraceChunk(1));

            sendStarted.Wait(30_000).Should().BeTrue("the flush loop should have started sending the first trace");

            // The flush thread is stuck inside SendTracesAsync, but it took the payload with it,
            // so the active buffer is empty and immediately writable again
            agent.ActiveBuffer.Should().BeSameAs(agent.FrontBuffer);
            agent.FrontBuffer.IsEmpty.Should().BeTrue();

            WriteTraceAndWait(agent, CreateTraceChunk(2));

            // No swap was needed, because the buffer was never unavailable
            agent.ActiveBuffer.Should().BeSameAs(agent.FrontBuffer);
            agent.FrontBuffer.TraceCount.Should().Be(1);
            agent.FrontBuffer.SpanCount.Should().Be(2);
            agent.BackBuffer.IsEmpty.Should().BeTrue();

            // Nothing was dropped while the send was in flight, and nothing timed out waiting
            // for the buffer either
            agent.DroppedTracesBufferFull.Should().Be(0);
            agent.DroppedTracesBufferFullAndLocked.Should().Be(0);
            agent.DroppedTracesBuffersLocked.Should().Be(0);
            agent.DroppedTracesTooLarge.Should().Be(0);

            releaseSend.Set();

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushBothBuffers()
        {
            // When the back buffer is full, both buffers should be flushed
            var api = new Mock<IApi>();

            var sizeOfTrace = ComputeSize(CreateTraceChunk(1));

            // Make the buffer size big enough for a single trace
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object, maxBufferSize: (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1);

            WriteTraceAndWait(agent, CreateTraceChunk(1));
            WriteTraceAndWait(agent, CreateTraceChunk(1));

            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);

            agent.FrontBuffer.IsFull.Should().BeTrue();
            agent.FrontBuffer.TraceCount.Should().Be(1);

            agent.BackBuffer.IsFull.Should().BeFalse();
            agent.BackBuffer.TraceCount.Should().Be(1);

            await agent.FlushTracesAsync();

            agent.FrontBuffer.IsEmpty.Should().BeTrue();
            agent.BackBuffer.IsEmpty.Should().BeTrue();

            api.Verify(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Exactly(2));

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushesAFallbackBufferThatIsNotFull()
        {
            // A Locked write swaps away from a buffer that holds traces but was never marked full.
            // Nothing else will ever empty that buffer, so a flush has to pick it up regardless of
            // whether it is full.
            var api = new MockApi();
            var agent = AgentWriterHelper.CreateWithManualFlush(api);

            WriteTraceAndWait(agent, CreateTraceChunk(1));

            agent.SwapActiveBufferForTests();

            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);
            agent.FrontBuffer.IsFull.Should().BeFalse();
            agent.FrontBuffer.TraceCount.Should().Be(1);

            await agent.FlushTracesAsync();

            api.Traces.Should().HaveCount(1);
            agent.FrontBuffer.IsEmpty.Should().BeTrue();

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task DropTraces()
        {
            // Traces should be dropped when both buffers are full
            var statsd = new Mock<IDogStatsd>();

            var sizeOfTrace = ComputeSize(CreateTraceChunk(1));

            // Make the buffer size big enough for a single trace
            var agent = AgentWriterHelper.CreateWithManualFlush(
                Mock.Of<IApi>(),
                statsd: new TestStatsdManager(statsd.Object),
                maxBufferSize: (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1,
                initialTracerMetricsEnabled: true);

            // Fill the two buffers
            WriteTraceAndWait(agent, CreateTraceChunk(1));
            WriteTraceAndWait(agent, CreateTraceChunk(1));

            // Buffers should have swapped
            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);

            // The agent does not know yet that the new active buffer is full
            agent.ActiveBuffer.IsFull.Should().BeFalse();

            // Both buffers have 1 trace stored
            agent.FrontBuffer.TraceCount.Should().Be(1);
            agent.FrontBuffer.SpanCount.Should().Be(1);

            agent.BackBuffer.TraceCount.Should().Be(1);
            agent.BackBuffer.SpanCount.Should().Be(1);

            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null), Times.Exactly(2));
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null), Times.Exactly(2));
            statsd.VerifyNoOtherCalls();
            statsd.Invocations.Clear();

            // Both buffers are at capacity, write a new trace
            WriteTraceAndWait(agent, CreateTraceChunk(2));

            // Buffers shouldn't have swapped since the reserve buffer was full
            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);

            // Both buffers should be full with 1 trace stored
            agent.FrontBuffer.IsFull.Should().BeTrue();
            agent.FrontBuffer.TraceCount.Should().Be(1);
            agent.FrontBuffer.SpanCount.Should().Be(1);

            agent.BackBuffer.IsFull.Should().BeTrue();
            agent.BackBuffer.TraceCount.Should().Be(1);
            agent.BackBuffer.SpanCount.Should().Be(1);

            agent.DroppedTracesBufferFull.Should().Be(1);
            agent.DroppedTracesTooLarge.Should().Be(0);

            // Dropped trace should have been reported to statsd
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 2, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedSpans, 2, 1, null), Times.Once);
            statsd.VerifyNoOtherCalls();

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task DropTraceThatExceedsBufferSize()
        {
            var statsd = new Mock<IDogStatsd>();
            var agent = AgentWriterHelper.CreateWithManualFlush(
                Mock.Of<IApi>(),
                statsd: new TestStatsdManager(statsd.Object),
                maxBufferSize: SpanBufferMessagePackSerializer.HeaderSizeConst,
                initialTracerMetricsEnabled: true);

            WriteTraceAndWait(agent, CreateTraceChunk(1));

            agent.FrontBuffer.IsEmpty.Should().BeTrue();
            agent.BackBuffer.IsEmpty.Should().BeTrue();
            agent.DroppedTracesBufferFull.Should().Be(0);
            agent.DroppedTracesTooLarge.Should().Be(1);

            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedSpans, 1, 1, null), Times.Once);
            statsd.VerifyNoOtherCalls();

            await agent.FlushTracesAsync();

            agent.DroppedTracesBufferFull.Should().Be(0);
            agent.DroppedTracesTooLarge.Should().Be(0);

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public Task WakeUpSerializationTask()
        {
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd: TestStatsdManager.NoOp, batchInterval: 0);

            // To reduce flakiness, first we make sure the serialization thread is started
            WaitForDequeue(agent);

            // Wait for the serialization thread to go to sleep
            while (true)
            {
                if (!WaitForDequeue(agent, wakeUpThread: false, delay: 500))
                {
                    break;
                }
            }

            // Serialization thread is asleep, makes sure it wakes up when enqueuing a trace
            agent.WriteTrace(CreateTraceChunk(1));
            WaitForDequeue(agent).Should().BeTrue();

            return agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task AddsTraceKeepRateMetricToRootSpan()
        {
            // Traces should be dropped when both buffers are full
            var calculator = new MovingAverageKeepRateCalculator(windowSize: 10, Timeout.InfiniteTimeSpan);

            var tracer = new Mock<IDatadogTracer>();
            tracer.Setup(x => x.DefaultServiceName).Returns("Default");
            tracer.Setup(x => x.PerTraceSettings).Returns(new PerTraceSettings(null, null, null!, MutableSettings.CreateWithoutDefaultSources(new(NullConfigurationSource.Instance), new ConfigurationTelemetry())));
            var traceContext = new TraceContext(tracer.Object);
            var rootSpanContext = new SpanContext(null, traceContext, null);
            var rootSpan = new Span(rootSpanContext, DateTimeOffset.UtcNow);
            var childSpan = new Span(new SpanContext(rootSpanContext, traceContext, null), DateTimeOffset.UtcNow);
            traceContext.AddSpan(rootSpan);
            traceContext.AddSpan(childSpan);
            var spans = new SpanCollection([rootSpan, childSpan]);
            var sizeOfTrace = ComputeSize(spans);

            // Make the buffer size big enough for a single trace
            var api = new MockApi();
            var agent = new AgentWriter(api, statsAggregator: null, statsd: TestStatsdManager.NoOp, calculator, automaticFlush: false, (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1, batchInterval: 0, apmTracingEnabled: true, initialTracerMetricsEnabled: false);

            // Fill both buffers
            WriteTraceAndWait(agent, spans);
            WriteTraceAndWait(agent, spans);

            // Drop one
            WriteTraceAndWait(agent, spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // Write another one
            agent.WriteTrace(spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API
            api.TraceCount.Should().Be(3);

            // Write trace and update keep rate
            calculator.UpdateBucket();
            agent.WriteTrace(spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            var traceChunk = new TraceChunkModel(spans);
            await agent.FlushAndCloseAsync();

            api.TraceCount.Should().Be(4); // previous value + 1
            api.Traces.Count.Should().Be(4);
            api.Traces.Last().Count.Should().Be(traceChunk.SpanCount);
        }

        [Fact]
        public async Task AgentWriterEnqueueFlushTasks()
        {
            // Flushes all run on the flush loop, one at a time, so flushes requested while one is in
            // flight wait for it. Every trace written below should still be sent, and every flush should
            // complete, once the blocked API call is released.
            var api = new Mock<IApi>();
            var agentWriter = AgentWriterHelper.CreateWithManualFlush(api.Object);
            var flushTcs = new TaskCompletionSource<bool>();
            var firstSendEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invocation = 0;
            var sentTraces = 0;

            api.Setup(i => i.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Returns((ArraySegment<byte> _, int numberOfTraces, bool _, long _, long _, bool _) =>
                {
                    Interlocked.Add(ref sentTraces, numberOfTraces);

                    // The first send blocks until we release it below; the flush loop is stuck in it,
                    // holding the front buffer locked.
                    if (Interlocked.Increment(ref invocation) == 1)
                    {
                        firstSendEntered.TrySetResult(true);
                        return flushTcs.Task;
                    }

                    return Task.FromResult(true);
                });

            var spans = CreateTraceChunk(1);

            // Write trace to the front buffer
            agentWriter.WriteTrace(spans);

            // Flush the front buffer. This blocks inside SendTracesAsync, so the front buffer
            // stays locked until we complete flushTcs.
            var firstFlush = agentWriter.FlushTracesAsync();
            await firstSendEntered.Task;

            // The front buffer is locked, so this swaps to the back buffer.
            agentWriter.WriteTrace(spans);

            // Queues up behind the in-flight flush.
            var secondFlush = agentWriter.FlushTracesAsync();

            // The back buffer is still unlocked, so this is written to it.
            agentWriter.WriteTrace(spans);

            // Also queues up behind the in-flight flush, and is batched with the second one.
            var thirdFlush = agentWriter.FlushTracesAsync();

            // None of the flushes can complete while the first send is blocked.
            var completed = await Task.WhenAny(thirdFlush, Task.Delay(TimeSpan.FromMilliseconds(100)));
            completed.Should().NotBeSameAs(thirdFlush);
            firstFlush.IsCompleted.Should().BeFalse();
            secondFlush.IsCompleted.Should().BeFalse();

            // Unblock the API so everything can drain and the writer can shut down.
            flushTcs.TrySetResult(true);
            await Task.WhenAll(firstFlush, secondFlush, thirdFlush).WaitAsync(TimeSpan.FromMilliseconds(30000));

            // Note that we can't assert on the DroppedTraces* counters here, because a flush resets them.
            // All three traces reaching the API is what proves none of them were dropped.
            Volatile.Read(ref sentTraces).Should().Be(3);

            await agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task WriteTrace_AfterFlushAndClose_DropsTheTrace()
        {
            // The serialization loop has stopped, so nothing would ever dequeue the trace. It has to
            // be dropped, otherwise it sits in the queue for the lifetime of the process.
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd: TestStatsdManager.NoOp, batchInterval: 0);

            await agent.FlushAndCloseAsync();

            agent.WriteTrace(CreateTraceChunk(1));

            // Nothing resets the counter, because the final flush ran before the write above
            agent.DroppedTracesBufferFull.Should().Be(1);
        }

        [Fact]
        public async Task ConcurrentFlushTracesAsync_NeverRunsMoreThanOneFlushAtATime()
        {
            // Buffers are only ever locked and flushed by the flush loop, so no matter how many callers
            // are flushing concurrently, a buffer is never sent while another send is in flight.
            var api = new Mock<IApi>();
            var concurrentSends = 0;
            var maxConcurrentSends = 0;
            var maxLock = new object();

            api.Setup(i => i.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Returns(async () =>
                {
                    var current = Interlocked.Increment(ref concurrentSends);

                    lock (maxLock)
                    {
                        maxConcurrentSends = Math.Max(maxConcurrentSends, current);
                    }

                    // Give any other flush a chance to overlap with this one, but not too big,
                    // otherwise could cause flake
                    await Task.Delay(5);

                    Interlocked.Decrement(ref concurrentSends);
                    return true;
                });

            // Buffers big enough for a single trace, so that they fill up and the active buffer keeps
            // switching while flushes are in flight. Background flushes are enabled too, so those must
            // not overlap with the requested ones either.
            var sizeOfTrace = ComputeSize(CreateTraceChunk(1));
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp, maxBufferSize: (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1, batchInterval: 0);

            var flushes = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                agent.WriteTrace(CreateTraceChunk(1));
                flushes.Add(agent.FlushTracesAsync());
            }

            await Task.WhenAll(flushes).WaitAsync(TimeSpan.FromMilliseconds(30000));

            lock (maxLock)
            {
                maxConcurrentSends.Should().Be(1);
            }

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTracesAsync_SendsTracesWrittenOnTheSameThread()
        {
            // A trace written before FlushTracesAsync() is called must have left the pending queue and be
            // a candidate for that flush, however many times we go around.
            var api = new MockApi();
            var agent = new AgentWriter(api, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false, batchInterval: 0);

            for (var i = 1; i <= 20; i++)
            {
                agent.WriteTrace(CreateTraceChunk(1, startingId: (ulong)i));
                await agent.FlushTracesAsync();

                api.Traces.Should().HaveCount(i);
            }

            await agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTracesAsync_AfterFlushAndClose_DoesNotHang()
        {
            // The flush loop has already performed its final flush and stopped, so there's nothing left
            // to flush, and nothing to wait for
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd: TestStatsdManager.NoOp, batchInterval: 0);

            agent.WriteTrace(CreateTraceChunk(1));
            await agent.FlushAndCloseAsync();

            await agent.FlushTracesAsync().WaitAsync(TimeSpan.FromMilliseconds(30000));
        }

        [Fact]
        public async Task FlushTracesAsync_DuringFinalFlush_DoesNotHang()
        {
            // A request that arrives after the final pass took its snapshot of _pendingFlushRequest
            // can't be completed by that pass, so only the flush loop's finally block can complete it.
            var api = new Mock<IApi>();
            var gate = new TaskCompletionSource<bool>();
            var firstSendEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invocations = 0;

            api.Setup(i => i.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    if (Interlocked.Increment(ref invocations) == 1)
                    {
                        firstSendEntered.TrySetResult(true);
                        return gate.Task;
                    }

                    return Task.FromResult(true);
                });

            // No background flushes, so the only send that can happen is the final pass's
            var agent = AgentWriterHelper.CreateWithManualFlush(api.Object);
            agent.WriteTrace(CreateTraceChunk(1));

            // Serialization stops, so the flush loop starts its final pass, which blocks in the API
            var closing = agent.FlushAndCloseAsync();
            await firstSendEntered.Task;

            var flush = agent.FlushTracesAsync();

            gate.SetResult(true);

            await flush.WaitAsync(TimeSpan.FromMilliseconds(30000));
            await closing.WaitAsync(TimeSpan.FromMilliseconds(30000));
        }

        /// <summary>
        /// Writes a trace and blocks until the serialization thread has serialized it. The
        /// watermark is enqueued after the trace and the queue has a single consumer, so the
        /// callback firing proves the trace has been written to a buffer.
        /// </summary>
        private static void WriteTraceAndWait(AgentWriter agent, SpanCollection trace)
        {
            agent.WriteTrace(trace);

            WaitForDequeue(agent, delay: 30_000)
                .Should().BeTrue("the serialization thread should have serialized the trace");
        }

        private static bool WaitForDequeue(AgentWriter agent, bool wakeUpThread = true, int delay = -1)
        {
            using var mutex = new ManualResetEventSlim();

            agent.WriteWatermark(() => mutex.Set(), wakeUpThread);

            return mutex.Wait(delay);
        }

        /// <summary>
        /// Takes a copy of the payload handed to the API. Copying matters: once the send completes,
        /// the flush loop recycles that array as a buffer's backing store, so the bytes are only
        /// guaranteed to be intact for the duration of the call. Deliberately assertion-free —
        /// it runs on the flush thread, where a failed assertion would be swallowed rather than
        /// failing the test.
        /// </summary>
        private static byte[] CopyPayload(ArraySegment<byte> data)
        {
            if (data.Array is null)
            {
                return [];
            }

            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            return copy;
        }

        private static void AssertPayloadEqual(byte[] data, byte[] expectedData)
        {
            data.Length.Should().BeGreaterOrEqualTo(SpanBufferMessagePackSerializer.HeaderSizeConst);

            data.Skip(SpanBufferMessagePackSerializer.HeaderSizeConst)
                .Should().Equal(expectedData);
        }

        private static int ComputeSize(SpanCollection spans)
        {
            var traceChunk = new TraceChunkModel(spans);
            return Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance).Length;
        }

        private static SpanCollection CreateTraceChunk(int spanCount, ulong startingId = 1)
        {
            var spans = new Span[spanCount];

            for (ulong i = 0; i < (ulong)spanCount; i++)
            {
                var spanContext = new SpanContext(startingId + i, startingId + i);
                spans[i] = new Span(spanContext, DateTimeOffset.UtcNow);
            }

            return new SpanCollection(spans);
        }

        private static TracerSettings SpanSamplingRule(string serviceName, string operationName, float sampleRate = 1.0f)
        {
            var rules = new SpanSamplingRule.SpanSamplingRuleConfig[]
            {
                new()
                {
                    ServiceNameGlob = serviceName,
                    OperationNameGlob = operationName,
                    SampleRate = sampleRate
                }
            };

            return TracerSettings.Create(new() { { ConfigurationKeys.SpanSamplingRules, JsonConvert.SerializeObject(rules) } });
        }

        internal class StubStatsAggregator(bool shouldKeepTrace, Func<SpanCollection, SpanCollection> processTrace) : IStatsAggregator
        {
            public List<SpanCollection> AddedSpans { get; } = new();

            public bool? CanComputeStats => true;

            public void Add(params Span[] spans) => AddRange(new SpanCollection(spans));

            public void AddRange(in SpanCollection spans)
            {
                AddedSpans.Add(spans);
            }

            public TraceKeepState ProcessTrace(ref SpanCollection spans)
            {
                spans = processTrace(spans);
                return shouldKeepTrace ? TraceKeepState.AggregateAndExport : TraceKeepState.AggregateOnly;
            }

            public Task DisposeAsync() => Task.CompletedTask;

            public StatsAggregationKey BuildKey(Span span) => new();
        }
    }
}
