// <copyright file="StandaloneCoverageReconciliationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Coverage.Collector;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class StandaloneCoverageReconciliationTests
{
    [Fact]
    public unsafe void StandaloneCollectorPublishesTheExactUnionAndArchivesItsProtocol()
    {
        var directory = CreateDirectory();
        try
        {
            using var reconciliation = StandaloneCoverageReconciliation.TryCreate(directory, "run-id");
            reconciliation.Should().NotBeNull();
            var handler = new DefaultWithGlobalCoverageEventHandler(
                configuredOutputDirectory: directory,
                runIdProvider: () => "run-id");
            var metadata = new MutableTestMetadata(
                8,
                0,
                [new FileCoverageMetadata("/src/standalone.cs", 0, 8, [0xff])]);

            PublishGeneration(handler, metadata, executedOffset: 0);
            PublishGeneration(handler, metadata, executedOffset: 7);
            handler.RequestSeal().Should().BeTrue();

            reconciliation!.TryPublish().Should().BeTrue();

            var outputPath = Directory.GetFiles(directory, "session-coverage-*.json").Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(outputPath, out var coverage).Should().BeTrue();
            var file = coverage!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutableBitmap.Should().Equal(0xff);
            file.ExecutedBitmap.Should().Equal(0x81);
            file.Data.Should().Equal(25, 8, 2);
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().BeEmpty();
            var archive = Path.Combine(directory, ".dd-coverage-completed");
            Directory.GetFiles(archive, "coverage-*.json", SearchOption.AllDirectories).Should().HaveCount(2);
            Directory.GetFiles(archive, ".dd-coverage-command-owner-*.claim", SearchOption.AllDirectories).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EnclosingCommandClaimCreatesANonOwnerParticipant()
    {
        var directory = CreateDirectory();
        try
        {
            using var command = DotnetTestRunState.TryCreate(
                DotnetTestCommandKind.DotnetTestCommand,
                null,
                directory,
                "run-id");

            using var reconciliation = StandaloneCoverageReconciliation.TryCreate(directory, "run-id");

            reconciliation.Should().NotBeNull();
            command.IsReconciliationOwner.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void LastStandaloneParticipantReconcilesWhenOwnerFinishesFirst()
    {
        var directory = CreateDirectory();
        try
        {
            using var owner = StandaloneCoverageReconciliation.TryCreate(directory, "run-id");
            using var participant = StandaloneCoverageReconciliation.TryCreate(directory, "run-id");
            owner.Should().NotBeNull();
            participant.Should().NotBeNull();

            var metadata = new MutableTestMetadata(
                8,
                0,
                [new FileCoverageMetadata("/src/standalone-owner-first.cs", 0, 8, [0xff])]);
            var ownerHandler = new DefaultWithGlobalCoverageEventHandler(configuredOutputDirectory: directory, runIdProvider: () => "run-id");
            PublishGeneration(ownerHandler, metadata, executedOffset: 0);
            ownerHandler.RequestSeal().Should().BeTrue();

            owner!.TryPublish().Should().BeFalse("the other standalone participant is still active");
            Directory.GetFiles(directory, "session-coverage-*.json").Should().BeEmpty();

            var participantHandler = new DefaultWithGlobalCoverageEventHandler(configuredOutputDirectory: directory, runIdProvider: () => "run-id");
            PublishGeneration(participantHandler, metadata, executedOffset: 7);
            participantHandler.RequestSeal().Should().BeTrue();

            participant!.TryPublish().Should().BeTrue();

            var outputPath = Directory.GetFiles(directory, "session-coverage-*.json").Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(outputPath, out var coverage).Should().BeTrue();
            var file = coverage!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutedBitmap.Should().Equal(0x81);
            file.Data.Should().Equal(25, 8, 2);
            Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static unsafe void PublishGeneration(
        DefaultWithGlobalCoverageEventHandler handler,
        ModuleCoverageMetadata metadata,
        int executedOffset)
    {
        var handle = handler.StartSession("xunit");
        handler.Container!.TryGetOrAddModuleValue(
                               metadata,
                               typeof(StandaloneCoverageReconciliationTests).Module,
                               CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                               out var module)
                           .Should()
                           .BeTrue();
        ((byte*)module!.FilesLines)[executedOffset] = 1;
        handler.EndSession(handle);
        using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
        handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

    private sealed class MutableTestMetadata : ModuleCoverageMetadata
    {
        private static readonly FieldInfo TotalLinesField = typeof(ModuleCoverageMetadata).GetField(nameof(TotalLines))!;
        private static readonly FieldInfo CoverageModeField = typeof(ModuleCoverageMetadata).GetField(nameof(CoverageMode))!;
        private static readonly FieldInfo FilesField = typeof(ModuleCoverageMetadata).GetField("Files", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        public MutableTestMetadata(int totalLines, int coverageMode, FileCoverageMetadata[] files)
        {
            // The production rewriter initializes these readonly fields. Reflection keeps this
            // construction helper entirely in the regression test assembly.
            TotalLinesField.SetValue(this, totalLines);
            CoverageModeField.SetValue(this, coverageMode);
            FilesField.SetValue(this, files);
        }
    }
}
