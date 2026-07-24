// <copyright file="GlobalCoverageOutputProtocolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
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
    public void FinalizeAndSealPublishesFinalSnapshotWithoutCollectorAndIsIdempotent()
    {
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            handler.EndSession(handle);

            CoverageReporter.FinalizeGlobalCoverage().Should().BeTrue();
            CoverageReporter.FinalizeGlobalCoverage().Should().BeTrue();

            handler.SealedComplete.Should().BeTrue();
            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void FinalizeAndSealWaitsForActiveContextBeforePublishingTerminalSnapshot()
    {
        var directory = CreateDirectory();
        try
        {
            var handler = CreateHandler(directory);
            var metadata = new TestModuleCoverageMetadata(
                8,
                0,
                [new FileCoverageMetadata("/src/late-finalization.cs", 0, 8, [0xff])]);
            var handle = handler.StartSession("xunit");
            handler.Container!.TryGetOrAddModuleValue(
                                   metadata,
                                   typeof(GlobalCoverageOutputProtocolTests).Module,
                                   CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                                   out var module)
                               .Should()
                               .BeTrue();
            ((byte*)module!.FilesLines)[7] = 1;

            handler.FinalizeAndSeal().Should().BeFalse("the active context still owns an admission");
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();

            handler.EndSession(handle);

            handler.SealedComplete.Should().BeTrue();
            var coveragePath = Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(coveragePath, out var coverage).Should().BeTrue();
            var file = coverage!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutableBitmap.Should().Equal(0xff);
            file.ExecutedBitmap.Should().Equal(0x01);
            file.Data.Should().Equal(12.5, 8, 1);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void RetiredContextKeepsProbeBufferAliveAndMergesLateWritesBeforeRelease()
    {
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            var probe = CoverageReporter<RetiredProbeMetadata>.AcquireFileCounter(0);
            ((byte*)probe.Pointer)[0] = 1;
            var module = handler.Container!.SnapshotModules().Should().ContainSingle().Subject;

            handler.EndSession(handle);

            module.FilesLines.Should().NotBe(IntPtr.Zero, "the active invocation still owns the native buffer");
            ((byte*)probe.Pointer)[7] = 1;
            probe.Dispose();

            module.FilesLines.Should().Be(IntPtr.Zero);
            module.AllocatedByteLength.Should().Be(0);
            using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
            var file = snapshot.Model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutedBitmap.Should().Equal(0x81);
            file.Data.Should().Equal(25, 8, 2);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void ConcurrentProbeReleaseMergesEveryLateWriteBeforeFreeingTheBuffer()
    {
        const int probeCount = 32;
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            var probes = new CoverageProbe[probeCount];
            for (var i = 0; i < probes.Length; i++)
            {
                probes[i] = CoverageReporter<ConcurrentRetiredProbeMetadata>.AcquireFileCounter(0);
            }

            var module = handler.Container!.SnapshotModules().Should().ContainSingle().Subject;
            handler.EndSession(handle);

            Parallel.For(
                0,
                probes.Length,
                i =>
                {
                    var probe = probes[i];
                    ((byte*)probe.Pointer)[i] = 1;
                    probe.Dispose();
                });

            module.FilesLines.Should().Be(IntPtr.Zero);
            module.AllocatedByteLength.Should().Be(0);
            using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
            var file = snapshot.Model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutedBitmap.Should().Equal(0xff, 0xff, 0xff, 0xff);
            file.Data.Should().Equal(100, probeCount, probeCount);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void LegacyProbeUsesProcessLifetimeSinkAndCannotWriteIntoPublishedCoverage()
    {
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            var pointer = (byte*)CoverageReporter<LegacyProbeMetadata>.GetFileCounter(0);

            handler.EndSession(handle);
            pointer[0] = 1;

            handler.GlobalContainer.SnapshotModules().Should().BeEmpty();
            handler.DiscardContainer.SnapshotModules().Should().ContainSingle();
            handler.FinalizeAndSeal().Should().BeTrue();
            var coveragePath = Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(coveragePath, out var coverage).Should().BeTrue();
            coverage!.Components.Should().BeEmpty();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void RetiredCallCountProbeMergesLateWritesWithoutDoubleCountingTheContext()
    {
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            var probe = CoverageReporter<RetiredCallCountProbeMetadata>.AcquireFileCounter(0);
            ((int*)probe.Pointer)[0]++;

            handler.EndSession(handle);
            ((int*)probe.Pointer)[7]++;
            probe.Dispose();

            handler.AccumulatorDiagnostics.AcceptedContextCount.Should().Be(1);
            using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
            var file = snapshot.Model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutedBitmap.Should().Equal(0x81);
            file.Data.Should().Equal(25, 8, 2);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public unsafe void SealWaitsForActiveProbeAndRejectsWritesAcquiredAfterTerminalSnapshot()
    {
        var directory = CreateDirectory();
        var previousHandler = CoverageReporter.Handler;
        try
        {
            var handler = CreateHandler(directory);
            CoverageReporter.Handler = handler;
            var handle = handler.StartSession("xunit");
            var probe = CoverageReporter<SealBoundaryProbeMetadata>.AcquireFileCounter(0);
            ((byte*)probe.Pointer)[0] = 1;
            handler.EndSession(handle);

            handler.FinalizeAndSeal().Should().BeFalse("an admitted probe can still change coverage");
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();

            ((byte*)probe.Pointer)[7] = 1;
            probe.Dispose();

            handler.SealedComplete.Should().BeTrue();
            var coveragePath = Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle().Subject;
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(coveragePath, out var coverage).Should().BeTrue();
            coverage!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject.ExecutedBitmap.Should().Equal(0x81);

            var rejectedProbe = CoverageReporter<SealBoundaryProbeMetadata>.AcquireFileCounter(0);
            ((byte*)rejectedProbe.Pointer)[3] = 1;
            rejectedProbe.Dispose();

            reader.TryRead(coveragePath, out coverage).Should().BeTrue();
            coverage!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject.ExecutedBitmap.Should().Equal(0x81);
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
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
    public unsafe void ReconciliationUnionsEveryPublishedGenerationExactly()
    {
        var directory = CreateDirectory();
        try
        {
            var handler = CreateHandler(directory);
            var metadata = new TestModuleCoverageMetadata(
                8,
                0,
                [new FileCoverageMetadata("/src/generations.cs", 0, 8, [0xff])]);

            using (var command = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id"))
            {
                PublishGeneration(executedOffset: 0);
                PublishGeneration(executedOffset: 7);
                handler.RequestSeal().Should().BeTrue();
                command.ReleaseActivity();
            }

            var outputPath = Path.Combine(directory, "session-coverage-result.json");
            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, outputPath, out var combined).Should().BeTrue();

            var file = combined!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.ExecutableBitmap.Should().Equal(0xff);
            file.ExecutedBitmap.Should().Equal(0x81);
            file.Data.Should().Equal(25, 8, 2);
            Directory.GetFiles(Path.Combine(directory, ".dd-coverage-completed"), "coverage-*.json", SearchOption.AllDirectories).Should().HaveCount(2);

            void PublishGeneration(int executedOffset)
            {
                var handle = handler.StartSession("xunit");
                handler.Container!.TryGetOrAddModuleValue(
                                       metadata,
                                       typeof(GlobalCoverageOutputProtocolTests).Module,
                                       CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata),
                                       out var module)
                                   .Should()
                                   .BeTrue();
                var counters = (byte*)module!.FilesLines;
                counters[executedOffset] = 1;
                handler.EndSession(handle);

                using var snapshot = handler.AcquireGlobalCoverageSnapshot().Snapshot!;
                handler.TryPublishRequiredFiles(snapshot).Should().BeTrue();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
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
            Directory.GetFiles(Path.Combine(directory, ".dd-coverage-completed"), ".dd-coverage-process-ready-*", SearchOption.AllDirectories).Should().ContainSingle();
            Directory.GetFiles(Path.Combine(directory, ".dd-coverage-completed"), ".dd-coverage-process-incomplete-*", SearchOption.AllDirectories).Should().ContainSingle();
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
    public void SemanticallyInvalidCoverageBlocksCombineAndPreservesDestinationAndProtocol()
    {
        var directory = CreateDirectory();
        var outputPath = Path.Combine(directory, "session-coverage-result.json");
        var original = new byte[] { 2, 4, 6, 8 };
        try
        {
            ProduceCompleteRun(directory);
            File.WriteAllBytes(outputPath, original);
            var rawPath = Directory.GetFiles(directory, "coverage-*.json").Single();
            File.WriteAllText(
                rawPath,
                "{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executableBitmap\":\"gA==\",\"executedBitmap\":\"/w==\"}]}]}",
                new UTF8Encoding(false));

            global::CoverageUtils.TryCombineAndGetTotalCoverage(directory, outputPath, out _).Should().BeFalse();

            File.ReadAllBytes(outputPath).Should().Equal(original);
            File.Exists(rawPath).Should().BeTrue();
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().ContainSingle();
            Directory.Exists(Path.Combine(directory, ".dd-coverage-completed")).Should().BeFalse();
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
    public void OwnerClaimAndExclusiveReconciliationPublishAndArchiveAsOneTransaction()
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
                var writer = new GlobalCoverageArtifactWriter();
                using var stagedOutput = writer.StageReplace(outputPath, model!);
                lease!.Complete(stagedOutput.Commit);
            }

            File.Exists(outputPath).Should().BeTrue();
            File.Exists(owner.ClaimPath!).Should().BeFalse();
            Directory.GetFiles(directory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(directory, ".dd-coverage-process-*").Should().ContainSingle(path => path.EndsWith("reconcile.lock", StringComparison.Ordinal));
            Directory.GetFiles(Path.Combine(directory, ".dd-coverage-completed"), ".dd-coverage-command-owner-*.claim", SearchOption.AllDirectories).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CertifiedInputMutationBeforeCommitLeavesOutputAndProtocolUnchanged()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var outputPath = Path.Combine(directory, "session-coverage-result.json");
            CoverageUtils.TryReadAndCombine(directory, outputPath, authority: null, out var model, out var lease).Should().BeTrue();
            using (lease)
            {
                var writer = new GlobalCoverageArtifactWriter();
                using (var stagedOutput = writer.StageReplace(outputPath, model!))
                {
                    File.AppendAllText(lease!.SelectedInputs.Should().ContainSingle().Subject.Path, " ");

                    var complete = () => lease.Complete(stagedOutput.Commit);
                    complete.Should().Throw<InvalidDataException>();
                }
            }

            File.Exists(outputPath).Should().BeFalse();
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
    public void OutputCommitFailureRollsBackRawMarkersAndAuthorityClaim()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRun(directory);
            var outputPath = Path.Combine(directory, "session-coverage-result.json");
            Directory.CreateDirectory(outputPath);
            CoverageUtils.TryReadAndCombine(directory, outputPath, authority: null, out var model, out var lease).Should().BeTrue();
            using (lease)
            {
                var writer = new GlobalCoverageArtifactWriter();
                using (var stagedOutput = writer.StageReplace(outputPath, model!))
                {
                    var complete = () => lease!.Complete(stagedOutput.Commit);
                    complete.Should().Throw<IOException>();
                }
            }

            Directory.Exists(outputPath).Should().BeTrue();
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
    public void ExplicitCurrentAuthorityIgnoresClaimLeftByAnInterruptedOlderRun()
    {
        var directory = CreateDirectory();
        try
        {
            ProduceCompleteRunForId(directory, "stale-run");
            var staleClaim = Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().ContainSingle().Subject;
            using var currentOwner = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, "run-id");
            ProduceCompleteRun(directory);
            currentOwner.ReleaseActivity();

            var outputPath = Path.Combine(directory, "session-coverage-result.json");
            using var authority = currentOwner.TakeReconciliationAuthority();
            CoverageUtils.TryReadAndCombine(directory, outputPath, authority, out var model, out var lease).Should().BeTrue();
            using (lease)
            {
                var writer = new GlobalCoverageArtifactWriter();
                using (var stagedOutput = writer.StageReplace(outputPath, model!))
                {
                    lease!.Complete(stagedOutput.Commit);
                }
            }

            File.Exists(outputPath).Should().BeTrue();
            File.Exists(staleClaim).Should().BeTrue("only the explicitly authorized run may be reconciled");
            Directory.GetFiles(directory, ".dd-coverage-command-owner-*.claim").Should().ContainSingle().Which.Should().Be(staleClaim);
            Directory.GetFiles(directory, "coverage-*.json").Should().ContainSingle("the older run's raw artifact remains untouched");
            Directory.GetFiles(directory, ".dd-coverage-process-incomplete-*").Should().ContainSingle();
            Directory.GetFiles(directory, ".dd-coverage-process-ready-*").Should().ContainSingle();
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

                var reader = new GlobalCoverageInputReader();
                reader.TryRead(input.Path, lease.GetCertifiedInput(input.Path), out _).Should().BeFalse();
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
    public void ArchivalPreflightPreservesEveryArtifactWhenLaterInputChanges()
    {
        var coordinatorDirectory = CreateDirectory();
        var secondaryDirectory = CreateDirectory();
        try
        {
            ProduceCompleteRun(coordinatorDirectory, secondaryDirectory);
            GlobalCoverageReconciliation.TryAcquire(coordinatorDirectory, authority: null, out var lease, out var protocolPresent).Should().BeTrue();
            protocolPresent.Should().BeTrue();
            var inputs = lease!.AllRawInputs.Should().HaveCount(2).And.Subject.ToArray();
            var changedInput = inputs[1];
            var originalContents = File.ReadAllBytes(changedInput.Path);
            using (lease)
            {
                File.AppendAllText(changedInput.Path, " ");

                var complete = () => lease.Complete();
                complete.Should().Throw<InvalidDataException>();
                inputs.Should().OnlyContain(input => File.Exists(input.Path));

                File.WriteAllBytes(changedInput.Path, originalContents);
            }

            GlobalCoverageReconciliation.TryAcquire(coordinatorDirectory, authority: null, out var retryLease, out protocolPresent).Should().BeTrue();
            protocolPresent.Should().BeTrue();
            using (retryLease)
            {
                retryLease!.Complete();
            }

            Directory.GetFiles(coordinatorDirectory, "coverage-*.json").Should().BeEmpty();
            Directory.GetFiles(secondaryDirectory, "coverage-*.json").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(coordinatorDirectory, true);
            Directory.Delete(secondaryDirectory, true);
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

    private static void ProduceCompleteRun(string directory) => ProduceCompleteRunForId(directory, "run-id");

    private static void ProduceCompleteRunForId(string directory, string runId)
    {
        using var command = DotnetTestRunState.TryCreate(DotnetTestCommandKind.DotnetTestCommand, null, directory, runId);
        var handler = new DefaultWithGlobalCoverageEventHandler(configuredOutputDirectory: directory, runIdProvider: () => runId);
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

    private sealed class RetiredProbeMetadata : TestModuleCoverageMetadata
    {
        public RetiredProbeMetadata()
            : base(8, 0, [new FileCoverageMetadata("/src/retired-probe.cs", 0, 8, [0xff])])
        {
        }
    }

    private sealed class SealBoundaryProbeMetadata : TestModuleCoverageMetadata
    {
        public SealBoundaryProbeMetadata()
            : base(8, 0, [new FileCoverageMetadata("/src/seal-boundary.cs", 0, 8, [0xff])])
        {
        }
    }

    private sealed class ConcurrentRetiredProbeMetadata : TestModuleCoverageMetadata
    {
        public ConcurrentRetiredProbeMetadata()
            : base(32, 0, [new FileCoverageMetadata("/src/concurrent-retired-probe.cs", 0, 32, [0xff, 0xff, 0xff, 0xff])])
        {
        }
    }

    private sealed class LegacyProbeMetadata : TestModuleCoverageMetadata
    {
        public LegacyProbeMetadata()
            : base(1, 0, [new FileCoverageMetadata("/src/legacy-probe.cs", 0, 1, [0x80])])
        {
        }
    }

    private sealed class RetiredCallCountProbeMetadata : TestModuleCoverageMetadata
    {
        public RetiredCallCountProbeMetadata()
            : base(8, 1, [new FileCoverageMetadata("/src/retired-call-count-probe.cs", 0, 8, [0xff])])
        {
        }
    }
}
