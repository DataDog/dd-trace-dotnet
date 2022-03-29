// <copyright file="AgentWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class AgentWriterTests
    {
        private readonly AgentWriter _agentWriter;
        private readonly Mock<IApi> _api;

        public AgentWriterTests()
        {
            var tracer = new Mock<IDatadogTracer>();
            tracer.Setup(x => x.DefaultServiceName).Returns("Default");

            _api = new Mock<IApi>();
            _agentWriter = new AgentWriter(_api.Object, statsAggregator: null, statsd: null);
        }

        [Fact]
        public void PushStats()
        {
            var statsAggregator = new Mock<IStatsAggregator>();

            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator.Object, statsd: null, automaticFlush: false);

            agent.WriteTrace(CreateTrace(1));

            statsAggregator.Verify(s => s.AddRange(It.IsAny<Span[]>(), 0, 1), Times.Once);
        }

        [Fact]
        public async Task WriteTrace_2Traces_SendToApi()
        {
            var trace = new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) };
            var expectedData1 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(new ArraySegment<Span>(trace));
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData1)), It.Is<int>(i => i == 1)), Times.Once);

            _api.Invocations.Clear();

            trace = new[] { new Span(new SpanContext(2, 2), DateTimeOffset.UtcNow) };
            var expectedData2 = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance);

            _agentWriter.WriteTrace(new ArraySegment<Span>(trace));
            await _agentWriter.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            _api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData2)), It.Is<int>(i => i == 1)), Times.Once);

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
        public Task FaultyApi()
        {
            // The flush thread should be able to recover from an error when calling the API
            // Also, it should free the faulty buffer
            var api = new Mock<IApi>();
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null);

            var mutex = new ManualResetEventSlim();

            agent.Flushed += () => mutex.Set();

            api.Setup(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>()))
                .Returns(() =>
                {
                    throw new InvalidOperationException();
                });

            agent.WriteTrace(CreateTrace(1));

            mutex.Wait();

            Assert.True(agent.ActiveBuffer == agent.FrontBuffer);
            Assert.True(agent.FrontBuffer.IsEmpty);
            Assert.True(agent.BackBuffer.IsEmpty);

            return agent.FlushAndCloseAsync();
        }

        [Fact]
        public Task SwitchBuffer()
        {
            // Make sure that the agent is able to switch to the secondary buffer when the primary is full/busy
            var api = new Mock<IApi>();
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null);

            var barrier = new Barrier(2);

            api.Setup(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>()))
                .Callback(() =>
                {
                    barrier.SignalAndWait();
                    barrier.SignalAndWait();
                })
                .Returns(Task.FromResult(true));

            agent.WriteTrace(CreateTrace(1));

            // Wait for the flush operation
            barrier.SignalAndWait();

            // At this point, the flush thread is stuck in Api.SendTracesAsync, and the frontBuffer should be active and locked
            Assert.True(agent.ActiveBuffer == agent.FrontBuffer);
            Assert.True(agent.FrontBuffer.IsLocked);
            Assert.Equal(1, agent.FrontBuffer.TraceCount);
            Assert.Equal(1, agent.FrontBuffer.SpanCount);

            var mutex = new ManualResetEventSlim();

            agent.WriteTrace(CreateTrace(2));

            // Wait for the trace to be dequeued
            WaitForDequeue(agent);

            // Since the frontBuffer was locked, the buffers should have been swapped
            Assert.True(agent.ActiveBuffer == agent.BackBuffer);
            Assert.Equal(1, agent.BackBuffer.TraceCount);
            Assert.Equal(2, agent.BackBuffer.SpanCount);

            // Unblock the flush thread
            barrier.SignalAndWait();

            // Wait for the next flush operation
            barrier.SignalAndWait();

            // Back buffer should still be active and being flushed
            Assert.True(agent.ActiveBuffer == agent.BackBuffer);
            Assert.True(agent.BackBuffer.IsLocked);
            Assert.False(agent.FrontBuffer.IsLocked);

            // Unblock and exit
            barrier.Dispose();
            return agent.FlushAndCloseAsync();
        }

        [Fact]
        public async Task FlushBothBuffers()
        {
            // When the back buffer is full, both buffers should be flushed
            var api = new Mock<IApi>();

            var sizeOfTrace = ComputeSizeOfTrace(CreateTrace(1));

            // Make the buffer size big enough for a single trace
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null, automaticFlush: false, maxBufferSize: (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1);

            agent.WriteTrace(CreateTrace(1));
            agent.WriteTrace(CreateTrace(1));

            Assert.True(agent.ActiveBuffer == agent.BackBuffer);
            Assert.True(agent.FrontBuffer.IsFull);
            Assert.Equal(1, agent.FrontBuffer.TraceCount);
            Assert.False(agent.BackBuffer.IsFull);
            Assert.Equal(1, agent.BackBuffer.TraceCount);

            await agent.FlushTracesAsync();

            Assert.True(agent.FrontBuffer.IsEmpty);
            Assert.True(agent.BackBuffer.IsEmpty);

            api.Verify(a => a.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), 1), Times.Exactly(2));
        }

        [Fact]
        public void DropTraces()
        {
            // Traces should be dropped when both buffers are full
            var statsd = new Mock<IDogStatsd>();

            var sizeOfTrace = ComputeSizeOfTrace(CreateTrace(1));

            // Make the buffer size big enough for a single trace
            var agent = new AgentWriter(Mock.Of<IApi>(), statsAggregator: null, statsd.Object, automaticFlush: false, (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1);

            // Fill the two buffers
            agent.WriteTrace(CreateTrace(1));
            agent.WriteTrace(CreateTrace(1));

            // Buffers should have swapped
            Assert.True(agent.ActiveBuffer == agent.BackBuffer);

            // The agent does not know yet that the new active buffer is full
            Assert.False(agent.ActiveBuffer.IsFull);

            // Both buffers have 1 trace stored
            Assert.Equal(1, agent.FrontBuffer.TraceCount);
            Assert.Equal(1, agent.FrontBuffer.SpanCount);
            Assert.Equal(1, agent.BackBuffer.TraceCount);
            Assert.Equal(1, agent.BackBuffer.SpanCount);

            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedTraces, 1, 1, null), Times.Exactly(2));
            statsd.Verify(s => s.Increment(TracerMetricNames.Queue.EnqueuedSpans, 1, 1, null), Times.Exactly(2));
            statsd.VerifyNoOtherCalls();
            statsd.Invocations.Clear();

            // Both buffers are at capacity, write a new trace
            agent.WriteTrace(CreateTrace(2));

            // Buffers shouldn't have swapped since the reserve buffer was full
            Assert.True(agent.ActiveBuffer == agent.BackBuffer);

            // Both buffers should be full with 1 trace stored
            Assert.True(agent.FrontBuffer.IsFull);
            Assert.Equal(1, agent.FrontBuffer.TraceCount);
            Assert.Equal(1, agent.FrontBuffer.SpanCount);
            Assert.True(agent.BackBuffer.IsFull);
            Assert.Equal(1, agent.BackBuffer.TraceCount);
            Assert.Equal(1, agent.BackBuffer.SpanCount);

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

            // To reduce flackyness, first we make sure the serialization thread is started
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
            agent.WriteTrace(CreateTrace(1));
            Assert.True(WaitForDequeue(agent));

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
            var trace = new ArraySegment<Span>(new[] { rootSpan, childSpan });
            var sizeOfTrace = ComputeSizeOfTrace(trace);

            // Make the buffer size big enough for a single trace
            var api = new Mock<IApi>();
            api.Setup(x => x.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>()))
               .ReturnsAsync(() => true);
            var agent = new AgentWriter(api.Object, statsAggregator: null, statsd: null, calculator, automaticFlush: false, maxBufferSize: (sizeOfTrace * 2) + SpanBuffer.HeaderSize - 1, batchInterval: 100);

            // Fill both buffers
            agent.WriteTrace(trace);
            agent.WriteTrace(trace);

            // Drop one
            agent.WriteTrace(trace);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            // Write another one
            agent.WriteTrace(trace);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API
            api.Verify();
            api.Invocations.Clear();

            // Write trace and update keep rate
            calculator.UpdateBucket();
            agent.WriteTrace(trace);
            await agent.FlushTracesAsync(); // Force a flush to make sure the trace is written to the API

            const double expectedTraceKeepRate = 0.75;
            rootSpan.SetMetric(Metrics.TracesKeepRate, expectedTraceKeepRate);
            var expectedData = Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance);
            await agent.FlushAndCloseAsync();

            api.Verify(x => x.SendTracesAsync(It.Is<ArraySegment<byte>>(y => Equals(y, expectedData)), It.Is<int>(i => i == 1)), Times.Once);
        }

        [Fact]
        public void AgentWriterEnqueueFlushTasks()
        {
            var api = new Mock<IApi>();
            var agentWriter = new AgentWriter(api.Object, statsAggregator: null, statsd: null, automaticFlush: false);
            var flushTcs = new TaskCompletionSource<bool>();
            int invocation = 0;

            api.Setup(i => i.SendTracesAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<int>()))
                .Returns(() =>
                {
                    // One for the front buffer, one for the back buffer
                    if (Interlocked.Increment(ref invocation) <= 2)
                    {
                        return flushTcs.Task;
                    }

                    return Task.FromResult(true);
                });

            var trace = new ArraySegment<Span>(new[] { new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow) });

            // Write trace to the front buffer
            agentWriter.WriteTrace(trace);

            // Flush front buffer
            var firstFlush = agentWriter.FlushTracesAsync();

            // This will swap to the back buffer due front buffer is blocked.
            agentWriter.WriteTrace(trace);

            // Flush the second buffer
            var secondFlush = agentWriter.FlushTracesAsync();

            // This trace will force other buffer swap and then a drop because both buffers are blocked
            agentWriter.WriteTrace(trace);

            // This will try to flush the front buffer again.
            var thirdFlush = agentWriter.FlushTracesAsync();

            // Third flush should wait for the first flush to complete.
            Assert.False(thirdFlush.IsCompleted);
        }

        private static bool WaitForDequeue(AgentWriter agent, bool wakeUpThread = true, int delay = -1)
        {
            var mutex = new ManualResetEventSlim();

            agent.WriteWatermark(() => mutex.Set(), wakeUpThread);

            return mutex.Wait(delay);
        }

        private static bool Equals(ArraySegment<byte> data, byte[] expectedData)
        {
            return data.Array.Skip(data.Offset).Take(data.Count).Skip(SpanBuffer.HeaderSize).SequenceEqual(expectedData);
        }

        private static int ComputeSizeOfTrace(ArraySegment<Span> trace)
        {
            return Vendors.MessagePack.MessagePackSerializer.Serialize(trace, SpanFormatterResolver.Instance).Length;
        }

        private static ArraySegment<Span> CreateTrace(int numberOfSpans)
        {
            var array = Enumerable.Range(0, numberOfSpans)
                .Select(i => new Span(new SpanContext((ulong)i + 1, (ulong)i + 1), DateTimeOffset.UtcNow))
                .ToArray();

            return new ArraySegment<Span>(array);
        }
    }
}
