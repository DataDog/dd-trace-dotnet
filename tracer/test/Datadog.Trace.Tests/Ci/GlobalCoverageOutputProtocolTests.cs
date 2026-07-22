// <copyright file="GlobalCoverageOutputProtocolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class GlobalCoverageOutputProtocolTests
{
    [Fact]
    public void PendingPrecedesFirstContextAndReadyFollowsBalancedSeal()
    {
        var directory = CreateDirectory();
        try
        {
            var handler = CreateHandler(directory);
            Directory.GetFiles(directory).Should().BeEmpty();

            var handle = handler.StartSession("xunit");
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().BeEmpty();
            handler.EndSession(handle);

            var result = handler.AcquireGlobalCoverageSnapshot();
            using (var snapshot = result.Snapshot!)
            {
                snapshot.RequiredOutputMask.Should().Be(1);
                handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
                snapshot.CommittedOutputMask.Should().Be(1);
            }

            handler.RequestSeal().Should().BeTrue();
            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MissingRequiredOutputLeavesPendingUnmatchedAndSealIncomplete()
    {
        var directory = CreateDirectory();
        try
        {
            var handler = CreateHandler(directory);
            var handle = handler.StartSession("xunit");
            handler.EndSession(handle);
            var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;

            snapshot.Dispose();

            handler.RequestSeal().Should().BeFalse();
            handler.SealedComplete.Should().BeFalse();
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void TwoPhysicalDirectoriesReceiveEveryGenerationAndCrossLinkedReadyMarkers()
    {
        var configuredDirectory = CreateDirectory();
        var collectorDirectory = CreateDirectory();
        try
        {
            var handler = CreateHandler(configuredDirectory);
            handler.RegisterCollectorOutputDirectory(collectorDirectory).Should().BeTrue();
            var handle = handler.StartSession("xunit");
            handler.EndSession(handle);
            var result = handler.AcquireGlobalCoverageSnapshot();
            using (var snapshot = result.Snapshot!)
            {
                snapshot.RequiredOutputMask.Should().Be(3);
                handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
                snapshot.CommittedOutputMask.Should().Be(3);
            }

            handler.RequestSeal().Should().BeTrue();
            Directory.GetFiles(configuredDirectory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(collectorDirectory, "coverage-*.json").Should().ContainSingle();
            var configuredReady = File.ReadAllText(Directory.GetFiles(configuredDirectory, ".dd-coverage-process-ready-*").Single());
            var collectorReady = File.ReadAllText(Directory.GetFiles(collectorDirectory, ".dd-coverage-process-ready-*").Single());
            var expectedDirectories = new[] { Path.GetFullPath(configuredDirectory), Path.GetFullPath(collectorDirectory) };
            JObject.Parse(configuredReady)["directories"]!.Values<string>().Should().BeEquivalentTo(expectedDirectories);
            JObject.Parse(collectorReady)["directories"]!.Values<string>().Should().BeEquivalentTo(expectedDirectories);
        }
        finally
        {
            Directory.Delete(configuredDirectory, true);
            Directory.Delete(collectorDirectory, true);
        }
    }

    [Fact]
    public void LexicalAliasDeduplicatesToOnePhysicalRegistration()
    {
        var directory = CreateDirectory();
        try
        {
            var alias = Path.Combine(directory, ".");
            var handler = CreateHandler(directory + Path.DirectorySeparatorChar);

            handler.RegisterCollectorOutputDirectory(alias).Should().BeTrue();

            handler.OutputRegistrations.Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void NewDirectoryAfterFreezeFailsClosedWithoutAReadyMarker()
    {
        var configuredDirectory = CreateDirectory();
        var lateDirectory = CreateDirectory();
        try
        {
            var handler = CreateHandler(configuredDirectory);
            var handle = handler.StartSession("xunit");

            handler.RegisterCollectorOutputDirectory(lateDirectory).Should().BeFalse();
            handler.EndSession(handle);

            handler.AcquireGlobalCoverageSnapshot().Status.Should().Be(GlobalCoverageSnapshotStatus.SuppressedIncomplete);
            Directory.GetFiles(configuredDirectory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(configuredDirectory, ".dd-coverage-process-ready-*").Should().BeEmpty();
            Directory.GetFiles(lateDirectory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(configuredDirectory, true);
            Directory.Delete(lateDirectory, true);
        }
    }

    [Fact]
    public void AuthorizedCombineArchivesRawArtifactsAndRemovesReadyBeforePendingSet()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var outputPath = Path.Combine(directory, "session-coverage-result.json");

            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, outputPath, out var combined).Should().BeTrue();

            combined.Should().NotBeNull();
            File.Exists(outputPath).Should().BeTrue();
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().BeEmpty();
            File.Exists(Path.Combine(directory, ".dd-coverage-process-reconcile.lock")).Should().BeTrue();
            Directory.GetFiles(Path.Combine(directory, ".dd-coverage-completed"), "coverage-*.json", SearchOption.AllDirectories).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void PendingWithoutReadyBlocksCombineAndPreservesExistingDestination()
    {
        var directory = CreateDirectory();
        var outputPath = Path.Combine(directory, "session-coverage-result.json");
        var original = new byte[] { 1, 3, 5, 7 };
        File.WriteAllBytes(outputPath, original);
        var handler = CreateHandler(directory);
        var handle = handler.StartSession("xunit");
        try
        {
            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, outputPath, out _).Should().BeFalse();

            File.ReadAllBytes(outputPath).Should().Equal(original);
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            handle.AbortIncomplete(GlobalCoverageFailureReason.TestCloseBeforeCoverage);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MalformedReadyMarkerBlocksCombineWithoutConsumingArtifacts()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var readyPath = Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Single();
            File.WriteAllText(readyPath, "{\"version\":1,\"unknown\":true}");
            var outputPath = Path.Combine(directory, "session-coverage-result.json");

            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, outputPath, out _).Should().BeFalse();

            File.Exists(outputPath).Should().BeFalse();
            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ReadyMarkerWithUtf8BomIsRejectedWithoutConsumingArtifacts()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var readyPath = Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Single();
            var marker = File.ReadAllText(readyPath);
            File.WriteAllText(readyPath, marker, Encoding.UTF8);

            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void OwnerClaimAndExclusiveReconciliationPublishOnceAndDeleteClaimLast()
    {
        var directory = CreateDirectory();
        try
        {
            using var owner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            ProduceCompleteRun(directory);
            owner.ReleaseActivity();

            var outputPath = Path.Combine(directory, "session-coverage-result.json");
            using var authority = owner.TakeReconciliationAuthority();
            CoverageUtils.TryReadAndCombine(directory, outputPath, authority, out var model, out var lease).Should().BeTrue();
            using (lease)
            {
                new GlobalCoverageArtifactWriter().WriteAtomicReplace(outputPath, model!);
                lease!.Complete();
            }

            File.Exists(outputPath).Should().BeTrue();
            File.Exists(owner.ClaimPath!).Should().BeFalse();
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-*").Should().ContainSingle(path => path.EndsWith("reconcile.lock", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ForeignOwnerClaimBlocksReconciliationWithoutConsumingArtifacts()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            File.WriteAllText(Path.Combine(directory, ".dd-coverage-command-owner-foreign.claim"), "foreign");

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ClaimlessProtocolRunBlocksReconciliationWithoutConsumingArtifacts()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            File.Delete(Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Single());

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void EmptyOwnerRunRemovesClaimAndLeavesDirectoryReusable()
    {
        var directory = CreateDirectory();
        try
        {
            var firstOwner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "first-run");
            var firstClaim = firstOwner.ClaimPath!;
            firstOwner.Dispose();

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            File.Exists(firstClaim).Should().BeFalse();
            using var nextOwner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "next-run");
            nextOwner.ReconciliationRole.Should().Be(DotnetTestReconciliationRole.ReconciliationOwner);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void OwnerClaimWithOrphanedRawArtifactIsNotTreatedAsEmptyRun()
    {
        var directory = CreateDirectory();
        try
        {
            var owner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            var claimPath = owner.ClaimPath!;
            owner.Dispose();
            var rawArtifactPath = Path.Combine(directory, "coverage-orphan.json");
            File.WriteAllText(rawArtifactPath, "{}");

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            File.Exists(claimPath).Should().BeTrue();
            File.Exists(rawArtifactPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MalformedOwnerClaimBlocksReconciliationWithoutConsumingArtifacts()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var claimPath = Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Single();
            File.WriteAllText(claimPath, "{\"version\":1,\"runToken\":\"wrong\"}");

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            File.Exists(claimPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LiveOwnerClaimBlocksUnprivilegedReconciliation()
    {
        var directory = CreateDirectory();
        try
        {
            using var owner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            ProduceCompleteRun(directory);
            owner.ReleaseActivity();

            CoverageUtils.TryCombineAndGetTotalCoverage(directory, Path.Combine(directory, "session-coverage-result.json"), out _).Should().BeFalse();

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            File.Exists(owner.ClaimPath!).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CertifiedInputMutationIsRejectedAndProtocolArtifactsRemainBlocked()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            GlobalCoverageReconciliation.TryAcquire(directory, authority: null, out var lease, out var protocolPresent).Should().BeTrue();
            protocolPresent.Should().BeTrue();
            using (lease)
            {
                var input = lease!.SelectedInputs.Should().ContainSingle().Subject;
                File.AppendAllText(input.Path, " ");

                new GlobalCoverageInputReader().TryRead(input.Path, lease.GetCertifiedInput(input.Path), out _).Should().BeFalse();
            }

            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MismatchedPhysicalCopyBlocksReconciliationBeforePublishing()
    {
        var coordinatorDirectory = CreateDirectory();
        var secondaryDirectory = CreateDirectory();
        try
        {
            ProduceCompleteRun(coordinatorDirectory, secondaryDirectory);
            File.AppendAllText(Directory.GetFiles(secondaryDirectory, "coverage-*.json").Single(), " ");

            CoverageUtils.TryCombineAndGetTotalCoverage(coordinatorDirectory, Path.Combine(coordinatorDirectory, "session-coverage-result.json"), out _).Should().BeFalse();

            File.Exists(Path.Combine(coordinatorDirectory, "session-coverage-result.json")).Should().BeFalse();
            Directory.GetFiles(coordinatorDirectory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(secondaryDirectory, "coverage-*.json").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(coordinatorDirectory, true);
            Directory.Delete(secondaryDirectory, true);
        }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

    private static DefaultWithGlobalCoverageEventHandler CreateHandler(string configuredDirectory)
        => new(configuredOutputDirectory: configuredDirectory, runIdProvider: () => "run-id");

    private static void ProduceCompleteRun(string directory)
    {
        using var command = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
        var handler = CreateHandler(directory);
        var handle = handler.StartSession("xunit");
        handler.EndSession(handle);
        using (var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!)
        {
            handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
        }

        handler.RequestSeal().Should().BeTrue();
        command.ReleaseActivity();
    }

    private static void ProduceCompleteRun(string coordinatorDirectory, string secondaryDirectory)
    {
        using var command = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, coordinatorDirectory, "run-id");
        var handler = CreateHandler(coordinatorDirectory);
        handler.RegisterCollectorOutputDirectory(secondaryDirectory).Should().BeTrue();
        var handle = handler.StartSession("xunit");
        handler.EndSession(handle);
        using (var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!)
        {
            handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
        }

        handler.RequestSeal().Should().BeTrue();
        command.ReleaseActivity();
    }
}
