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
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class AgentWriterTests
    {
        private readonly AgentWriter _agentWriter;
        private readonly Mock<IApi> _api;

        public AgentWriterTests()
        {
            _api = new Mock<IApi>();
            _agentWriter = new AgentWriter(_api.Object, statsAggregator: null, statsd: null);
        }

        [Fact]
        public async Task SpanSampling_CanComputeStats_ShouldNotSend_WhenSpanSamplingDoesNotMatch()
        {
            var api = new Mock<IApi>();
            var settings = SpanSamplingRule("*", "*", 0.0f); // don't sample any rule
            var statsAggregator = new Mock<IStatsAggregator>();
            statsAggregator.Setup(x => x.CanComputeStats).Returns(true);
            statsAggregator.Setup(x => x.ProcessTrace(It.IsAny<ArraySegment<Span>>())).Returns<ArraySegment<Span>>(x => x);
            statsAggregator.Setup(x => x.ShouldKeepTrace(It.IsAny<ArraySegment<Span>>())).Returns(false);
            var agent = new AgentWriter(api.Object, statsAggregator.Object, statsd: null, automaticFlush: false);

            var tracer = new Tracer(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            span.Finish(); // triggers the span sampler to run
            var traceChunk = new ArraySegment<Span>(new[] { span });

            agent.WriteTrace(traceChunk);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            api.Verify(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Never);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_SingleMatchedSpan_WhenStatsDrops()
        {
            var api = new Mock<IApi>();
            var statsAggregator = new Mock<IStatsAggregator>();
            statsAggregator.Setup(x => x.CanComputeStats).Returns(true);
            statsAggregator.Setup(x => x.ProcessTrace(It.IsAny<ArraySegment<Span>>())).Returns<ArraySegment<Span>>(x => x);
            statsAggregator.Setup(x => x.ShouldKeepTrace(It.IsAny<ArraySegment<Span>>())).Returns(false);
            var settings = SpanSamplingRule("*", "*");
            var agent = new AgentWriter(api.Object, statsAggregator.Object, statsd: null, automaticFlush: false);
            var tracer = new Tracer(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            span.Finish();
            var traceChunk = new ArraySegment<Span>(new[] { span });
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(new TraceChunkModel(traceChunk, SamplingPriorityValues.UserKeep), SpanFormatterResolver.Instance);

            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            var expectedDroppedP0Traces = 1;
            var expectedDroppedP0Spans = 0;

            api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.Is<long>(i => i == expectedDroppedP0Traces), It.Is<long>(i => i == expectedDroppedP0Spans), It.IsAny<bool>()), Times.Once);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_MultipleMatchedSpans_WhenStatsDrops()
        {
            var api = new Mock<IApi>();
            var statsAggregator = new Mock<IStatsAggregator>();
            statsAggregator.Setup(x => x.CanComputeStats).Returns(true);
            statsAggregator.Setup(x => x.ProcessTrace(It.IsAny<ArraySegment<Span>>())).Returns<ArraySegment<Span>>(x => x);
            statsAggregator.Setup(x => x.ShouldKeepTrace(It.IsAny<ArraySegment<Span>>())).Returns(false);
            var settings = SpanSamplingRule("*", "*");
            var agent = new AgentWriter(api.Object, statsAggregator.Object, statsd: null, automaticFlush: false);
            var tracer = new Tracer(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
            var rootSpanContext = new SpanContext(null, traceContext, "service");
            var rootSpan = new Span(rootSpanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            var keptChildSpan = new Span(new SpanContext(rootSpanContext, traceContext, "service"), DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(rootSpan); // IS single span sampled
            traceContext.AddSpan(keptChildSpan); // IS single span sampled

            rootSpan.Finish();
            keptChildSpan.Finish();

            var expectedChunk = new ArraySegment<Span>(new[] { rootSpan, keptChildSpan });
            var size = ComputeSize(expectedChunk);
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(new TraceChunkModel(expectedChunk, SamplingPriorityValues.UserKeep), SpanFormatterResolver.Instance);

            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            var expectedDroppedP0Traces = 1;
            var expectedDroppedP0Spans = 0;
            api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.Is<long>(i => i == expectedDroppedP0Traces), It.Is<long>(i => i == expectedDroppedP0Spans), It.IsAny<bool>()), Times.Once);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_MultipleMatchedSpans_WhenStatsDropsOne()
        {
            var api = new Mock<IApi>();
            var statsAggregator = new Mock<IStatsAggregator>();
            statsAggregator.Setup(x => x.CanComputeStats).Returns(true);
            statsAggregator.Setup(x => x.ProcessTrace(It.IsAny<ArraySegment<Span>>())).Returns<ArraySegment<Span>>(x => x);
            statsAggregator.Setup(x => x.ShouldKeepTrace(It.IsAny<ArraySegment<Span>>())).Returns(false);
            var settings = SpanSamplingRule("*", "operation");
            var agent = new AgentWriter(api.Object, statsAggregator.Object, statsd: null, automaticFlush: false);
            var tracer = new Tracer(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            traceContext.SetSamplingPriority(SamplingPriorityValues.UserReject, SamplingMechanism.Manual);
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

            rootSpan.SetMetric(Metrics.TracesKeepRate, 0);

            // create a trace chunk so that our array has an offset
            var unusedSpans = CreateTraceChunk(5, 10);
            var spanList = new List<Span>();
            spanList.AddRange(unusedSpans.Array);
            spanList.AddRange(new[] { rootSpan, droppedChildSpan, droppedChildSpan2, keptChildSpan });
            var spans = spanList.ToArray();

            var traceChunk = new ArraySegment<Span>(spans, 5, 4);
            var expectedChunk = new ArraySegment<Span>(new[] { rootSpan, keptChildSpan });
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(new TraceChunkModel(expectedChunk, SamplingPriorityValues.UserKeep), SpanFormatterResolver.Instance);

            agent.WriteTrace(traceChunk);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            var expectedDroppedP0Traces = 1;
            var expectedDroppedP0Spans = 2;
            // expecting a single trace, but there should have been two spans
            api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.Is<long>(i => i == expectedDroppedP0Traces), It.Is<long>(i => i == expectedDroppedP0Spans), It.IsAny<bool>()), Times.Once);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public void PushStats()
        {
            var statsAggregator = new Mock<IStatsAggregator>();
            var spans = CreateTraceChunk(1);
            statsAggregator.Setup(x => x.ProcessTrace(spans)).Returns(spans);
            statsAggregator.Setup(x => x.CanComputeStats).Returns(true);

            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator.Object, statsd: null, automaticFlush: false);

            agent.WriteTrace(spans);

            statsAggregator.Verify(s => s.AddRange(It.Is<ArraySegment<Span>>(x => x.Offset == 0 && x.Count == 1)), Times.Once);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var spans = CreateTraceChunk(1);
            var traceChunk = new TraceChunkModel(spans);
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(spans);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);

            _api.Invocations.Clear();

            spans = CreateTraceChunk(1, 2);
            traceChunk = new TraceChunkModel(spans);
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(spans);
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData2)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushTwice()
        {
            var w = new AgentWriter(_api.Object, statsAggregator: null, statsd: null);
            await w.FlushAndCloseAsync();
            await w.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FaultyApi()
        {
            // The flush thread should be able to recover from an error when calling the API
            // Also, it should free the faulty buffer
            var api = new Mock<IApi>();
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null, automaticFlush: false);

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
        public Task SwitchBuffer()
        {
            // Make sure that the agent is able to switch to the secondary buffer when the primary is full/busy
            var api = new Mock<IApi>();
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null);

            var barrier = new Barrier(2);

            api.Setup(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Callback(() =>
                {
                    barrier.SignalAndWait();
                    barrier.SignalAndWait();
                })
                .Returns(Task.FromResult(true));

            agent.WriteTrace(CreateTraceChunk(1));

            // Wait for the flush operation
            barrier.SignalAndWait();

            // At this point, the flush thread is stuck in Api.SendTracesAsync, and the frontBuffer should be active and locked
            agent.ActiveBuffer.Should().BeSameAs(agent.FrontBuffer);
            agent.FrontBuffer.IsLocked.Should().BeTrue();
            agent.FrontBuffer.TraceCount.Should().Be(1);
            agent.FrontBuffer.SpanCount.Should().Be(1);

            agent.WriteTrace(CreateTraceChunk(2));

            // Wait for the trace to be dequeued
            WaitForDequeue(agent);

            // Since the frontBuffer was locked, the buffers should have been swapped
            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);
            agent.BackBuffer.TraceCount.Should().Be(1);
            agent.BackBuffer.SpanCount.Should().Be(2);

            // Unblock the flush thread
            barrier.SignalAndWait();

            // Wait for the next flush operation
            barrier.SignalAndWait();

            // Back buffer should still be active and being flushed
            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);
            agent.BackBuffer.IsLocked.Should().BeTrue();
            agent.FrontBuffer.IsLocked.Should().BeFalse();

            // Unblock and exit
            barrier.Dispose();
            return agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushBothBuffers()
        {
            // When the back buffer is full, both buffers should be flushed
            var api = new Mock<IApi>();

            var sizeOfTrace = ComputeSize(CreateTraceChunk(1));

            // Make the buffer size big enough for a single trace
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null, automaticFlush: false, maxBufferSize: (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1);

            agent.WriteTrace(CreateTraceChunk(1));
            agent.WriteTrace(CreateTraceChunk(1));

            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);

            agent.FrontBuffer.IsFull.Should().BeTrue();
            agent.FrontBuffer.TraceCount.Should().Be(1);

            agent.BackBuffer.IsFull.Should().BeFalse();
            agent.BackBuffer.TraceCount.Should().Be(1);

            await agent.FlushTracesAsync();

            agent.FrontBuffer.IsEmpty.Should().BeTrue();
            agent.BackBuffer.IsEmpty.Should().BeTrue();

            api.Verify(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1, It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Exactly(2));
        }

        [Fact]
        public void DropTraces()
        {
            // Traces should be dropped when both buffers are full
            var statsd = new Mock<IDogStatsd>();

            var sizeOfTrace = ComputeSize(CreateTraceChunk(1));

            // Make the buffer size big enough for a single trace
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd.Object, automaticFlush: false, (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1);

            // Fill the two buffers
            agent.WriteTrace(CreateTraceChunk(1));
            agent.WriteTrace(CreateTraceChunk(1));

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
            agent.WriteTrace(CreateTraceChunk(2));

            // Buffers shouldn't have swapped since the reserve buffer was full
            agent.ActiveBuffer.Should().BeSameAs(agent.BackBuffer);

            // Both buffers should be full with 1 trace stored
            agent.FrontBuffer.IsFull.Should().BeTrue();
            agent.FrontBuffer.TraceCount.Should().Be(1);
            agent.FrontBuffer.SpanCount.Should().Be(1);

            agent.BackBuffer.IsFull.Should().BeTrue();
            agent.BackBuffer.TraceCount.Should().Be(1);
            agent.BackBuffer.SpanCount.Should().Be(1);

            // Dropped trace should have been reported to statsd
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 2, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedTraces, 1, 1, null), Times.Once);
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.DroppedSpans, 2, 1, null), Times.Once);
            statsd.VerifyNoOtherCalls();
        }

        [Fact]
        public Task WakeUpSerializationTask()
        {
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd: null, batchInterval: 0);

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
            var traceContext = new TraceContext(tracer.Object);
            var rootSpanContext = new SpanContext(null, traceContext, null);
            var rootSpan = new Span(rootSpanContext, DateTimeOffset.UtcNow);
            var childSpan = new Span(new SpanContext(rootSpanContext, traceContext, null), DateTimeOffset.UtcNow);
            traceContext.AddSpan(rootSpan);
            traceContext.AddSpan(childSpan);
            var spans = new ArraySegment<Span>(new[] { rootSpan, childSpan });
            var sizeOfTrace = ComputeSize(spans);

            // Make the buffer size big enough for a single trace
            var api = new Mock<IApi>();
            api.Setup(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
               .ReturnsAsync(() => true);
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null, calculator, automaticFlush: false, maxBufferSize: (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1, batchInterval: 100, appsecStandaloneEnabled: false);

            // Fill both buffers
            agent.WriteTrace(spans);
            agent.WriteTrace(spans);

            // Drop one
            agent.WriteTrace(spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // Write another one
            agent.WriteTrace(spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API
            api.Verify();
            api.Invocations.Clear();

            // Write trace and update keep rate
            calculator.UpdateBucket();
            agent.WriteTrace(spans);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            const double expectedTraceKeepRate = 0.75;
            rootSpan.SetMetric(Metrics.TracesKeepRate, expectedTraceKeepRate);

            var traceChunk = new TraceChunkModel(spans);
            var expectedData = Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance);
            await agent.FlushAndCloseAsync();

            api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData)), It.Is<int>(i => i == 1), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void AgentWriterEnqueueFlushTasks()
        {
            var api = new Mock<IApi>();
            var agentWriter = new AgentWriter(api.Object, statsAggregator: null, statsd: null, automaticFlush: false);
            var flushTcs = new TaskCompletionSource<bool>();
            int invocation = 0;

            api.Setup(i => i.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    // One for the front buffer, one for the back buffer
                    if (Interlocked.Increment(ref invocation) <= 2)
                    {
                        return flushTcs.Task;
                    }

                    return Task.FromResult(true);
                });

            var spans = CreateTraceChunk(1);

            // Write trace to the front buffer
            agentWriter.WriteTrace(spans);

            // Flush front buffer
            var firstFlush = agentWriter.FlushTracesAsync();

            // This will swap to the back buffer due front buffer is blocked.
            agentWriter.WriteTrace(spans);

            // Flush the second buffer
            var secondFlush = agentWriter.FlushTracesAsync();

            // This trace will force other buffer swap and then a drop because both buffers are blocked
            agentWriter.WriteTrace(spans);

            // This will try to flush the front buffer again.
            var thirdFlush = agentWriter.FlushTracesAsync();

            // Third flush should wait for the first flush to complete.
            thirdFlush.IsCompleted.Should().BeFalse();
        }

        private static bool WaitForDequeue(AgentWriter agent, bool wakeUpThread = true, int delay = -1)
        {
            var mutex = new ManualResetEventSlim();

            agent.WriteWatermark(() => mutex.Set(), wakeUpThread);

            return mutex.Wait(delay);
        }

        private static bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            var equals = data.Array!.Skip(data.Offset).Take(data.Count).Skip(SpanBuffer.HeaderSize).SequenceEqual(expectedData);
            return equals;
        }

        private static int ComputeSize(ArraySegment<Span> spans)
        {
            var traceChunk = new TraceChunkModel(spans);
            return Vendors.MessagePack.MessagePackSerializer.Serialize(traceChunk, SpanFormatterResolver.Instance).Length;
        }

        private static ArraySegment<Span> CreateTraceChunk(int spanCount, ulong startingId = 1)
        {
            var spans = new Span[spanCount];

            for (ulong i = 0; i < (ulong)spanCount; i++)
            {
                var spanContext = new SpanContext(startingId + i, startingId + i);
                spans[i] = new Span(spanContext, DateTimeOffset.UtcNow);
            }

            return new ArraySegment<Span>(spans);
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
    }
}
