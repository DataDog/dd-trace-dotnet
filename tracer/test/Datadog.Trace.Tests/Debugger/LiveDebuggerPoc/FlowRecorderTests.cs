// <copyright file="FlowRecorderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.LiveDebuggerPoc;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.LiveDebuggerPoc
{
    public class FlowRecorderTests
    {
        [Fact]
        public void EnterExit_WhenDisabled_DoesNotRecordEvents()
        {
            FlowRecorder.ConfigureForTesting(enabled: false);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state);

            state.IsValid.Should().BeFalse();
            FlowRecorder.DrainForTesting().Should().BeEmpty();
            FlowRecorder.DroppedEvents.Should().Be(0);
        }

        [Fact]
        public void EnterExit_RecordsBalancedFrameEvents()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state);

            var events = FlowRecorder.DrainForTesting();
            events.Should().HaveCount(2);
            events[0].Kind.Should().Be(FlowEventKind.Enter);
            events[1].Kind.Should().Be(FlowEventKind.Exit);
            events[0].FlowId.Should().Be(events[1].FlowId);
            events[0].FrameId.Should().Be(events[1].FrameId);
            events[0].ParentFrameId.Should().Be(0);
            events[0].Depth.Should().Be(1);
            events[0].MethodMetadataIndex.Should().Be(42);
        }

        [Fact]
        public void NestedEnterExit_RecordsParentChildEdges()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var parent = FlowRecorder.Enter(methodMetadataIndex: 1);
            var child = FlowRecorder.Enter(methodMetadataIndex: 2);
            FlowRecorder.Exit(ref child);
            FlowRecorder.Exit(ref parent);

            var events = FlowRecorder.DrainForTesting();
            events.Should().HaveCount(4);
            events[1].Kind.Should().Be(FlowEventKind.Enter);
            events[1].FlowId.Should().Be(events[0].FlowId);
            events[1].ParentFrameId.Should().Be(events[0].FrameId);
            events[1].Depth.Should().Be(2);
            events[2].FrameId.Should().Be(events[1].FrameId);
            events[3].FrameId.Should().Be(events[0].FrameId);
        }

        [Fact]
        public async Task AsyncContinuation_PreservesFlowContext()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var parent = FlowRecorder.Enter(methodMetadataIndex: 1);
            await Task.Yield();
            var child = FlowRecorder.Enter(methodMetadataIndex: 2);
            FlowRecorder.Exit(ref child);
            FlowRecorder.Exit(ref parent);

            var events = FlowRecorder.DrainForTesting();
            events[1].FlowId.Should().Be(events[0].FlowId);
            events[1].ParentFrameId.Should().Be(events[0].FrameId);
        }

        [Fact]
        public async Task ParallelAsyncBranches_DoNotMutateSharedFlowContext()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var parent = FlowRecorder.Enter(methodMetadataIndex: 1);
            var barrier = new Barrier(participantCount: 3);

            var first = Task.Run(() => RecordChildAfterBarrier(methodMetadataIndex: 2, barrier));
            var second = Task.Run(() => RecordChildAfterBarrier(methodMetadataIndex: 3, barrier));
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            await Task.WhenAll(first, second);
            FlowRecorder.Exit(ref parent);

            var events = FlowRecorder.DrainForTesting();
            var childEnterEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter && flowEvent.MethodMetadataIndex is 2 or 3).ToArray();
            childEnterEvents.Should().HaveCount(2);
            childEnterEvents.Should().OnlyContain(flowEvent => flowEvent.ParentFrameId == events[0].FrameId);
            childEnterEvents.Should().OnlyContain(flowEvent => flowEvent.Depth == 2);
        }

        [Fact]
        public void ExitAfterReset_DoesNotRecordStaleExit()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            var staleState = FlowRecorder.Enter(methodMetadataIndex: 42);

            FlowRecorder.ConfigureForTesting(enabled: true);
            FlowRecorder.Exit(ref staleState);

            FlowRecorder.DrainForTesting().Should().BeEmpty();
        }

        [Fact]
        public void Exit_WithException_RecordsExceptionEvent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state, new InvalidOperationException());

            var events = FlowRecorder.DrainForTesting();
            events.Should().HaveCount(3);
            events[1].Kind.Should().Be(FlowEventKind.Exception);
            events[1].ExceptionTypeId.Should().NotBe(0);
            events[2].Kind.Should().Be(FlowEventKind.Exit);
        }

        [Fact]
        public void BufferLimit_DropsEventsInsteadOfBlocking()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, bufferSize: 1);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state);

            var events = FlowRecorder.DrainForTesting();
            events.Should().ContainSingle();
            FlowRecorder.DroppedEvents.Should().Be(1);
        }

        [Fact]
        public void BinaryFormat_RoundTripsEvents()
        {
            var events = new[]
            {
                new FlowEvent(FlowEventKind.Enter, timestamp: 1, methodMetadataIndex: 2, flowId: 3, frameId: 4, parentFrameId: 5, depth: 6, threadId: 7, traceIdUpper: 8, traceIdLower: 9, rootSpanId: 10, activeSpanId: 11, exceptionTypeId: 12),
                new FlowEvent(FlowEventKind.Exit, timestamp: 13, methodMetadataIndex: 14, flowId: 15, frameId: 16, parentFrameId: 17, depth: 18, threadId: 19, traceIdUpper: 20, traceIdLower: 21, rootSpanId: 22, activeSpanId: 23, exceptionTypeId: 24),
            };

            using var stream = new MemoryStream();
            FlowEventBinaryFormat.Write(stream, events);
            stream.Position = 0;

            var roundTripped = FlowEventBinaryFormat.Read(stream);
            roundTripped.Should().BeEquivalentTo(events);
        }

        private static void RecordChildAfterBarrier(int methodMetadataIndex, Barrier barrier)
        {
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            var state = FlowRecorder.Enter(methodMetadataIndex);
            FlowRecorder.Exit(ref state);
        }
    }
}
