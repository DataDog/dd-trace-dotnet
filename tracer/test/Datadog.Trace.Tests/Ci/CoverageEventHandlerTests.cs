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
using Datadog.Trace.Ci.Coverage.Models.Tests;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class CoverageEventHandlerTests
{
    [Fact]
    public async Task OwnerBoundHandlesCloseExactContextsFromAnUnrelatedExecutionContext()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
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
        firstHandle.Context!.SnapshotModules().Should().BeEmpty();
        secondHandle.Context!.SnapshotModules().Should().BeEmpty();
    }

    [Fact]
    public async Task ClosedFlowUsesLazyGlobalFallbackInsteadOfReopeningLocalContext()
    {
        var previousHandler = CoverageReporter.Handler;
        var handler = new DefaultWithGlobalCoverageEventHandler();
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
            handle.Context!.SnapshotModules().Should().BeEmpty();
            handler.GlobalContainer.SnapshotModules().Should().ContainSingle();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
        }
    }

    [Fact]
    public async Task ConcurrentFirstProbePublishesExactlyOneNativeBuffer()
    {
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
                                                      out var module)
                                                  .Should()
                                                  .BeTrue();
                                           return module;
                                       }))
                              .ToArray();

        start.SetResult(true);
        var modules = await Task.WhenAll(tasks);

        modules.Distinct().Should().ContainSingle();
        modules.Should().OnlyContain(value => value != null);
        var module = modules[0]!;
        context.SnapshotModules().Should().ContainSingle().Which.Should().BeSameAs(module);
        module.FilesLines.Should().NotBe(IntPtr.Zero);
        module.AllocatedByteLength.Should().Be(rawByteLength);

        context.Dispose();
        module.FilesLines.Should().Be(IntPtr.Zero);
        module.AllocatedByteLength.Should().Be(0);
        context.SnapshotModules().Should().BeEmpty();
    }

    [Fact]
    public void DisposeReleasesEveryModuleAndIsIdempotent()
    {
        var context = new CoverageContextContainer();
        var metadata = CreateMetadata(totalLines: 8, coverageMode: 0, lastExecutableLine: 1);

        context.TryGetOrAddModuleValue(metadata, typeof(CoverageEventHandlerTests).Module, 8, out _).Should().BeTrue();
        context.TryGetOrAddModuleValue(metadata, typeof(string).Module, 8, out _).Should().BeTrue();
        var modules = context.SnapshotModules();

        context.Dispose();

        modules.Should().OnlyContain(module => module.FilesLines == IntPtr.Zero && module.AllocatedByteLength == 0);
        context.SnapshotModules().Should().BeEmpty();
        context.Dispose();
        context.SnapshotModules().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public unsafe void PerTestCoveragePreservesTheExactUnionAcrossModules(int coverageMode)
    {
        var handler = new DefaultCoverageEventHandler();
        var metadata = new TestModuleCoverageMetadata(
            8,
            coverageMode,
            [new FileCoverageMetadata("/src/shared.cs", 0, 8, [0xff])]);
        var rawByteLength = CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(CoverageEventHandlerTests).Module,
                               rawByteLength,
                               out var firstModule)
                           .Should()
                           .BeTrue();
        handler.Container.TryGetOrAddModuleValue(
                             metadata,
                             typeof(string).Module,
                             rawByteLength,
                             out var secondModule)
                       .Should()
                       .BeTrue();

        if (coverageMode == 0)
        {
            ((byte*)firstModule!.FilesLines)[0] = 1;
            ((byte*)secondModule!.FilesLines)[7] = 1;
        }
        else
        {
            // Call counters are deliberately unsynchronized; only zero versus non-zero is meaningful.
            ((int*)firstModule!.FilesLines)[0] = -1;
            ((int*)secondModule!.FilesLines)[7] = int.MinValue;
        }

        var coverage = handler.EndSession(handle).Should().BeOfType<TestCoverage>().Subject;

        coverage.Files.Should().ContainSingle().Subject.Bitmap.Should().Equal(0x81);
        firstModule.FilesLines.Should().Be(IntPtr.Zero);
        secondModule.FilesLines.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public unsafe void TenThousandClosedContextsKeepTheExactUnionInOneCompactBitmap()
    {
        const int contextCount = 10_000;
        const int rawByteLength = 128 * 1024;
        const int executableLineCount = contextCount * 2;

        var handler = new DefaultWithGlobalCoverageEventHandler();
        var expectedExecutableBitmap = Enumerable.Repeat((byte)0xff, executableLineCount / 8).ToArray();
        var expectedExecutedBitmap = Enumerable.Repeat((byte)0xaa, executableLineCount / 8).ToArray();
        var metadata = new TestModuleCoverageMetadata(
            rawByteLength,
            0,
            [new FileCoverageMetadata("/src/stress.cs", 0, executableLineCount, expectedExecutableBitmap)]);

        for (var i = 0; i < contextCount; i++)
        {
            var handle = handler.StartSession("xunit");
            handler.Container!.TryGetOrAddModuleValue(
                                   metadata,
                                   typeof(CoverageEventHandlerTests).Module,
                                   rawByteLength,
                                   out var module)
                               .Should()
                               .BeTrue();
            var counters = (byte*)module!.FilesLines;
            counters[i * 2] = 1;
            handler.EndSession(handle);
            module.FilesLines.Should().Be(IntPtr.Zero, "closed contexts must release native buffers immediately");
            module.AllocatedByteLength.Should().Be(0);
        }

        var contextDiagnostics = handler.ContextDiagnostics;
        contextDiagnostics.Started.Should().Be(contextCount);
        contextDiagnostics.Closed.Should().Be(contextCount);
        contextDiagnostics.Disposed.Should().Be(contextCount);

        var accumulator = handler.AccumulatorDiagnostics;
        accumulator.RetainedBitmapBytes.Should().Be(expectedExecutedBitmap.Length);
        accumulator.ModuleCount.Should().Be(1);
        accumulator.FileSlotCount.Should().Be(1);
        accumulator.AcceptedContextCount.Should().Be(contextCount);
        accumulator.IsValid.Should().BeTrue();

        using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
        snapshot.MergedContextCount.Should().Be(contextCount);
        var file = snapshot.Model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
        file.ExecutableBitmap.Should().Equal(expectedExecutableBitmap);
        file.ExecutedBitmap.Should().Equal(expectedExecutedBitmap);
        file.Data.Should().Equal(50, executableLineCount, executableLineCount / 2);
    }

    private static CoverageSessionHandle StartAndAllocate(DefaultWithGlobalCoverageEventHandler handler, ModuleCoverageMetadata metadata)
    {
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(CoverageEventHandlerTests).Module,
                               CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                               out _)
                           .Should()
                           .BeTrue();
        return handle;
    }

    private static unsafe void WriteStaleFlowCounter()
    {
        var pointer = (byte*)CoverageReporter<StaleFlowMetadata>.GetFileCounter(0);
        *pointer = 1;
    }

    private static TestModuleCoverageMetadata CreateMetadata(int totalLines, int coverageMode, int lastExecutableLine)
        => new(
            totalLines,
            coverageMode,
            [new FileCoverageMetadata("/src/example.cs", 0, lastExecutableLine, new byte[(lastExecutableLine + 7) / 8])]);

    private sealed class StaleFlowMetadata : TestModuleCoverageMetadata
    {
        public StaleFlowMetadata()
            : base(1, 0, [new FileCoverageMetadata("/src/stale.cs", 0, 1, [0x80])])
        {
        }
    }
}
