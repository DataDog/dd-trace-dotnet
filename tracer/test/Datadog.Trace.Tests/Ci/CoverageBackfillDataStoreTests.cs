// <copyright file="CoverageBackfillDataStoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.CIVisibility.TestOptimizationRunId,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
    ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory)]
public class CoverageBackfillDataStoreTests
{
    private const string PersistCoverageErrorMessage = "CoverageBackfillDataStore: Error persisting ITR coverage backfill data.";
    private const string PersistActualSkipErrorMessage = "CoverageBackfillDataStore: Error persisting actual ITR skip state.";
    private const string LoadCoverageErrorMessage = "CoverageBackfillDataStore: Error loading ITR coverage backfill data.";
    private const string ReadActualSkipErrorMessage = "CoverageBackfillDataStore: Error reading actual ITR skip state.";
    private const string PersistIpcFailureErrorMessage = "CoverageBackfillDataStore: Error persisting coverage IPC failure state.";
    private const string ReadIpcFailureErrorMessage = "CoverageBackfillDataStore: Error reading coverage IPC failure state.";
    private const string LoadScopedCoverageErrorMessage = "CoverageBackfillDataStore: Error loading scoped ITR coverage backfill data.";
    private const string ReadBackfillFileTimeoutMessage = "CoverageBackfillDataStore: Timed out reading ITR coverage backfill file.";
    private static readonly string DefaultRunId = $"test-run-{Guid.NewGuid():N}";
    private static readonly StringComparison PathComparison = FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    [Fact]
    public void GetOrCreateRunFolderDoesNotMutateEnvironment()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);
            var testOptimization = CreateTestOptimization(workspacePath, runId: DefaultRunId);

            var runFolder = CoverageBackfillDataStore.GetOrCreateRunFolder(testOptimization.Object);

            runFolder.Should().Be(Path.Combine(workspacePath, ".dd", DefaultRunId));
            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder).Should().BeNull();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void GetNewRunFolderIgnoresPropagatedStateForRunner()
    {
        var workspacePath = CreateWorkspacePath();
        var staleWorkingDirectory = CreateWorkspacePath();
        var staleRunFolder = Path.Combine(staleWorkingDirectory, ".dd", "stale-run");
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, staleRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, staleWorkingDirectory);
            var testOptimization = CreateTestOptimization(workspacePath, runId: "new-run");

            CoverageBackfillDataStore.GetNewRunFolder(testOptimization.Object)
                                     .Should()
                                     .Be(Path.Combine(workspacePath, ".dd", "new-run"));
            CoverageBackfillDataStore.GetOrCreateRunFolder(testOptimization.Object).Should().Be(staleRunFolder);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
            DeleteWorkspacePath(staleWorkingDirectory);
        }
    }

    [Fact]
    public void PathEqualityUsesCurrentPlatformComparison()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-CoverageBackfill-{Guid.NewGuid():N}");
        var alternateCasePath = path.Replace("CoverageBackfill", "coveragebackfill");

        InvokePathsEqual(path, alternateCasePath).Should().Be(FrameworkDescription.Instance.IsWindows());
    }

    [Fact]
    public void TryLoadMergesCompleteScopedActualSkipCoverage()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var firstScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var secondScope = new SkippableTestsRequestScope("Samples.NUnitTests", "scope-b");

            CoverageBackfillDataStore.Persist(testOptimization.Object, firstScope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.Persist(testOptimization.Object, secondScope, CreateCoverage("src/Calculator.cs", 0b_0100_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, firstScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, firstScope);
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, secondScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, secondScope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.IsPresent.Should().BeTrue();
            coverageBackfillData.IsValid.Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void ScopedPersistDoesNotExposeScopedJsonAsUnscopedEnvironmentFallback()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");

            CoverageBackfillDataStore.Persist(testOptimization.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath).Should().BeNullOrEmpty();
            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder).Should().Be(Path.Combine(workspacePath, ".dd", DefaultRunId));
            File.Exists(GetScopedBackfillPath(workspacePath, scope)).Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadFailsClosedWhenOnlyTemporaryScopedActualSkipMarkerExists()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Unscoped.cs", 0b_1000_0000));
            var temporaryMarkerPath = GetScopedActualSkipPath(workspacePath, $"{scope.Fingerprint}.{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(temporaryMarkerPath)!);
            File.WriteAllText(temporaryMarkerPath, "1");

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryLoadScopedCoverageWaitsForFinalActualSkipMarkerWhenTemporaryMarkerAppearsFirst()
    {
        var workspacePath = CreateWorkspacePath();
        Task releaseTask = null;
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var temporaryMarkerPath = GetScopedActualSkipPath(workspacePath, $"{scope.Fingerprint}.{Guid.NewGuid():N}.tmp");
            var finalMarkerPath = GetScopedActualSkipPath(workspacePath, scope.Fingerprint);
            var backfillableMarkerPath = GetScopedBackfillableSkipPath(workspacePath, scope.Fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(temporaryMarkerPath)!);
            File.WriteAllText(temporaryMarkerPath, "1");
            CoverageBackfillDataStore.Persist(testOptimization.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            releaseTask = RunAfterDelay(
                () =>
                {
                    File.Move(temporaryMarkerPath, finalMarkerPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backfillableMarkerPath)!);
                    File.WriteAllText(backfillableMarkerPath, "1");
                },
                millisecondsDelay: 25);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryLoadScopedCoverageWaitsForAllTemporaryActualSkipMarkersBeforeMerging()
    {
        var workspacePath = CreateWorkspacePath();
        Task releaseTask = null;
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var firstScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var secondScope = new SkippableTestsRequestScope("Samples.NUnitTests", "scope-b");
            CoverageBackfillDataStore.Persist(testOptimization.Object, firstScope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.Persist(testOptimization.Object, secondScope, CreateCoverage("src/Calculator.cs", 0b_0100_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, firstScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, firstScope);

            var temporaryMarkerPath = GetScopedActualSkipPath(workspacePath, $"{secondScope.Fingerprint}.{Guid.NewGuid():N}.tmp");
            var finalMarkerPath = GetScopedActualSkipPath(workspacePath, secondScope.Fingerprint);
            var backfillableMarkerPath = GetScopedBackfillableSkipPath(workspacePath, secondScope.Fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(temporaryMarkerPath)!);
            File.WriteAllText(temporaryMarkerPath, "1");
            releaseTask = RunAfterDelay(
                () =>
                {
                    File.Move(temporaryMarkerPath, finalMarkerPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backfillableMarkerPath)!);
                    File.WriteAllText(backfillableMarkerPath, "1");
                },
                millisecondsDelay: 25);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
        }
        finally
        {
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadFailsClosedWhenScopedActualSkipCoverageIsIncomplete()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var completeScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var incompleteScope = new SkippableTestsRequestScope("Samples.NUnitTests", "scope-b");

            CoverageBackfillDataStore.Persist(testOptimization.Object, completeScope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, completeScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, completeScope);
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, incompleteScope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadFailsClosedWhenScopedActualSkipCoverageIsIncompleteEvenWithUnscopedFallback()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var incompleteScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");

            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Unscoped.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, incompleteScope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryLoadScopedCoverageWaitsForJsonWhenMarkerAppearsFirst()
    {
        var workspacePath = CreateWorkspacePath();
        Task writerTask = null;
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var backfillPath = GetScopedBackfillPath(workspacePath, scope);
            var contents = JsonHelper.SerializeObject(CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, scope);
            writerTask = WriteAfterDelay(backfillPath, contents);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            if (writerTask is not null)
            {
                await writerTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryLoadScopedCoverageRetriesTransientLockedJsonFile()
    {
        var workspacePath = CreateWorkspacePath();
        Task releaseTask = null;
        FileStream lockedFile = null;
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var backfillPath = GetScopedBackfillPath(workspacePath, scope);

            CoverageBackfillDataStore.Persist(testOptimization.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, scope);

            lockedFile = new FileStream(backfillPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            releaseTask = DisposeAfterDelay(lockedFile);
            lockedFile = null;

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            lockedFile?.Dispose();
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryLoadUnscopedCoverageRetriesTransientLockedJsonFile()
    {
        var workspacePath = CreateWorkspacePath();
        Task releaseTask = null;
        FileStream lockedFile = null;
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var backfillPath = GetUnscopedBackfillPath(workspacePath);

            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            lockedFile = new FileStream(backfillPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            releaseTask = DisposeAfterDelay(lockedFile);
            lockedFile = null;

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            lockedFile?.Dispose();
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadUsesPropagatedRunFolderAcrossDifferentWorkingDirectories()
    {
        var producerWorkspacePath = CreateWorkspacePath();
        var consumerWorkspacePath = CreateWorkspacePath();
        var sharedBasePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(sharedBasePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var producer = CreateTestOptimization(producerWorkspacePath);
            var consumer = CreateTestOptimization(consumerWorkspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");

            CoverageBackfillDataStore.Persist(producer.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(producer.Object, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(producer.Object, scope);

            CoverageBackfillDataStore.TryLoad(consumer.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.IsPresent.Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            DeleteWorkspacePath(producerWorkspacePath);
            DeleteWorkspacePath(consumerWorkspacePath);
            DeleteWorkspacePath(sharedBasePath);
        }
    }

    [Fact]
    public void TryLoadFallbackIsScopedToRunId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var firstRun = CreateTestOptimization(workspacePath, runId: "run-a");
            var secondRun = CreateTestOptimization(workspacePath, runId: "run-b");

            CoverageBackfillDataStore.Persist(firstRun.Object, CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);

            File.Exists(Path.Combine(workspacePath, ".dd", "run-a", "coverage-backfill.json")).Should().BeTrue();
            CoverageBackfillDataStore.TryLoad(secondRun.Object, out var secondRunCoverage).Should().BeFalse();
            secondRunCoverage.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void PersistReplacesExistingCoverageFile()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);

            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Calculator.cs", 0b_0100_0000));

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_0100_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void PersistLogsWarningWhenRunFolderCannotBeCreated()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var blockedRunFolder = Path.Combine(workspacePath, "coverage-backfill-run-folder");
            File.WriteAllText(blockedRunFolder, "not a directory");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, blockedRunFolder);

            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Calculator.cs", 0b_1000_0000));

            log.Verify(
                x => x.Warning(
                    It.IsAny<Exception>(),
                    PersistCoverageErrorMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void RecordActualItrSkipLogsWarningWhenRunFolderCannotBeCreated()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var blockedRunFolder = Path.Combine(workspacePath, "coverage-backfill-run-folder");
            File.WriteAllText(blockedRunFolder, "not a directory");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, blockedRunFolder);

            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, default);

            log.Verify(
                x => x.Warning(
                    It.IsAny<Exception>(),
                    PersistActualSkipErrorMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadLogsDebugWhenPersistedCoverageIsMalformed()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var backfillPath = GetUnscopedBackfillPath(workspacePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backfillPath)!);
            File.WriteAllText(backfillPath, "{ malformed-json");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, backfillPath);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
            log.Verify(
                x => x.Debug(
                    It.IsAny<Exception>(),
                    LoadCoverageErrorMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void HasActualItrSkipLogsDebugWhenRunFolderCannotBeResolved()
    {
        var log = new Mock<IDatadogLogger>();
        var testOptimization = CreateFailingTestOptimization(log.Object);

        CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();

        log.Verify(
            x => x.Debug(
                It.IsAny<Exception>(),
                ReadActualSkipErrorMessage,
                It.IsAny<int>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void TryLoadLogsDebugWhenScopedCoverageJsonIsMalformed()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var backfillPath = GetScopedBackfillPath(workspacePath, scope);

            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, scope);
            Directory.CreateDirectory(Path.GetDirectoryName(backfillPath)!);
            File.WriteAllText(backfillPath, "{ malformed-json");

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
            log.Verify(
                x => x.Debug(
                    It.IsAny<Exception>(),
                    LoadScopedCoverageErrorMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void RecordCoverageIpcFailureLogsWarningWhenRunFolderCannotBeCreated()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var blockedRunFolder = Path.Combine(workspacePath, "coverage-backfill-run-folder");
            File.WriteAllText(blockedRunFolder, "not a directory");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, blockedRunFolder);

            CoverageBackfillDataStore.RecordCoverageIpcFailure(testOptimization.Object, "collector-ipc");

            log.Verify(
                x => x.Warning(
                    It.IsAny<Exception>(),
                    PersistIpcFailureErrorMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcFailureLogsDebugWhenRunFolderCannotBeResolved()
    {
        var log = new Mock<IDatadogLogger>();
        var testOptimization = CreateFailingTestOptimization(log.Object);

        CoverageBackfillDataStore.TryReadCoverageIpcFailure(testOptimization.Object, out var reason).Should().BeFalse();

        reason.Should().BeEmpty();
        log.Verify(
            x => x.Debug(
                It.IsAny<Exception>(),
                ReadIpcFailureErrorMessage,
                It.IsAny<int>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void RecordCoverageIpcFailureCanBeReadFromPropagatedRunFolder()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var missingReason).Should().BeFalse();
            missingReason.Should().BeEmpty();

            CoverageBackfillDataStore.RecordCoverageIpcFailure("collector-ipc");

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var reason).Should().BeTrue();
            reason.Should().Contain("collector-ipc");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcFailureFailsClosedWhenMarkerIsLocked()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Directory.CreateDirectory(sharedRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var markerPath = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-failure");
            File.WriteAllText(markerPath, "collector-ipc");

            using (new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var lockedReason).Should().BeFalse();
                lockedReason.Should().BeEmpty();
            }

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var reason).Should().BeTrue();
            reason.Should().Be("collector-ipc");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcFailureLogsDebugWhenMarkerReadTimesOut()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var log = new Mock<IDatadogLogger>();
            var testOptimization = CreateTestOptimization(workspacePath, log: log.Object);
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Directory.CreateDirectory(sharedRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var markerPath = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-failure");
            File.WriteAllText(markerPath, "collector-ipc");

            using (new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                CoverageBackfillDataStore.TryReadCoverageIpcFailure(testOptimization.Object, out var lockedReason).Should().BeFalse();
                lockedReason.Should().BeEmpty();
            }

            log.Verify(
                x => x.Debug(
                    It.IsAny<Exception>(),
                    ReadBackfillFileTimeoutMessage,
                    It.IsAny<int>(),
                    It.IsAny<string>()),
                Times.Once);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcFailureRetriesTransientLockedMarker()
    {
        var workspacePath = CreateWorkspacePath();
        Task releaseTask = null;
        FileStream lockedFile = null;
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Directory.CreateDirectory(sharedRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var markerPath = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-failure");
            File.WriteAllText(markerPath, "collector-ipc");

            lockedFile = new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            releaseTask = DisposeAfterDelay(lockedFile);
            lockedFile = null;

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var reason).Should().BeTrue();

            reason.Should().Be("collector-ipc");
        }
        finally
        {
            lockedFile?.Dispose();
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task RecordCoverageIpcFailureHandlesConcurrentWriters()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var tasks = new Task[8];
            for (var i = 0; i < tasks.Length; i++)
            {
                var writerId = i;
                tasks[i] = Task.Run(() => CoverageBackfillDataStore.RecordCoverageIpcFailure($"collector-ipc-{writerId}"));
            }

            await Task.WhenAll(tasks);

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var reason).Should().BeTrue();
            reason.Should().Contain("collector-ipc-");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void CoverageIpcResultCanBeReadFromPropagatedRunFolder()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);
            var backfillValidation = CodeCoverageBackfillValidation.Create(
                requiredBackendFilesWithCoverage: 1,
                new Dictionary<string, int>
                {
                    ["src/Calculator.cs"] = 1
                },
                new Dictionary<string, HashSet<int>>
                {
                    ["src/Calculator.cs"] = [1]
                },
                new Dictionary<string, string>
                {
                    ["src/Calculator.cs"] = "repo-a/src/Calculator.cs"
                });

            CoverageBackfillDataStore.TryReadCoverageIpcResults(out var missingResults).Should().BeFalse();
            missingResults.Should().BeEmpty();

            CoverageBackfillDataStore.RecordCoverageIpcResult(
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 80,
                backfilled: true,
                executableLines: 5,
                coveredLines: 4,
                diagnostic: "persisted",
                backfillValidated: true,
                backfillNotApplicable: true,
                backfillValidation: backfillValidation);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(out var results).Should().BeTrue();

            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(80);
            result.Backfilled.Should().BeTrue();
            result.ExecutableLines.Should().Be(5);
            result.CoveredLines.Should().Be(4);
            result.Diagnostic.Should().Be("persisted");
            result.BackfillValidated.Should().BeTrue();
            result.BackfillNotApplicable.Should().BeTrue();
            result.BackfillValidation.Should().NotBeNull();
            result.BackfillValidation!.CanPublish().Should().BeTrue();
            result.BackfillValidation.LocalCandidateByBackendPath.Should().Contain("src/Calculator.cs", "repo-a/src/Calculator.cs");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsForDelayedResultFolderWhenRequested()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization.Object, sessionId: 123, out var missingResults).Should().BeFalse();
            missingResults.Should().BeEmpty();

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true, waitForCoverletXmlFallback: false));
            ShouldStillBeWaiting(readTask);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "delayed-folder");

            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            var result = readResult.Results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
            result.Percentage.Should().Be(75);
            result.Backfilled.Should().BeTrue();
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(3);
            result.Diagnostic.Should().Be("delayed-folder");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsForStableSnapshotWhenLaterResultAppears()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            Directory.CreateDirectory(resultFolder);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "first");

            var secondFinalPath = Path.Combine(resultFolder, "Coverlet-second.json");
            var secondTemporaryPath = secondFinalPath + ".tmp";
            File.WriteAllText(secondTemporaryPath, CreateCoverageIpcResultJson("second-result", CodeCoverageReportSource.Coverlet, 75, true, 4, 3, "second"));

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true, waitForCoverletXmlFallback: false));
            ShouldStillBeWaiting(readTask);
            File.Move(secondTemporaryPath, secondFinalPath);
            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            readResult.Results.Should().HaveCount(2);
            readResult.Results.Should().Contain(result => result.Diagnostic == "first");
            readResult.Results.Should().Contain(result => result.Diagnostic == "second");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsForLateFinalResultAfterNonEmptySnapshot()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "first");

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true));
            var firstCompletedTask = await Task.WhenAny(readTask, Task.Delay(100));
            firstCompletedTask.Should().NotBe(readTask, "the reader should leave time for a higher-priority persisted fallback result");

            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "fallback");

            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            readResult.Results.Should().HaveCount(2);
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet && result.Diagnostic == "first");
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.CoverletXmlFallback && result.Diagnostic == "fallback");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsPastQuietPeriodForCoverletXmlFallback()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "direct-coverlet");

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true));
            await Task.Delay(1_200);
            readTask.IsCompleted.Should().BeFalse("the reader should not publish direct Coverlet while a higher-priority XML fallback may still arrive");

            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            var fallbackFinalPath = Path.Combine(resultFolder, "CoverletXmlFallback-late.json");
            var fallbackTemporaryPath = fallbackFinalPath + ".tmp";
            File.WriteAllText(fallbackTemporaryPath, CreateCoverageIpcResultJson("late-fallback", CodeCoverageReportSource.CoverletXmlFallback, 75, true, 4, 3, "xml-fallback"));
            readTask.IsCompleted.Should().BeFalse("the reader should not publish while the higher-priority XML fallback result is still being written");
            File.Move(fallbackTemporaryPath, fallbackFinalPath);

            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            readResult.Results.Should().HaveCount(2);
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet && result.Diagnostic == "direct-coverlet");
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.CoverletXmlFallback && result.Diagnostic == "xml-fallback");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsForPendingCoverletXmlFallbackTemporaryFile()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "direct-coverlet");

            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            var fallbackFinalPath = Path.Combine(resultFolder, "CoverletXmlFallback-late.json");
            var fallbackTemporaryPath = fallbackFinalPath + ".tmp";
            File.WriteAllText(fallbackTemporaryPath, CreateCoverageIpcResultJson("late-fallback", CodeCoverageReportSource.CoverletXmlFallback, 75, true, 4, 3, "xml-fallback"));

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true));
            await Task.Delay(1_200);
            readTask.IsCompleted.Should().BeFalse("the reader should wait past the normal read timeout when a pending XML fallback can supersede direct Coverlet");

            File.Move(fallbackTemporaryPath, fallbackFinalPath);
            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            readResult.Results.Should().HaveCount(2);
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet && result.Diagnostic == "direct-coverlet");
            readResult.Results.Should().Contain(result => result.Source == CodeCoverageReportSource.CoverletXmlFallback && result.Diagnostic == "xml-fallback");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsStopsAfterQuietPeriodWhenCoverletXmlFallbackCannotArrive()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "direct-coverlet");

            var readTask = Task.Run(() => ReadCoverageIpcResults(testOptimization.Object, sessionId: 123, waitForResultFolder: true, waitForCoverletXmlFallback: false));
            var completedTask = await Task.WhenAny(readTask, Task.Delay(800));

            completedTask.Should().Be(readTask, "the reader should not wait for a Coverlet XML fallback when the selected command cannot produce one");
            var readResult = await readTask;
            readResult.Success.Should().BeTrue();
            var result = readResult.Results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
            result.Diagnostic.Should().Be("direct-coverlet");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void CoverageIpcResultsKeepMultipleSameSourceProducerResults()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var firstResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                CodeCoverageReportSource.Coverlet,
                percentage: 10,
                backfilled: true,
                executableLines: 10,
                coveredLines: 1,
                diagnostic: "first");
            var secondResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                CodeCoverageReportSource.Coverlet,
                percentage: 90,
                backfilled: true,
                executableLines: 10,
                coveredLines: 9,
                diagnostic: "second");

            CoverageBackfillDataStore.TryReadCoverageIpcResults(out var results).Should().BeTrue();

            firstResultId.Should().NotBeNullOrEmpty();
            secondResultId.Should().NotBeNullOrEmpty();
            secondResultId.Should().NotBe(firstResultId);
            results.Should().HaveCount(2);
            results.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet && result.Diagnostic == "first" && result.ResultId == firstResultId);
            results.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet && result.Diagnostic == "second" && result.ResultId == secondResultId);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void StableCoverageIpcResultIdIsWriteOnce()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);

            var firstResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "first",
                resultId: "stable-result");
            var secondResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "second",
                resultId: "stable-result");

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization.Object, sessionId: 123, out var results).Should().BeTrue();

            firstResultId.Should().Be("stable-result");
            secondResultId.Should().Be("stable-result");
            var result = results.Should().ContainSingle().Subject;
            result.ResultId.Should().Be("stable-result");
            result.Percentage.Should().Be(75);
            result.Backfilled.Should().BeTrue();
            result.CoveredLines.Should().Be(3);
            result.Diagnostic.Should().Be("first");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void StableCoverageIpcResultIdReturnsNullWhenPersistenceFails()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var blockingRunFolder = Path.Combine(workspacePath, "run-folder-blocker");
            File.WriteAllText(blockingRunFolder, "not a directory");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, blockingRunFolder);
            var testOptimization = CreateTestOptimization(workspacePath);

            var resultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "stable",
                resultId: "stable-result");

            resultId.Should().BeNull();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void CoverageIpcResultPersistsSupersededResultIds()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);

            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                resultId: "merged-result",
                supersededResultIds: ["partial-a", "partial-b"]);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization.Object, sessionId: 123, out var results).Should().BeTrue();

            var result = results.Should().ContainSingle().Subject;
            result.ResultId.Should().Be("merged-result");
            result.SupersededResultIds.Should().Equal("partial-a", "partial-b");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultReadsFullBackfillMetadata()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var backfillValidation = CodeCoverageBackfillValidation.Create(
                requiredBackendFilesWithCoverage: 1,
                expectedCoveredLinesByBackendPath: new Dictionary<string, int>
                {
                    ["src/Calculator.cs"] = 2
                },
                representedBackendLinesByBackendPath: new Dictionary<string, HashSet<int>>
                {
                    ["src/Calculator.cs"] = [10, 11]
                },
                localCandidateByBackendPath: new Dictionary<string, string>
                {
                    ["src/Calculator.cs"] = "/repo/src/Calculator.cs"
                },
                requiredBackendPathsWithCoverage: ["src/Calculator.cs"],
                requiredBackendLinesByBackendPath: new Dictionary<string, HashSet<int>>
                {
                    ["src/Calculator.cs"] = [10, 11]
                });

            var resultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "persisted-reference",
                resultId: "merged-result",
                backfillValidated: true,
                backfillValidation: backfillValidation,
                supersededResultIds: ["partial-a", "partial-b"]);

            resultId.Should().Be("merged-result");

            CoverageBackfillDataStore.TryReadCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                resultId,
                out var result).Should().BeTrue();

            result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
            result.ResultId.Should().Be("merged-result");
            result.Percentage.Should().Be(75);
            result.Backfilled.Should().BeTrue();
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(3);
            result.Diagnostic.Should().Be("persisted-reference");
            result.BackfillValidated.Should().BeTrue();
            result.BackfillValidation.Should().NotBeNull();
            result.BackfillValidation.CanPublish().Should().BeTrue();
            result.BackfillValidation.LocalCandidateByBackendPath.Should().Contain("src/Calculator.cs", "/repo/src/Calculator.cs");
            result.SupersededResultIds.Should().Equal("partial-a", "partial-b");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Theory]
    [InlineData(456, nameof(CodeCoverageReportSource.CoverletXmlFallback), "merged-result")]
    [InlineData(123, nameof(CodeCoverageReportSource.Coverlet), "merged-result")]
    [InlineData(123, nameof(CodeCoverageReportSource.CoverletXmlFallback), "missing-result")]
    [InlineData(123, nameof(CodeCoverageReportSource.CoverletXmlFallback), "")]
    [InlineData(123, nameof(CodeCoverageReportSource.CoverletXmlFallback), null)]
    public void TryReadCoverageIpcResultRejectsWrongReference(ulong sessionId, string sourceName, string resultId)
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "persisted-reference",
                resultId: "merged-result");

            var source = (CodeCoverageReportSource)Enum.Parse(typeof(CodeCoverageReportSource), sourceName);
            CoverageBackfillDataStore.TryReadCoverageIpcResult(
                testOptimization.Object,
                sessionId,
                source,
                resultId,
                out var result).Should().BeFalse();

            result.Should().Be(default(CodeCoverageAggregationResult));
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultRejectsInvalidJson()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "persisted-reference",
                resultId: "merged-result");
            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            var resultFile = Directory.GetFiles(resultFolder, "*.json", SearchOption.TopDirectoryOnly).Should().ContainSingle().Subject;
            File.WriteAllText(resultFile, "{ invalid json");

            CoverageBackfillDataStore.TryReadCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                "merged-result",
                out var result).Should().BeFalse();

            result.Should().Be(default(CodeCoverageAggregationResult));
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Theory]
    [InlineData(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage), "merged-result")]
    [InlineData(nameof(CodeCoverageReportSource.Coverlet), "other-result")]
    public void TryReadCoverageIpcResultRejectsTamperedResultPayload(string embeddedSourceName, string embeddedResultId)
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "persisted-reference",
                resultId: "merged-result");
            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            var resultFile = Directory.GetFiles(resultFolder, "*.json", SearchOption.TopDirectoryOnly).Should().ContainSingle().Subject;
            var embeddedSource = (CodeCoverageReportSource)Enum.Parse(typeof(CodeCoverageReportSource), embeddedSourceName);
            File.WriteAllText(resultFile, CreateCoverageIpcResultJson(embeddedResultId, embeddedSource, 75, true, 4, 3, "tampered"));

            CoverageBackfillDataStore.TryReadCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.Coverlet,
                "merged-result",
                out var result).Should().BeFalse();

            result.Should().Be(default(CodeCoverageAggregationResult));
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void StableCoverageIpcResultIdIsEncodedForFileNameOnly()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var unsafeResultId = "../unsafe\\result/id:" + new string('x', 128);

            var resultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                testOptimization.Object,
                sessionId: 123,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 75,
                backfilled: true,
                executableLines: 4,
                coveredLines: 3,
                diagnostic: "unsafe",
                resultId: unsafeResultId);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization.Object, sessionId: 123, out var results).Should().BeTrue();

            resultId.Should().Be(unsafeResultId);
            results.Should().ContainSingle().Subject.ResultId.Should().Be(unsafeResultId);
            var resultFolder = GetIpcResultFolder(workspacePath, sessionId: 123);
            var directFiles = Directory.GetFiles(resultFolder, "*.json", SearchOption.TopDirectoryOnly);
            var allFiles = Directory.GetFiles(resultFolder, "*.json", SearchOption.AllDirectories);
            directFiles.Should().ContainSingle();
            allFiles.Should().Equal(directFiles);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task StableCoverageIpcResultIdIsWriteOnceForConcurrentWriters()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var firstWriter = Task.Run(
                () => CoverageBackfillDataStore.RecordCoverageIpcResult(
                    testOptimization.Object,
                    sessionId: 123,
                    CodeCoverageReportSource.CoverletXmlFallback,
                    percentage: 75,
                    backfilled: true,
                    executableLines: 4,
                    coveredLines: 3,
                    diagnostic: "first",
                    resultId: "stable-result"));
            var secondWriter = Task.Run(
                () => CoverageBackfillDataStore.RecordCoverageIpcResult(
                    testOptimization.Object,
                    sessionId: 123,
                    CodeCoverageReportSource.CoverletXmlFallback,
                    percentage: 25,
                    backfilled: false,
                    executableLines: 4,
                    coveredLines: 1,
                    diagnostic: "second",
                    resultId: "stable-result"));

            var resultIds = await Task.WhenAll(firstWriter, secondWriter);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization.Object, sessionId: 123, out var results).Should().BeTrue();

            resultIds.Should().Equal("stable-result", "stable-result");
            var result = results.Should().ContainSingle().Subject;
            result.ResultId.Should().Be("stable-result");
            result.Diagnostic.Should().BeOneOf("first", "second");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcResultsWaitsForAtomicTemporaryResultFile()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            var resultFolder = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-results");
            Directory.CreateDirectory(resultFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var finalPath = Path.Combine(resultFolder, "Coverlet-delayed.json");
            var temporaryPath = finalPath + ".tmp";
            var contents = CreateCoverageIpcResultJson("delayed-result", CodeCoverageReportSource.Coverlet, 80, true, 5, 4, "delayed");
            File.WriteAllText(temporaryPath, contents);

            var readTask = Task.Run(ReadCoverageIpcResults);
            ShouldStillBeWaiting(readTask);
            File.Move(temporaryPath, finalPath);
            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            var result = readResult.Results.Should().ContainSingle().Subject;
            result.ResultId.Should().Be("delayed-result");
            result.Percentage.Should().Be(80);
            result.Diagnostic.Should().Be("delayed");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultsDoesNotPublishPartialSnapshotWhenTemporaryResultDoesNotComplete()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            var resultFolder = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-results", "session-123");
            Directory.CreateDirectory(resultFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var finalPath = Path.Combine(resultFolder, "Coverlet-first.json");
            var temporaryPath = Path.Combine(resultFolder, "Coverlet-second.json.tmp");
            File.WriteAllText(finalPath, CreateCoverageIpcResultJson("first-result", CodeCoverageReportSource.Coverlet, 25, false, 4, 1, "first"));
            File.WriteAllText(temporaryPath, CreateCoverageIpcResultJson("second-result", CodeCoverageReportSource.Coverlet, 75, true, 4, 3, "second"));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization: CreateTestOptimization(workspacePath).Object, sessionId: 123, waitForResultFolder: true, waitForCoverletXmlFallback: false, out var results, out var readFailed).Should().BeFalse();

            results.Should().BeEmpty();
            readFailed.Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultsNormalReadDoesNotPublishPartialSnapshotWhenTemporaryResultDoesNotComplete()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            var resultFolder = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-results", "session-123");
            Directory.CreateDirectory(resultFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var finalPath = Path.Combine(resultFolder, "Coverlet-first.json");
            var temporaryPath = Path.Combine(resultFolder, "Coverlet-second.json.tmp");
            File.WriteAllText(finalPath, CreateCoverageIpcResultJson("first-result", CodeCoverageReportSource.Coverlet, 25, false, 4, 1, "first"));
            File.WriteAllText(temporaryPath, CreateCoverageIpcResultJson("second-result", CodeCoverageReportSource.Coverlet, 75, true, 4, 3, "second"));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(CreateTestOptimization(workspacePath).Object, sessionId: 123, waitForResultFolder: false, out var results, out var readFailed).Should().BeFalse();

            results.Should().BeEmpty();
            readFailed.Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultsDoesNotPublishPartialSnapshotWhenFinalResultIsInvalid()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            var resultFolder = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-results", "session-123");
            Directory.CreateDirectory(resultFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            File.WriteAllText(Path.Combine(resultFolder, "Coverlet-first.json"), CreateCoverageIpcResultJson("first-result", CodeCoverageReportSource.Coverlet, 25, false, 4, 1, "first"));
            File.WriteAllText(Path.Combine(resultFolder, "Coverlet-second.json"), "{not-json");

            CoverageBackfillDataStore.TryReadCoverageIpcResults(CreateTestOptimization(workspacePath).Object, sessionId: 123, waitForResultFolder: false, out var results, out var readFailed).Should().BeFalse();

            results.Should().BeEmpty();
            readFailed.Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultsDoesNotReportReadFailureWhenNoResultFolderExists()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Directory.CreateDirectory(sharedRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(CreateTestOptimization(workspacePath).Object, sessionId: 123, waitForResultFolder: false, out var results, out var readFailed).Should().BeFalse();

            results.Should().BeEmpty();
            readFailed.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryReadCoverageIpcResultsReportsReadFailureWhenExpectedResultFolderStaysEmpty()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            var resultFolder = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-results", "session-123");
            Directory.CreateDirectory(resultFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(CreateTestOptimization(workspacePath).Object, sessionId: 123, waitForResultFolder: true, out var results, out var readFailed).Should().BeFalse();

            results.Should().BeEmpty();
            readFailed.Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public async Task TryReadCoverageIpcFailureWaitsForAtomicTemporaryMarkerFile()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Directory.CreateDirectory(sharedRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            var markerPath = Path.Combine(sharedRunFolder, "coverage-backfill-ipc-failure");
            var temporaryPath = markerPath + ".tmp";
            File.WriteAllText(temporaryPath, "collector-ipc");

            var readTask = Task.Run(ReadCoverageIpcFailure);
            ShouldStillBeWaiting(readTask);
            File.Move(temporaryPath, markerPath);
            var readResult = await readTask;

            readResult.Success.Should().BeTrue();
            readResult.Reason.Should().Be("collector-ipc");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void CoverageIpcResultIsReadOnlyForMatchingSessionId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            CoverageBackfillDataStore.RecordCoverageIpcResult(
                sessionId: 123,
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 80,
                backfilled: true,
                executableLines: 5,
                coveredLines: 4,
                diagnostic: "matching-session");

            CoverageBackfillDataStore.TryReadCoverageIpcResults(sessionId: 456, out var missingResults).Should().BeFalse();
            missingResults.Should().BeEmpty();

            CoverageBackfillDataStore.TryReadCoverageIpcResults(sessionId: 123, out var results).Should().BeTrue();
            results.Should().ContainSingle().Which.Diagnostic.Should().Be("matching-session");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void CoverageIpcFailureIsReadOnlyForMatchingSessionId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var sharedRunFolder = Path.Combine(workspacePath, ".dd", DefaultRunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, sharedRunFolder);

            CoverageBackfillDataStore.RecordCoverageIpcFailure(sessionId: 123, "collector-ipc");

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(sessionId: 456, out var missingReason).Should().BeFalse();
            missingReason.Should().BeEmpty();

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(sessionId: 123, out var reason).Should().BeTrue();
            reason.Should().Contain("collector-ipc");
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void ActualItrSkipIsReadOnlyForMatchingSessionId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);

            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, sessionId: 123, default);

            CoverageBackfillDataStore.HasPersistedActualItrSkip(testOptimization.Object, sessionId: 456).Should().BeFalse();
            CoverageBackfillDataStore.HasPersistedActualItrSkip(testOptimization.Object, sessionId: 123).Should().BeTrue();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void ScopedActualSkipCoverageIsReadOnlyForMatchingSessionId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");

            CoverageBackfillDataStore.Persist(testOptimization.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, sessionId: 123, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, sessionId: 123, scope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 456, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 123, out var coverageBackfillData).Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadForSessionPrefersUnscopedCoverageOverLegacyScopedCoverage()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var legacyScope = new SkippableTestsRequestScope("Samples.XUnitTests", "legacy-scope");

            CoverageBackfillDataStore.Persist(testOptimization.Object, legacyScope, CreateCoverage("src/Legacy.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, legacyScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, legacyScope);
            CoverageBackfillDataStore.Persist(testOptimization.Object, CreateCoverage("src/Current.cs", 0b_0100_0000));

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 456, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Current.cs");
            coverageBackfillData.ExecutedLinesByRelativePath.Should().NotContainKey("src/Legacy.cs");
            coverageBackfillData.ExecutedLinesByRelativePath["src/Current.cs"].Should().Equal([0b_0100_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadForSessionFailsClosedWhenUnscopedCoverageIsInvalidEvenWithLegacyScopedCoverage()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var legacyScope = new SkippableTestsRequestScope("Samples.XUnitTests", "legacy-scope");
            var unscopedPath = GetUnscopedBackfillPath(workspacePath);

            CoverageBackfillDataStore.Persist(testOptimization.Object, legacyScope, CreateCoverage("src/Legacy.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, legacyScope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, legacyScope);
            Directory.CreateDirectory(Path.GetDirectoryName(unscopedPath)!);
            File.WriteAllText(unscopedPath, "{broken");

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 456, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void BackfillableSkipScopeIsReadOnlyForMatchingSessionId()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var scope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");

            CoverageBackfillDataStore.Persist(testOptimization.Object, scope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, sessionId: 123, scope);
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(testOptimization.Object, sessionId: 123, scope);
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, sessionId: 456, scope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 456, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, sessionId: 123, out var coverageBackfillData).Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1000_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    private static Mock<ITestOptimization> CreateTestOptimization(string workspacePath, string runId = null, IDatadogLogger log = null)
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.RunId).Returns(runId ?? DefaultRunId);
        testOptimization.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(workspacePath));
        testOptimization.Setup(x => x.Log).Returns(log ?? DatadogLogging.GetLoggerFor(typeof(CoverageBackfillDataStoreTests)));
        return testOptimization;
    }

    private static Mock<ITestOptimization> CreateFailingTestOptimization(IDatadogLogger log)
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.Log).Returns(log);
        testOptimization.Setup(x => x.CIValues).Throws(new InvalidOperationException("CI values are unavailable."));
        return testOptimization;
    }

    private static CoverageBackfillData CreateCoverage(string path, byte bitmap)
        => CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                [path] = Convert.ToBase64String([bitmap])
            });

    private static string GetUnscopedBackfillPath(string workspacePath)
        => Path.Combine(workspacePath, ".dd", DefaultRunId, "coverage-backfill.json");

    private static string GetScopedBackfillPath(string workspacePath, SkippableTestsRequestScope scope)
        => Path.Combine(workspacePath, ".dd", DefaultRunId, "coverage-backfill-scopes", $"{scope.Fingerprint}.json");

    private static string GetScopedActualSkipPath(string workspacePath, string scopeFingerprint)
        => Path.Combine(workspacePath, ".dd", DefaultRunId, "coverage-backfill-actual-skip-scopes", scopeFingerprint);

    private static string GetScopedBackfillableSkipPath(string workspacePath, string scopeFingerprint)
        => Path.Combine(workspacePath, ".dd", DefaultRunId, "coverage-backfill-backfillable-skip-scopes", scopeFingerprint);

    private static string GetIpcResultFolder(string workspacePath, ulong sessionId)
        => Path.Combine(workspacePath, ".dd", DefaultRunId, "coverage-backfill-ipc-results", $"session-{sessionId}");

    private static string CreateCoverageIpcResultJson(string resultId, CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string diagnostic, bool backfillValidated = false, bool backfillNotApplicable = false)
        => JsonHelper.SerializeObject(
            new CoverageIpcResultForTests
            {
                ResultId = resultId,
                Source = source,
                Percentage = percentage,
                Backfilled = backfilled,
                ExecutableLines = executableLines,
                CoveredLines = coveredLines,
                Diagnostic = diagnostic,
                BackfillValidated = backfillValidated,
                BackfillNotApplicable = backfillNotApplicable,
            });

    private static Task WriteAfterDelay(string filePath, string contents)
        => RunAfterDelay(
            () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var tempPath = filePath + ".tmp";
                File.WriteAllText(tempPath, contents);
                File.Move(tempPath, filePath);
            },
            millisecondsDelay: 50);

    private static Task DisposeAfterDelay(IDisposable disposable)
        => RunAfterDelay(disposable.Dispose, millisecondsDelay: 50);

    private static Task RunAfterDelay(Action action, int millisecondsDelay)
        => Task.Factory.StartNew(
            () =>
            {
                Thread.Sleep(millisecondsDelay);
                action();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static CoverageIpcResultsReadResult ReadCoverageIpcResults()
    {
        var success = CoverageBackfillDataStore.TryReadCoverageIpcResults(out var results);
        return new CoverageIpcResultsReadResult(success, results);
    }

    private static CoverageIpcResultsReadResult ReadCoverageIpcResults(ITestOptimization testOptimization, ulong sessionId, bool waitForResultFolder, bool waitForCoverletXmlFallback = true)
    {
        var success = CoverageBackfillDataStore.TryReadCoverageIpcResults(testOptimization, sessionId, waitForResultFolder, waitForCoverletXmlFallback, out var results, out _);
        return new CoverageIpcResultsReadResult(success, results);
    }

    private static CoverageIpcFailureReadResult ReadCoverageIpcFailure()
    {
        var success = CoverageBackfillDataStore.TryReadCoverageIpcFailure(out var reason);
        return new CoverageIpcFailureReadResult(success, reason);
    }

    private static void ShouldStillBeWaiting(Task task)
    {
        SpinWait.SpinUntil(() => task.Status == TaskStatus.Running || task.IsCompleted, TimeSpan.FromSeconds(1))
                .Should().BeTrue("the reader task should start before asserting that it is waiting");
        task.Wait(TimeSpan.FromMilliseconds(50)).Should().BeFalse("the reader should wait for the delayed coverage IPC artifact");
    }

    private static string CreateWorkspacePath()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-backfill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        return workspacePath;
    }

    private static void DeleteWorkspacePath(string workspacePath)
    {
        DeleteCurrentDirectoryMirrors(workspacePath);
        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    private static void DeleteCurrentDirectoryMirrors(string workspacePath)
    {
        var workspaceRunFolderRoot = Path.Combine(workspacePath, ".dd");
        if (!Directory.Exists(workspaceRunFolderRoot))
        {
            return;
        }

        foreach (var workspaceRunFolder in Directory.EnumerateDirectories(workspaceRunFolderRoot))
        {
            var runId = Path.GetFileName(workspaceRunFolder);
            if (StringUtil.IsNullOrEmpty(runId))
            {
                continue;
            }

            var currentDirectoryMirror = Path.Combine(Environment.CurrentDirectory, ".dd", runId);
            if (string.Equals(Path.GetFullPath(workspaceRunFolder), Path.GetFullPath(currentDirectoryMirror), PathComparison) ||
                !Directory.Exists(currentDirectoryMirror))
            {
                continue;
            }

            Directory.Delete(currentDirectoryMirror, recursive: true);
        }
    }

    private static bool InvokePathsEqual(string left, string right)
    {
        return (bool)typeof(CoverageBackfillDataStore)
                     .GetMethod("PathsEqual", BindingFlags.Static | BindingFlags.NonPublic)!
                     .Invoke(null, [left, right])!;
    }

    private readonly struct CoverageIpcResultsReadResult
    {
        public CoverageIpcResultsReadResult(bool success, CodeCoverageAggregationResult[] results)
        {
            Success = success;
            Results = results;
        }

        public bool Success { get; }

        public CodeCoverageAggregationResult[] Results { get; }
    }

    private readonly struct CoverageIpcFailureReadResult
    {
        public CoverageIpcFailureReadResult(bool success, string reason)
        {
            Success = success;
            Reason = reason;
        }

        public bool Success { get; }

        public string Reason { get; }
    }

    private sealed class CoverageIpcResultForTests
    {
        public string ResultId { get; set; }

        public CodeCoverageReportSource Source { get; set; }

        public double Percentage { get; set; }

        public bool Backfilled { get; set; }

        public bool BackfillValidated { get; set; }

        public bool BackfillNotApplicable { get; set; }

        public double? ExecutableLines { get; set; }

        public double? CoveredLines { get; set; }

        public string Diagnostic { get; set; }
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
