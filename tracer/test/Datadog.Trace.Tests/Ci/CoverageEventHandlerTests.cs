// <copyright file="CoverageEventHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class CoverageEventHandlerTests
{
    private enum FailurePoint
    {
        Capacity,
        Allocation,
        Initialization,
        PrePublication,
        PostPublication,
    }

    [Fact]
    public async Task OwnerBoundHandlesCloseExactContextsFromAnUnrelatedExecutionContext()
    {
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var strategy = new CoverageModuleValueStrategy(diagnostics);
        var handler = new DefaultWithGlobalCoverageEventHandler(moduleValueStrategy: strategy);
        var metadata = CreateMetadata(totalLines: 128, coverageMode: 0, lastExecutableLine: 128);

        var firstHandle = await Task.Run(() => StartAndAllocate(handler, metadata));
        var secondHandle = await Task.Run(() => StartAndAllocate(handler, metadata));

        Task closeTask;
        using (ExecutionContext.SuppressFlow())
        {
            closeTask = Task.Run(
                () =>
                {
                    handler.EndSession(secondHandle);
                    handler.EndSession(firstHandle);
                });
        }

        await closeTask;

        var contextDiagnostics = handler.ContextDiagnostics;
        contextDiagnostics.Started.Should().Be(2);
        contextDiagnostics.Closed.Should().Be(2);
        contextDiagnostics.Disposed.Should().Be(2);
        handler.ActiveContexts.Should().Be(0);
        handler.AccumulatorDiagnostics.AcceptedContextCount.Should().Be(2);
        var native = diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext);
        native.CurrentBytes.Should().Be(0);
        native.ActiveBuffers.Should().Be(0);
        native.AllocationCount.Should().Be(2);
        native.FreeCount.Should().Be(2);
    }

    [Fact]
    public async Task ClosedFlowUsesLazyGlobalFallbackInsteadOfReopeningLocalContext()
    {
        var previousHandler = CoverageReporter.Handler;
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var handler = new DefaultWithGlobalCoverageEventHandler(moduleValueStrategy: new CoverageModuleValueStrategy(diagnostics));
        CoverageReporter.Handler = handler;

        try
        {
            var handle = handler.StartSession("xunit");
            WriteStaleFlowCounter();

            var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var child = Task.Run(
                async () =>
                {
                    await probe.Task;
                    WriteStaleFlowCounter();
                });

            handler.EndSession(handle);
            probe.SetResult(true);
            await child;
            diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext).ActiveBuffers.Should().Be(0);
            var fallback = diagnostics.GetSnapshot(CoverageModuleValueOrigin.GlobalFallback);
            fallback.ActiveBuffers.Should().Be(1);
            fallback.AllocationCount.Should().Be(1);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
        }
    }

    [Fact]
    public async Task ConcurrentFirstProbePublishesExactlyOneNativeBuffer()
    {
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var strategy = new CoverageModuleValueStrategy(diagnostics);
        var context = new CoverageContextContainer();
        var metadata = CreateMetadata(totalLines: 1024, coverageMode: 0, lastExecutableLine: 1);
        var rawByteLength = CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 32)
                              .Select(
                                   _ => Task.Run(
                                       async () =>
                                       {
                                           await start.Task;
                                           context.TryGetOrAddModuleValue(
                                                      metadata,
                                                      typeof(CoverageEventHandlerTests).Module,
                                                      rawByteLength,
                                                      strategy,
                                                      CoverageModuleValueOrigin.TestContext,
                                                      out var module)
                                                  .Should()
                                                  .BeTrue();
                                           return module;
                                       }))
                              .ToArray();

        start.SetResult(true);
        var modules = await Task.WhenAll(tasks);

        modules.Distinct().Should().ContainSingle();
        var beforeClose = diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext);
        beforeClose.CurrentBytes.Should().Be(1024);
        beforeClose.PeakBytes.Should().Be(1024);
        beforeClose.ActiveBuffers.Should().Be(1);
        beforeClose.PeakBuffers.Should().Be(1);
        beforeClose.AllocationCount.Should().Be(1);

        context.Dispose();
        var afterClose = diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext);
        afterClose.CurrentBytes.Should().Be(0);
        afterClose.ActiveBuffers.Should().Be(0);
        afterClose.FreeCount.Should().Be(1);
    }

    [Fact]
    public void DisposeAttemptsEveryModuleWhenOneFreeThrows()
    {
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var strategy = new ThrowAfterFreeStrategy(diagnostics);
        var context = new CoverageContextContainer();
        var metadata = CreateMetadata(totalLines: 8, coverageMode: 0, lastExecutableLine: 1);

        context.TryGetOrAddModuleValue(metadata, typeof(CoverageEventHandlerTests).Module, 8, strategy, CoverageModuleValueOrigin.TestContext, out _).Should().BeTrue();
        context.TryGetOrAddModuleValue(metadata, typeof(string).Module, 8, strategy, CoverageModuleValueOrigin.TestContext, out _).Should().BeTrue();

        var action = context.Dispose;

        action.Should().Throw<InvalidOperationException>().WithMessage("Injected free failure.");
        strategy.FreeCalls.Should().Be(2);
        context.SnapshotModules().Should().BeEmpty();
        context.Dispose();
        strategy.FreeCalls.Should().Be(2);
    }

    [Fact]
    public unsafe void TenThousandClosedContextsKeepOnlyOneCompactBitmap()
    {
        const int contextCount = 10_000;
        const int rawByteLength = 128 * 1024;

        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var handler = new DefaultWithGlobalCoverageEventHandler(moduleValueStrategy: new CoverageModuleValueStrategy(diagnostics));
        var metadata = CreateMetadata(totalLines: rawByteLength, coverageMode: 0, lastExecutableLine: 1);

        for (var i = 0; i < contextCount; i++)
        {
            var handle = handler.StartSession("xunit");
            handler.Container!.TryGetOrAddModuleValue(
                                   metadata,
                                   typeof(CoverageEventHandlerTests).Module,
                                   rawByteLength,
                                   handler.ModuleValueStrategy,
                                   CoverageModuleValueOrigin.TestContext,
                                   out var module)
                               .Should()
                               .BeTrue();
            *(byte*)module!.FilesLines = 1;
            handler.EndSession(handle);
        }

        var native = diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext);
        native.CurrentBytes.Should().Be(0);
        native.PeakBytes.Should().Be(rawByteLength);
        native.ActiveBuffers.Should().Be(0);
        native.PeakBuffers.Should().Be(1);
        native.AllocationCount.Should().Be(contextCount);
        native.FreeCount.Should().Be(contextCount);

        var contextDiagnostics = handler.ContextDiagnostics;
        contextDiagnostics.Started.Should().Be(contextCount);
        contextDiagnostics.Closed.Should().Be(contextCount);
        contextDiagnostics.Disposed.Should().Be(contextCount);

        var accumulator = handler.AccumulatorDiagnostics;
        accumulator.RetainedBitmapBytes.Should().Be(1);
        accumulator.ModuleCount.Should().Be(1);
        accumulator.FileSlotCount.Should().Be(1);
        accumulator.AcceptedContextCount.Should().Be(contextCount);
        accumulator.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FirstProbeFailuresReleaseEveryOwnedNativeBufferAndSuppressGlobalOutput()
    {
        var previousHandler = CoverageReporter.Handler;
        try
        {
            AssertFirstProbeFailure<CapacityFailureMetadata>(FailurePoint.Capacity, expectedAllocations: 0, expectedFrees: 0);
            AssertFirstProbeFailure<AllocationFailureMetadata>(FailurePoint.Allocation, expectedAllocations: 1, expectedFrees: 0);
            AssertFirstProbeFailure<InitializationFailureMetadata>(FailurePoint.Initialization, expectedAllocations: 1, expectedFrees: 1);
            AssertFirstProbeFailure<PrePublicationFailureMetadata>(FailurePoint.PrePublication, expectedAllocations: 1, expectedFrees: 1);
            AssertFirstProbeFailure<PostPublicationFailureMetadata>(FailurePoint.PostPublication, expectedAllocations: 1, expectedFrees: 1);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
        }
    }

    private static CoverageSessionHandle StartAndAllocate(DefaultWithGlobalCoverageEventHandler handler, ModuleCoverageMetadata metadata)
    {
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(CoverageEventHandlerTests).Module,
                               CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                               handler.ModuleValueStrategy,
                               CoverageModuleValueOrigin.TestContext,
                               out _)
                           .Should()
                           .BeTrue();
        return handle;
    }

    private static unsafe void WriteStaleFlowCounter()
        => *(byte*)CoverageReporter<StaleFlowMetadata>.GetFileCounter(0) = 1;

    private static unsafe void AssertFirstProbeFailure<TMetadata>(FailurePoint failurePoint, int expectedAllocations, int expectedFrees)
        where TMetadata : ModuleCoverageMetadata, new()
    {
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var strategy = new FailingStrategy(diagnostics, failurePoint);
        var handler = new DefaultWithGlobalCoverageEventHandler(moduleValueStrategy: strategy);
        CoverageReporter.Handler = handler;
        var handle = handler.StartSession("xunit");

        Action action = () => { _ = CoverageReporter<TMetadata>.GetFileCounter(0); };
        action.Should().Throw<InvalidOperationException>();
        handle.AbortIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);

        strategy.AllocateCalls.Should().Be(expectedAllocations);
        strategy.FreeCalls.Should().Be(expectedFrees);
        var native = diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext);
        native.ActiveBuffers.Should().Be(0);
        native.CurrentBytes.Should().Be(0);
        handler.ActiveContexts.Should().Be(0);
        handler.AccumulatorDiagnostics.IsValid.Should().BeFalse();
    }

    private static TestMetadata CreateMetadata(int totalLines, int coverageMode, int lastExecutableLine)
        => new(
            totalLines,
            coverageMode,
            [new FileCoverageMetadata("/src/example.cs", 0, lastExecutableLine, new byte[(lastExecutableLine + 7) / 8])]);

    private sealed class TestMetadata : ModuleCoverageMetadata
    {
        internal TestMetadata(int totalLines, int coverageMode, FileCoverageMetadata[] files)
            : base(totalLines, coverageMode, files)
        {
        }
    }

    private sealed class StaleFlowMetadata : ModuleCoverageMetadata
    {
        public StaleFlowMetadata()
            : base(1, 0, [new FileCoverageMetadata("/src/stale.cs", 0, 1, [0x80])])
        {
        }
    }

    private sealed class CapacityFailureMetadata : OneLineMetadata
    {
    }

    private sealed class AllocationFailureMetadata : OneLineMetadata
    {
    }

    private sealed class InitializationFailureMetadata : OneLineMetadata
    {
    }

    private sealed class PrePublicationFailureMetadata : OneLineMetadata
    {
    }

    private sealed class PostPublicationFailureMetadata : OneLineMetadata
    {
    }

    private abstract class OneLineMetadata : ModuleCoverageMetadata
    {
        protected OneLineMetadata()
            : base(1, 0, [new FileCoverageMetadata("/src/failure.cs", 0, 1, [0x80])])
        {
        }
    }

    private sealed class FailingStrategy : CoverageModuleValueStrategy
    {
        private readonly FailurePoint _failurePoint;

        internal FailingStrategy(CoverageNativeAllocationDiagnostics diagnostics, FailurePoint failurePoint)
            : base(diagnostics)
        {
            _failurePoint = failurePoint;
        }

        internal int AllocateCalls { get; private set; }

        internal int FreeCalls { get; private set; }

        internal override IntPtr Allocate(int byteLength, CoverageModuleValueOrigin origin)
        {
            AllocateCalls++;
            if (_failurePoint == FailurePoint.Allocation)
            {
                throw new InvalidOperationException("Injected allocation failure.");
            }

            return base.Allocate(byteLength, origin);
        }

        internal override unsafe void Initialize(IntPtr pointer, int byteLength, CoverageModuleValueOrigin origin)
        {
            base.Initialize(pointer, byteLength, origin);
            if (_failurePoint == FailurePoint.Initialization)
            {
                throw new InvalidOperationException("Injected initialization failure.");
            }
        }

        internal override void Free(IntPtr pointer, CoverageModuleValueOrigin origin)
        {
            FreeCalls++;
            base.Free(pointer, origin);
        }

        internal override void BeforeCapacityGrowth(CoverageModuleValueOrigin origin)
        {
            if (_failurePoint == FailurePoint.Capacity)
            {
                throw new InvalidOperationException("Injected capacity failure.");
            }
        }

        internal override void BeforePublication(CoverageModuleValueOrigin origin)
        {
            if (_failurePoint == FailurePoint.PrePublication)
            {
                throw new InvalidOperationException("Injected pre-publication failure.");
            }
        }

        internal override void AfterPublication(CoverageModuleValueOrigin origin)
        {
            if (_failurePoint == FailurePoint.PostPublication)
            {
                throw new InvalidOperationException("Injected post-publication failure.");
            }
        }
    }

    private sealed class ThrowAfterFreeStrategy : CoverageModuleValueStrategy
    {
        internal ThrowAfterFreeStrategy(CoverageNativeAllocationDiagnostics diagnostics)
            : base(diagnostics)
        {
        }

        internal int FreeCalls { get; private set; }

        internal override void Free(IntPtr pointer, CoverageModuleValueOrigin origin)
        {
            FreeCalls++;
            base.Free(pointer, origin);
            throw new InvalidOperationException("Injected free failure.");
        }
    }
}
