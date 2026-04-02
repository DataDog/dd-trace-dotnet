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
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Agent
{
    public class AgentWriterTests
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

        [Fact]
        public async Task SpanSampling_CanComputeStats_ShouldNotSend_WhenSpanSamplingDoesNotMatch()
        {
            var api = new Mock<IApi>();
            var settings = SpanSamplingRule("*", "*", 0.0f); // don't sample any rule
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var agent = new AgentWriter(api.Object, statsAggregator, statsd: TestStatsdManager.NoOp, automaticFlush: false);

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

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public async Task SpanSampling_ShouldSend_SingleMatchedSpan_WhenStatsDrops()
        {
            var api = new Mock<IApi>();
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var settings = SpanSamplingRule("*", "*");
            var agent = new AgentWriter(api.Object, statsAggregator, statsd: TestStatsdManager.NoOp, automaticFlush: false);
            await using var tracer = TracerHelper.Create(settings, agent, sampler: null, scopeManager: null, statsd: null);

            var traceContext = new TraceContext(tracer);
            var spanContext = new SpanContext(null, traceContext, "service");
            var span = new Span(spanContext, DateTimeOffset.UtcNow) { OperationName = "operation" };
            traceContext.AddSpan(span);
            traceContext.SetSamplingPriority(priority: SamplingPriorityValues.UserReject, mechanism: SamplingMechanism.Manual, rate: null, limiterRate: null);
            span.Finish();
            var traceChunk = new SpanCollection([span]);
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
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);
            var settings = SpanSamplingRule("*", "*");
            var agent = new AgentWriter(api.Object, statsAggregator, statsd: TestStatsdManager.NoOp, automaticFlush: false);
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
            // var size = ComputeSize(expectedChunk);
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
            var api = new MockApi();
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => x);

            var settings = SpanSamplingRule("*", "operation");
            var agentWriter = new AgentWriter(api, statsAggregator, statsd: TestStatsdManager.NoOp, automaticFlush: false);
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

            await _agentWriter.FlushAndCloseAsync();
        }

        [Fact]
        public void PushStats()
        {
            var spans = CreateTraceChunk(1);
            var statsAggregator = new StubStatsAggregator(shouldKeepTrace: false, x => spans);
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator, statsd: TestStatsdManager.NoOp, automaticFlush: false);

            agent.WriteTrace(spans);

            statsAggregator.AddedSpans.Should().Contain(spans).Which.Count.Should().Be(1);
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
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);

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
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp);

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
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false, maxBufferSize: (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1);

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
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, new TestStatsdManager(statsd.Object), automaticFlush: false, (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1, initialTracerMetricsEnabled: true);

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
            var agent = new AgentWriter(api, statsAggregator: null, statsd: TestStatsdManager.NoOp, calculator, automaticFlush: false, (sizeOfTrace * 2) + SpanBufferMessagePackSerializer.HeaderSizeConst - 1, batchInterval: 100, apmTracingEnabled: true, initialTracerMetricsEnabled: false);

            // Fill both buffers
            agent.WriteTrace(spans);
            agent.WriteTrace(spans);

            // Drop one
            agent.WriteTrace(spans);
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
        public void AgentWriterEnqueueFlushTasks()
        {
            var api = new Mock<IApi>();
            var agentWriter = new AgentWriter(api.Object, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
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
            _ = agentWriter.FlushTracesAsync();

            // This will swap to the back buffer due front buffer is blocked.
            agentWriter.WriteTrace(spans);

            // Flush the second buffer
            _ = agentWriter.FlushTracesAsync();

            // This trace will force other buffer swap and then a drop because both buffers are blocked
            agentWriter.WriteTrace(spans);

            // This will try to flush the front buffer again.
            var thirdFlush = agentWriter.FlushTracesAsync();

            // Third flush should wait for the first flush to complete.
            thirdFlush.IsCompleted.Should().BeFalse();
        }

        private static bool WaitForDequeue(AgentWriter agent, bool wakeUpThread = true, int delay = -1)
        {
            using var mutex = new ManualResetEventSlim();

            agent.WriteWatermark(() => mutex.Set(), wakeUpThread);

            return mutex.Wait(delay);
        }

        private static bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            var equals = data.Array!.Skip(data.Offset).Take(data.Count).Skip(SpanBufferMessagePackSerializer.HeaderSizeConst).SequenceEqual(expectedData);
            return equals;
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

            public bool ShouldKeepTrace(in SpanCollection spans) => shouldKeepTrace;

            public SpanCollection ProcessTrace(in SpanCollection trace) => processTrace(trace);

            public Task DisposeAsync() => Task.CompletedTask;
        }
    }
}
