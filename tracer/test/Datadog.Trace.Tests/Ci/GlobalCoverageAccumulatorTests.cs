// <copyright file="GlobalCoverageAccumulatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class GlobalCoverageAccumulatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public unsafe void AggregatesLineExecutionAndLineCallCountModes(int coverageMode)
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var metadata = new TestModuleCoverageMetadata(
            16,
            coverageMode,
            [new FileCoverageMetadata("/src/mode.cs", 0, 16, [0xff, 0xff])]);
        var handle = handler.StartSession("xunit");
        var rawByteLength = CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(GlobalCoverageAccumulatorTests).Module,
                               rawByteLength,
                               out var module)
                           .Should()
                           .BeTrue();

        if (coverageMode == 0)
        {
            var counters = (byte*)module!.FilesLines;
            counters[0] = 1;
            counters[8] = 1;
        }
        else
        {
            var counters = (int*)module!.FilesLines;
            counters[0] = 3;
            counters[8] = 7;
        }

        handler.EndSession(handle);

        var result = handler.AcquireGlobalCoverageSnapshot();
        result.Status.Should().Be(GlobalCoverageSnapshotStatus.Success);
        using var snapshot = result.Snapshot!;
        var file = snapshot.Model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
        file.ExecutedBitmap.Should().Equal(0x80, 0x80);
        file.Data.Should().Equal(12.5, 16, 2);
    }

    [Fact]
    public unsafe void ExceedingBitmapBudgetSuppressesGlobalCoverageButFreesNativeContext()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler(
            new GlobalCoverageAccumulatorLimits(maximumSingleBitmapBytes: 0, maximumRetainedBitmapBytes: 0, maximumModules: 1, maximumFileSlots: 1));
        var metadata = new TestModuleCoverageMetadata(8, 0, [new FileCoverageMetadata("/src/limit.cs", 0, 8, [0xff])]);
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(GlobalCoverageAccumulatorTests).Module,
                               8,
                               out var module)
                           .Should()
                           .BeTrue();
        *(byte*)module!.FilesLines = 1;

        handler.EndSession(handle).Should().NotBeNull();

        module.FilesLines.Should().Be(IntPtr.Zero);
        module.AllocatedByteLength.Should().Be(0);
        var snapshotResult = handler.AcquireGlobalCoverageSnapshot();
        snapshotResult.Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
        snapshotResult.FailureReason.Should().Be(GlobalCoverageFailureReason.MergeFailed);
    }

    [Fact]
    public void ConcurrentSuppressionInvalidatesAnAlreadyAcquiredSnapshotBeforeCommit()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var result = handler.AcquireGlobalCoverageSnapshot();
        using var snapshot = result.Snapshot!;
        var committed = false;

        handler.MarkProbeDataIncomplete(GlobalCoverageFailureReason.ProbeDataIncomplete);

        handler.TryCommit(snapshot, () => committed = true).Should().BeFalse();
        committed.Should().BeFalse();
        snapshot.Dispose();
        handler.AcquireGlobalCoverageSnapshot().Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
    }

    [Fact]
    public void CommitFailureIsStickyAndPreservesTheOriginalException()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var result = handler.AcquireGlobalCoverageSnapshot();
        using var snapshot = result.Snapshot!;
        var expected = new InvalidOperationException("Injected output failure.");

        var action = () => handler.TryCommit(snapshot, () => throw expected);

        action.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expected);
        snapshot.Dispose();
        var suppressed = handler.AcquireGlobalCoverageSnapshot();
        suppressed.Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
        suppressed.FailureReason.Should().Be(GlobalCoverageFailureReason.OutputCommitFailed);
    }

    [Fact]
    public void DisposedSnapshotCannotCommitSideEffects()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var result = handler.AcquireGlobalCoverageSnapshot();
        var snapshot = result.Snapshot!;
        snapshot.Dispose();
        var committed = false;

        handler.TryCommit(snapshot, () => committed = true).Should().BeFalse();
        committed.Should().BeFalse();
    }

    [Fact]
    public void SealWaitsForSnapshotLeaseAndCompletesAfterItsDisposal()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var result = handler.AcquireGlobalCoverageSnapshot();
        var snapshot = result.Snapshot!;
        var completed = false;

        handler.FinalizeAndSeal(value => completed = value).Should().BeFalse();
        completed.Should().BeFalse();

        snapshot.Dispose();

        completed.Should().BeTrue();
        handler.AcquireGlobalCoverageSnapshot().Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
    }

    [Fact]
    public void StartDuringCompletingIsRejectedAndMakesSealIncomplete()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var handle = handler.StartSession("xunit");

        var completed = false;
        handler.FinalizeAndSeal(value => completed = value).Should().BeFalse();
        handler.StartSession("xunit").IsValid.Should().BeFalse();
        handler.EndSession(handle);

        completed.Should().BeFalse();
        handler.AcquireGlobalCoverageSnapshot().Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
    }

    [Fact]
    public void StartAfterSealThrowsBeforeCreatingAContext()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        handler.FinalizeAndSeal().Should().BeTrue();

        var action = () => handler.StartSession("xunit");

        action.Should().Throw<InvalidOperationException>();
        handler.Container.Should().BeNull();
    }

    [Fact]
    public unsafe void UnionsOverlappingContextsAcrossModulesAndFilesWithoutInflatingDenominator()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var firstMetadata = new TestModuleCoverageMetadata(
            16,
            0,
            [
                new FileCoverageMetadata("/src/a.cs", 0, 8, [0xff]),
                new FileCoverageMetadata("/src/b.cs", 8, 8, [0xff])
            ]);
        var secondMetadata = new TestModuleCoverageMetadata(8, 0, [new FileCoverageMetadata("/src/c.cs", 0, 8, [0xff])]);

        MergeContext(handler, firstMetadata, typeof(GlobalCoverageAccumulatorTests).Module, [0, 8]);
        MergeContext(handler, firstMetadata, typeof(GlobalCoverageAccumulatorTests).Module, [0, 1, 8, 15]);
        MergeContext(handler, secondMetadata, typeof(string).Module, [3]);

        using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
        snapshot.Model.Components.Should().HaveCount(2);
        snapshot.Model.Components.SelectMany(component => component.Files).Should().HaveCount(3);
        snapshot.Model.Data[0].Should().Be(20.83);
        snapshot.Model.Data[1].Should().Be(24);
        snapshot.Model.Data[2].Should().Be(5);
        snapshot.MergedContextCount.Should().Be(3);
    }

    [Fact]
    public unsafe void ContextClosedAfterSnapshotAppearsInTheNextSnapshot()
    {
        var handler = new DefaultWithGlobalCoverageEventHandler();
        var metadata = new TestModuleCoverageMetadata(8, 0, [new FileCoverageMetadata("/src/late.cs", 0, 8, [0xff])]);
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(GlobalCoverageAccumulatorTests).Module,
                               8,
                               out var module)
                           .Should()
                           .BeTrue();
        *(byte*)module!.FilesLines = 1;

        using (var first = handler.AcquireGlobalCoverageSnapshot().Snapshot!)
        {
            first.Model.Components.Should().BeEmpty();
        }

        handler.EndSession(handle);
        using var second = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
        second.Model.Data.Should().Equal(12.5, 8, 1);
        second.MergedContextCount.Should().Be(1);
    }

    private static unsafe void MergeContext(DefaultWithGlobalCoverageEventHandler handler, ModuleCoverageMetadata metadata, Module module, int[] executedOffsets)
    {
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               module,
                               CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                               out var moduleValue)
                           .Should()
                           .BeTrue();
        var counters = (byte*)moduleValue!.FilesLines;
        foreach (var offset in executedOffsets)
        {
            counters[offset] = 1;
        }

        handler.EndSession(handle);
    }
}
