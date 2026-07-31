// <copyright file="GlobalCoverageOutputTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class GlobalCoverageOutputTests
{
    [Fact]
    public unsafe void SealPublishesOneProcessArtifactWithTheExactAccumulatedUnion()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            const string runId = "one-process-artifact";
            var handler = new DefaultWithGlobalCoverageEventHandler(configuredOutputDirectory: directory, runIdProvider: () => runId);
            var metadata = new TestModuleCoverageMetadata(8, 0, [new FileCoverageMetadata("/src/file.cs", 0, 8, [0xff])]);

            MergeLine(handler, metadata, 0);
            using (var intermediate = handler.AcquireGlobalCoverageSnapshot().Snapshot!)
            {
                intermediate.Model.Data.Should().Equal(12.5, 8, 1);
            }

            MergeLine(handler, metadata, 7);
            handler.FinalizeAndSeal().Should().BeTrue();
            handler.FinalizeAndSeal().Should().BeTrue("sealing is idempotent");

            Directory.GetFiles(directory, GlobalCoverageProtocol.PendingMarkerPattern).Should().BeEmpty();
            var artifact = Directory.GetFiles(directory, GlobalCoverageProtocol.CoverageFilePattern).Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(artifact, out var coverage).Should().BeTrue();
            coverage!.Data.Should().Equal(25, 8, 2);
            coverage.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject.ExecutedBitmap.Should().Equal(0x81);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PendingProducerPreventsRunScopedConsumption()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            const string runId = "pending-producer";
            var output = new GlobalCoverageOutputManager(directory, directory, () => runId);
            output.EnsureConfiguredAndFreeze().Should().BeTrue();

            GlobalCoverageFileCombiner.TryAcquireInputFiles(directory, GlobalCoverageProtocol.GetRunToken(runId), out var files).Should().BeFalse();
            files.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CollectorCanProvideOutputDirectoryAfterInMemoryCoverageStarts()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var output = new GlobalCoverageOutputManager(
                configuredDirectory: null,
                baseDirectory: directory,
                runIdProvider: () => "late-collector");

            output.EnsureConfiguredAndFreeze().Should().BeTrue();
            output.RegisterCollectorAndFreeze(directory).Should().BeTrue();

            Directory.GetFiles(directory, GlobalCoverageProtocol.PendingMarkerPattern).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static unsafe void MergeLine(DefaultWithGlobalCoverageEventHandler handler, ModuleCoverageMetadata metadata, int line)
    {
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(metadata, typeof(GlobalCoverageOutputTests).Module, 8, out var module).Should().BeTrue();
        ((byte*)module!.FilesLines)[line] = 1;
        handler.EndSession(handle);
        module.FilesLines.Should().Be(IntPtr.Zero);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dd-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
