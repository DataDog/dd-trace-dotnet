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
        public void EnterExit_WhenNoOperationIsActive_DoesNotRecordEvents()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state);

            state.IsValid.Should().BeFalse();
            FlowRecorder.DrainForTesting().Should().BeEmpty();
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
        public void EnterFastExitFast_RecordsBalancedFrameEvents()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var state = FlowRecorder.EnterFast(methodMetadataIndex: 42);
            FlowRecorder.ExitFast(ref state);

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
        public void OperationScope_StampsEventsAndRecordsMetadata()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            using (FlowRecorder.StartOperation("manual-root", "Sample.Root"))
            {
                var state = FlowRecorder.EnterFast(methodMetadataIndex: 42);
                FlowRecorder.ExitFast(ref state);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
            file.Operations.Should().ContainSingle(operation => operation.OperationId == 1 && operation.TriggerReason == "manual-root" && operation.Root == "Sample.Root");
        }

        [Fact]
        public void StartConfiguredOperation_UsesFallbackRootMetadata()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            using (FlowRecorder.StartConfiguredOperation("logical-root", "Sample.ConfiguredRoot"))
            {
                var state = FlowRecorder.EnterFast(methodMetadataIndex: 42);
                FlowRecorder.ExitFast(ref state);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Operations.Should().ContainSingle(operation => operation.TriggerReason == "logical-root" && operation.Root == "Sample.ConfiguredRoot");
        }

        [Fact]
        public void OperationScope_DisposeRestoresPreviousOperation()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            using (FlowRecorder.StartOperation("outer", "Outer.Root"))
            {
                var outerBefore = FlowRecorder.EnterFast(methodMetadataIndex: 1);
                FlowRecorder.ExitFast(ref outerBefore);

                using (FlowRecorder.StartOperation("inner", "Inner.Root"))
                {
                    var inner = FlowRecorder.EnterFast(methodMetadataIndex: 2);
                    FlowRecorder.ExitFast(ref inner);
                }

                var outerAfter = FlowRecorder.EnterFast(methodMetadataIndex: 3);
                FlowRecorder.ExitFast(ref outerAfter);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Operations.Select(operation => operation.OperationId).Should().BeEquivalentTo(new ulong[] { 1, 2 });
            file.Events.Where(flowEvent => flowEvent.MethodMetadataIndex is 1 or 3).Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
            file.Events.Where(flowEvent => flowEvent.MethodMetadataIndex == 2).Should().OnlyContain(flowEvent => flowEvent.OperationId == 2);
        }

        [Fact]
        public async Task OperationScope_FlowsAcrossAwaitWithoutFrameContext()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            using (FlowRecorder.StartOperation("async-root", "Sample.AsyncRoot"))
            {
                await Task.Yield();
                var state = FlowRecorder.EnterFast(methodMetadataIndex: 42);
                FlowRecorder.ExitFast(ref state);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().HaveCount(2);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
            file.Events[0].ParentFrameId.Should().Be(0);
        }

        [Fact]
        public void OperationScope_DisposeStopsNewRecording()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            using (FlowRecorder.StartOperation("manual-root", "Sample.Root"))
            {
                var state = FlowRecorder.EnterFast(methodMetadataIndex: 1);
                FlowRecorder.ExitFast(ref state);
            }

            var afterDispose = FlowRecorder.EnterFast(methodMetadataIndex: 2);
            FlowRecorder.ExitFast(ref afterDispose);

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().HaveCount(2);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.MethodMetadataIndex == 1);
        }

        [Fact]
        public async Task OperationScope_DisposedCapturedContextDoesNotRecordWhenAnotherOperationIsActive()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);
            using var releaseWorker = new ManualResetEventSlim();
            Task worker;

            using (FlowRecorder.StartOperation("first", "First.Root"))
            {
                worker = Task.Run(() =>
                {
                    releaseWorker.Wait();
                    var stale = FlowRecorder.EnterFast(methodMetadataIndex: 2);
                    stale.IsValid.Should().BeFalse();
                    FlowRecorder.ExitFast(ref stale);
                });
                await Task.Yield();
            }

            using (FlowRecorder.StartOperation("second", "Second.Root"))
            {
                var active = FlowRecorder.EnterFast(methodMetadataIndex: 1);
                FlowRecorder.ExitFast(ref active);
                releaseWorker.Set();
                await worker;
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().OnlyContain(flowEvent => flowEvent.MethodMetadataIndex == 1);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 2);
        }

        [Fact]
        public void OperationScope_ConcurrentDisposeDoesNotCloseOtherOperationGate()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            using var outer = FlowRecorder.StartOperation("outer", "Outer.Root");
            var inner = FlowRecorder.StartOperation("inner", "Inner.Root");

            inner.Dispose();
            Parallel.Invoke(inner.Dispose, inner.Dispose);

            var outerState = FlowRecorder.EnterFast(methodMetadataIndex: 1);
            FlowRecorder.ExitFast(ref outerState);

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().HaveCount(2);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
        }

        [Fact]
        public void OperationScope_StaleDisposeAfterResetDoesNotCloseNewOperationGate()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);
            var stale = FlowRecorder.StartOperation("stale", "Stale.Root");

            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);
            using var active = FlowRecorder.StartOperation("active", "Active.Root");
            stale.Dispose();

            var state = FlowRecorder.EnterFast(methodMetadataIndex: 1);
            FlowRecorder.ExitFast(ref state);

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().HaveCount(2);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
        }

        [Fact]
        public async Task OperationScope_DisposeRepairsCurrentContextAfterChildContextDisposesFirst()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, allowRecordingWithoutOperation: false);

            using var outer = FlowRecorder.StartOperation("outer", "Outer.Root");
            var inner = FlowRecorder.StartOperation("inner", "Inner.Root");

            await Task.Run(inner.Dispose);
            inner.Dispose();

            var outerState = FlowRecorder.EnterFast(methodMetadataIndex: 1);
            FlowRecorder.ExitFast(ref outerState);

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().HaveCount(2);
            file.Events.Should().OnlyContain(flowEvent => flowEvent.OperationId == 1);
        }

        [Fact]
        public void OperationBudget_RecordsTruncationMarker()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, maxEventsPerOperation: 1, allowRecordingWithoutOperation: false);

            using (FlowRecorder.StartOperation("manual-root", "Sample.Root"))
            {
                var first = FlowRecorder.EnterFast(methodMetadataIndex: 1);
                FlowRecorder.ExitFast(ref first);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().Contain(flowEvent => flowEvent.Kind == FlowEventKind.Truncated);
            file.Strings[file.Events.Single(flowEvent => flowEvent.Kind == FlowEventKind.Truncated).MethodMetadataIndex].Should().Be("event limit");
        }

        [Fact]
        public void OperationBudget_RecordsSuppressionMarker()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, maxUniqueMethodsPerOperation: 1, allowRecordingWithoutOperation: false);

            using (FlowRecorder.StartOperation("manual-root", "Sample.Root"))
            {
                var first = FlowRecorder.EnterFast(methodMetadataIndex: 1);
                FlowRecorder.ExitFast(ref first);
                var second = FlowRecorder.EnterFast(methodMetadataIndex: 2);
                FlowRecorder.ExitFast(ref second);
            }

            var file = FlowRecorder.DrainFileForTesting();
            file.Events.Should().Contain(flowEvent => flowEvent.Kind == FlowEventKind.Suppressed);
            file.Strings[file.Events.Single(flowEvent => flowEvent.Kind == FlowEventKind.Suppressed).MethodMetadataIndex].Should().Be("unique method limit");
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
        public void NestedEnterFastExitFast_RecordsParentChildEdges()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var parent = FlowRecorder.EnterFast(methodMetadataIndex: 1);
            var child = FlowRecorder.EnterFast(methodMetadataIndex: 2);
            FlowRecorder.ExitFast(ref child);
            FlowRecorder.ExitFast(ref parent);

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
        public async Task AsyncContinuation_DoesNotFlowFrameContext()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var parent = FlowRecorder.Enter(methodMetadataIndex: 1);
            await Task.Yield();
            var child = FlowRecorder.Enter(methodMetadataIndex: 2);
            FlowRecorder.Exit(ref child);
            FlowRecorder.Exit(ref parent);

            var events = FlowRecorder.DrainForTesting();
            events[1].FlowId.Should().NotBe(events[0].FlowId);
            events[1].ParentFrameId.Should().Be(0);
            events[1].Depth.Should().Be(1);
        }

        [Fact]
        public async Task DetachedEnter_DoesNotFlowAsAsyncParent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var detached = FlowRecorder.EnterDetached(methodMetadataIndex: 1);
            await Task.Yield();
            var child = FlowRecorder.Enter(methodMetadataIndex: 2);
            FlowRecorder.Exit(ref child);
            FlowRecorder.Exit(ref detached);

            var events = FlowRecorder.DrainForTesting();
            events[0].MethodMetadataIndex.Should().Be(1);
            events[1].MethodMetadataIndex.Should().Be(2);
            events[1].ParentFrameId.Should().Be(0);
            events[1].Depth.Should().Be(1);
        }

        [Fact]
        public async Task AsyncStep_ReusesOperationFlowWithoutFlowingAsParent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            long operationId = 0;
            long generation = 0;

            var first = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.Exit(ref first);
            await Task.Yield();
            var child = FlowRecorder.Enter(methodMetadataIndex: 2);
            FlowRecorder.Exit(ref child);
            var second = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.Exit(ref second);

            var events = FlowRecorder.DrainForTesting();
            var enterEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter).ToArray();
            enterEvents.Should().HaveCount(3);
            enterEvents[0].FlowId.Should().Be(enterEvents[2].FlowId);
            enterEvents[0].FrameId.Should().NotBe(enterEvents[2].FrameId);
            enterEvents[1].MethodMetadataIndex.Should().Be(2);
            enterEvents[1].ParentFrameId.Should().Be(0);
        }

        [Fact]
        public async Task AsyncStepFast_ReusesOperationFlowWithoutFlowingAsParent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            long operationId = 0;
            long generation = 0;

            var first = FlowRecorder.EnterAsyncStepFast(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.ExitFast(ref first);
            await Task.Yield();
            var child = FlowRecorder.EnterFast(methodMetadataIndex: 2);
            FlowRecorder.ExitFast(ref child);
            var second = FlowRecorder.EnterAsyncStepFast(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.ExitFast(ref second);

            var events = FlowRecorder.DrainForTesting();
            var enterEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter).ToArray();
            enterEvents.Should().HaveCount(3);
            enterEvents[0].FlowId.Should().Be(enterEvents[2].FlowId);
            enterEvents[0].FrameId.Should().NotBe(enterEvents[2].FrameId);
            enterEvents[1].MethodMetadataIndex.Should().Be(2);
            enterEvents[1].ParentFrameId.Should().Be(0);
        }

        [Fact]
        public void AsyncStep_AssignsNewOperationFlowAfterReset()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            long operationId = 0;
            long generation = 0;

            var first = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.Exit(ref first);
            var firstOperationId = operationId;
            var firstGeneration = generation;

            FlowRecorder.ConfigureForTesting(enabled: true);
            var second = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref operationId, ref generation);
            FlowRecorder.Exit(ref second);

            operationId.Should().Be(1);
            operationId.Should().Be(firstOperationId, "a new generation resets recorder-local id counters");
            generation.Should().NotBe(firstGeneration);
        }

        [Fact]
        public void RecordAsyncEdge_RecordsCurrentAsyncOperationAsParent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            long parentOperationId = 0;
            long parentGeneration = 0;
            long childOperationId = 0;
            long childGeneration = 0;

            var parent = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref parentOperationId, ref parentGeneration);
            FlowRecorder.RecordAsyncEdge(methodMetadataIndex: 2, ref childOperationId, ref childGeneration);
            FlowRecorder.Exit(ref parent);

            var events = FlowRecorder.DrainForTesting();
            events.Should().HaveCount(3);
            events[1].Kind.Should().Be(FlowEventKind.AsyncEdge);
            events[1].MethodMetadataIndex.Should().Be(2);
            events[1].FlowId.Should().Be((ulong)parentOperationId);
            events[1].FrameId.Should().Be((ulong)childOperationId);
            childGeneration.Should().Be(parentGeneration);
        }

        [Fact]
        public void RecordAsyncEdge_WithoutCurrentAsyncOperation_DoesNotRecord()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            long childOperationId = 0;
            long childGeneration = 0;

            FlowRecorder.RecordAsyncEdge(methodMetadataIndex: 2, ref childOperationId, ref childGeneration);

            FlowRecorder.DrainForTesting().Should().BeEmpty();
            childOperationId.Should().Be(0);
            childGeneration.Should().Be(0);
        }

        [Fact]
        public void AsyncStep_AfterResetOnAnotherThread_DoesNotUseStaleThreadStaticParent()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            using var enterParent = new ManualResetEventSlim();
            using var resetCompleted = new ManualResetEventSlim();
            Exception? workerException = null;

            var worker = new Thread(() =>
            {
                try
                {
                    long parentOperationId = 0;
                    long parentGeneration = 0;
                    var parent = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 1, ref parentOperationId, ref parentGeneration);
                    enterParent.Set();
                    resetCompleted.Wait();

                    long childOperationId = 0;
                    long childGeneration = 0;
                    var child = FlowRecorder.EnterAsyncStep(methodMetadataIndex: 2, ref childOperationId, ref childGeneration);
                    FlowRecorder.Exit(ref child);
                    FlowRecorder.Exit(ref parent);
                }
                catch (Exception ex)
                {
                    workerException = ex;
                }
            });

            worker.Start();
            enterParent.Wait();
            FlowRecorder.ConfigureForTesting(enabled: true);
            resetCompleted.Set();
            worker.Join();

            workerException.Should().BeNull();
            FlowRecorder.DrainForTesting().Should().NotContain(flowEvent => flowEvent.Kind == FlowEventKind.AsyncEdge);
        }

        [Fact]
        public async Task ParallelAsyncBranches_DoNotInheritParentFrameContext()
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
            childEnterEvents.Should().OnlyContain(flowEvent => flowEvent.ParentFrameId == 0);
            childEnterEvents.Should().OnlyContain(flowEvent => flowEvent.Depth == 1);
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
            FlowRecorder.DroppedEvents.Should().BeGreaterThan(0);
        }

        [Fact]
        public void BinaryFormat_RoundTripsEvents()
        {
            var events = new[]
            {
                new FlowEvent(FlowEventKind.Enter, timestamp: 1, methodMetadataIndex: 2, flowId: 3, frameId: 4, parentFrameId: 5, depth: 6, threadId: 7, exceptionTypeId: 12),
                new FlowEvent(FlowEventKind.Exit, timestamp: 13, methodMetadataIndex: 14, flowId: 15, frameId: 16, parentFrameId: 17, depth: 18, threadId: 19, exceptionTypeId: 24),
            };
            var methods = new[]
            {
                new FlowMethodMetadata(methodMetadataIndex: 2, displayName: "Sample.Type.Method")
            };
            var operations = new[]
            {
                new FlowOperationMetadata(1, generation: 2, triggerReason: "manual-root", root: "Sample.Root", startTimestamp: 3, traceIdUpper: 4, traceIdLower: 5, rootSpanId: 6, activeSpanId: 7)
            };

            using var stream = new MemoryStream();
            FlowEventBinaryFormat.Write(stream, new FlowEventFile(events, methods, [], [], [], [], operations));
            stream.Position = 0;

            var roundTripped = FlowEventBinaryFormat.Read(stream);
            roundTripped.Events.Should().BeEquivalentTo(events);
            roundTripped.Methods.Should().BeEquivalentTo(methods);
            roundTripped.Operations.Should().BeEquivalentTo(operations);
        }

        [Fact]
        public void BinaryFormat_RoundTripsValueSections()
        {
            var file = new FlowEventFile(
                new[]
                {
                    new FlowEvent(FlowEventKind.Enter, timestamp: 1, methodMetadataIndex: 2, flowId: 3, frameId: 4, parentFrameId: 0, depth: 1, threadId: 7, exceptionTypeId: 0)
                },
                new[] { new FlowMethodMetadata(2, "Sample.Type.Method") },
                new[] { "arg0", "hello", "boom" },
                new[] { "System.String", "System.InvalidOperationException" },
                new[] { new FlowExceptionDetails(3, 4, typeId: 1, messageId: 2, stackId: -1, hResult: -1) },
                new[] { new FlowCapturedValue(3, 4, FlowCapturePhase.Entry, FlowValueKind.Argument, nameId: 0, typeId: 0, FlowValueTag.String, FlowNotCapturedReason.None, numberValue: 0, stringId: 1, itemCount: -1, capturedItemCount: -1) });

            using var stream = new MemoryStream();
            FlowEventBinaryFormat.Write(stream, file);
            stream.Position = 0;

            var roundTripped = FlowEventBinaryFormat.Read(stream);
            roundTripped.Events.Should().BeEquivalentTo(file.Events);
            roundTripped.Methods.Should().BeEquivalentTo(file.Methods);
            roundTripped.Strings.Should().BeEquivalentTo(file.Strings);
            roundTripped.Types.Should().BeEquivalentTo(file.Types);
            roundTripped.Exceptions.Should().BeEquivalentTo(file.Exceptions);
            roundTripped.Values.Should().BeEquivalentTo(file.Values);
        }

        [Fact]
        public void Flush_MergesColdMethodMetadataSidecar()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);
            var outputPath = Path.Combine(Path.GetTempPath(), "flow-recorder-" + Guid.NewGuid().ToString("N") + ".dflp");
            try
            {
                var state = FlowRecorder.EnterFast(methodMetadataIndex: 42);
                FlowRecorder.ExitFast(ref state);
                File.WriteAllText(outputPath + ".methods", "42\tSample.Type.Method\\tEscaped" + Environment.NewLine);

                FlowRecorder.Flush(outputPath);

                using var stream = File.Open(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var file = FlowEventBinaryFormat.Read(stream);
                file.Methods.Should().ContainSingle(method => method.MethodMetadataIndex == 42 && method.DisplayName == "Sample.Type.Method\tEscaped");
            }
            finally
            {
                TryDelete(outputPath);
                TryDelete(outputPath + ".methods");
            }
        }

        [Fact]
        public void Exit_WithException_RecordsExceptionDetails()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            FlowRecorder.Exit(ref state, new InvalidOperationException("boom"));

            var file = FlowRecorder.DrainFileForTesting();
            file.Exceptions.Should().ContainSingle();
            file.Types[file.Exceptions[0].TypeId].Should().Be(typeof(InvalidOperationException).FullName);
            file.Strings[file.Exceptions[0].MessageId].Should().Be("boom");
        }

        [Fact]
        public void LogArg_WhenCaptureEnabled_RecordsPrimitiveValue()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, valueCaptureMode: FlowValueCaptureMode.Entry);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            var value = 123;
            FlowRecorder.ShouldCaptureValues(ref state, FlowCapturePhase.Entry).Should().BeTrue();
            FlowRecorder.LogArg(ref value, index: 0, ref state);
            FlowRecorder.Exit(ref state);

            var file = FlowRecorder.DrainFileForTesting();
            file.Values.Should().ContainSingle();
            file.Values[0].Tag.Should().Be(FlowValueTag.Int64);
            file.Values[0].NumberValue.Should().Be(123);
            file.Strings[file.Values[0].NameId].Should().Be("arg0");
        }

        [Fact]
        public void DrainFile_RotatesStringAndTypeTablesWithCapturedValues()
        {
            FlowRecorder.ConfigureForTesting(enabled: true, valueCaptureMode: FlowValueCaptureMode.Entry);

            var firstState = FlowRecorder.Enter(methodMetadataIndex: 1);
            var first = "first";
            FlowRecorder.LogArg(ref first, index: 0, ref firstState);
            FlowRecorder.Exit(ref firstState);

            var firstFile = FlowRecorder.DrainFileForTesting();
            firstFile.Values.Should().ContainSingle();
            firstFile.Strings[firstFile.Values[0].NameId].Should().Be("arg0");
            firstFile.Strings[firstFile.Values[0].StringId].Should().Be("first");
            firstFile.Types[firstFile.Values[0].TypeId].Should().Be(typeof(string).FullName);

            var secondState = FlowRecorder.Enter(methodMetadataIndex: 2);
            var second = "second";
            FlowRecorder.LogArg(ref second, index: 0, ref secondState);
            FlowRecorder.Exit(ref secondState);

            var secondFile = FlowRecorder.DrainFileForTesting();
            secondFile.Values.Should().ContainSingle();
            secondFile.Strings[secondFile.Values[0].NameId].Should().Be("arg0");
            secondFile.Strings[secondFile.Values[0].StringId].Should().Be("second");
            secondFile.Types[secondFile.Values[0].TypeId].Should().Be(typeof(string).FullName);
        }

        [Fact]
        public void LogArg_WhenCaptureDisabled_DoesNotRecordValue()
        {
            FlowRecorder.ConfigureForTesting(enabled: true);

            var state = FlowRecorder.Enter(methodMetadataIndex: 42);
            var value = 123;
            FlowRecorder.ShouldCaptureValues(ref state, FlowCapturePhase.Entry).Should().BeFalse();
            FlowRecorder.LogArg(ref value, index: 0, ref state);
            FlowRecorder.Exit(ref state);

            FlowRecorder.DrainFileForTesting().Values.Should().BeEmpty();
        }

        private static void RecordChildAfterBarrier(int methodMetadataIndex, Barrier barrier)
        {
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            var state = FlowRecorder.Enter(methodMetadataIndex);
            FlowRecorder.Exit(ref state);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup for temp files.
            }
        }
    }
}
