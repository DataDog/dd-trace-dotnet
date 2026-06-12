// <copyright file="TestSessionCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath,
    ConfigurationKeys.CIVisibility.TestSessionCommand,
    ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder,
    "X_DATADOG_TRACE_ID",
    "X_DATADOG_PARENT_ID",
    "X_DATADOG_SAMPLING_PRIORITY",
    "X_DATADOG_ORIGIN",
    "X_DATADOG_TAGS",
    "TRACEPARENT",
    "TRACESTATE",
    "BAGGAGE",
    "B3",
    "X_B3_TRACEID",
    "X_B3_SPANID",
    "X_B3_SAMPLED",
    "X_B3_FLAGS")]
public class TestSessionCoverageTests
{
    [Fact]
    public void ClosePublishesCoverageWithoutExplicitPublish()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 75, backfilled: true, executableLines: 4, coveredLines: 3);

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(75);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void CloseRepublishesCoverageRecordedAfterEarlierPublish()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 0, backfilled: false, executableLines: 1, coveredLines: 0);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(0);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();

            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1);

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void PublishCodeCoverageClearsStaleBackfilledTagWhenBestResultIsNotBackfilled()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1);
            session.PublishCodeCoverage();

            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");

            session.RecordCodeCoverage(CodeCoverageReportSource.ExternalXml, 50, backfilled: false, executableLines: 2, coveredLines: 1);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void MultipleSameSourceRawCoverageResultsFailClosedToFallbackAfterDeduplication()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 1, coveredLines: 1, resultId: "fallback-a");
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 1, coveredLines: 1, resultId: "fallback-a");
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 50, backfilled: true, executableLines: 2, coveredLines: 1, resultId: "fallback-b");

            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(0);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void MultipleSameSourceCoverletResultsFailClosedToInternalCoverage()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "coverlet-a", resultId: "coverlet-a");
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "coverlet-b", resultId: "coverlet-b");

            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(25);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void MergedXmlFallbackReplacesPartialAttachmentResults()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 0, backfilled: true, executableLines: 2, coveredLines: 0, resultId: "fallback-a");
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 2, coveredLines: 2, resultId: "fallback-b");

            session.RecordMergedCodeCoverage(
                CodeCoverageReportSource.CoverletXmlFallback,
                50,
                backfilled: true,
                executableLines: 2,
                coveredLines: 1,
                resultId: "fallback-group",
                supersededResultIds: ["fallback-a", "fallback-b"]);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 2, coveredLines: 2, resultId: "fallback-b");
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void DuplicateMergedXmlFallbackStillDeduplicatesSupersededAttachmentResults()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 50, backfilled: true, executableLines: 2, coveredLines: 1, resultId: "fallback-group");
            session.RecordMergedCodeCoverage(
                CodeCoverageReportSource.CoverletXmlFallback,
                50,
                backfilled: true,
                executableLines: 2,
                coveredLines: 1,
                resultId: "fallback-group",
                supersededResultIds: ["fallback-a", "fallback-b"]);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 2, coveredLines: 2, resultId: "fallback-b");
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void IpcMergedXmlFallbackReplacesPartialAttachmentResults()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 0, backfilled: true, executableLines: 2, coveredLines: 0, resultId: "fallback-a");
            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage(
                    CodeCoverageReportSource.CoverletXmlFallback,
                    50,
                    backfilled: true,
                    executableLines: 2,
                    coveredLines: 1,
                    resultId: "fallback-group",
                    supersededResultIds: ["fallback-a", "fallback-b"]));
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 2, coveredLines: 2, resultId: "fallback-b");
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void PersistedMergedXmlFallbackReplacesPartialAttachmentResults()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0);
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 0, backfilled: true, executableLines: 2, coveredLines: 0, resultId: "fallback-a");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                TestOptimization.Instance,
                session.Tags.SessionId,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 50,
                backfilled: true,
                executableLines: 2,
                coveredLines: 1,
                resultId: "fallback-group",
                supersededResultIds: ["fallback-a", "fallback-b"]);

            session.RecordPersistedCoverageIpcResults().Should().BeTrue();
            session.RecordCodeCoverage(CodeCoverageReportSource.CoverletXmlFallback, 100, backfilled: true, executableLines: 2, coveredLines: 2, resultId: "fallback-b");
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void SuppressUnvalidatedCodeCoverageKeepsValidatedCoverletResult()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0, diagnostic: "stale-coverlet");
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "validated-coverlet", backfillValidated: true);

            session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.Coverlet);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void SuppressUnvalidatedCodeCoverageRejectsNotApplicableCoverletResultWhenSameSourceCountsAreAmbiguous()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "not-applicable-coverlet", backfillNotApplicable: true);
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "validated-coverlet", backfillValidated: true);
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 0, backfilled: false, executableLines: 1, coveredLines: 0, diagnostic: "stale-coverlet");

            session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.Coverlet);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(25);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void SuppressUnvalidatedCodeCoverageRemovesNotApplicableCoverletResultWithoutValidatedCoverletResult()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "not-applicable-coverlet", backfillNotApplicable: true);

            session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.Coverlet);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(25);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void SuppressUnvalidatedCodeCoverageRejectsLateNotApplicableCoverletResultWhenSameSourceCountsAreAmbiguous()
    {
        var session = CreateSession();
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
            session.SuppressUnvalidatedCodeCoverageResult(CodeCoverageReportSource.Coverlet);
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "late-not-applicable-coverlet", backfillNotApplicable: true);
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "late-validated-coverlet", backfillValidated: true);
            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(25);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void FinalizeCoverageResultsBeforeSessionCloseSuppressesLocalExternalCoverageWhenProcessingThrowsAfterActualItrSkip()
    {
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-finalize-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            session.RecordCodeCoverage(CodeCoverageReportSource.Coverlet, 80, backfilled: false, executableLines: 5, coveredLines: 4);
            session.RecordCodeCoverage(CodeCoverageReportSource.MicrosoftCodeCoverage, 60, backfilled: false, executableLines: 5, coveredLines: 3);

            DotnetCommon.FinalizeCoverageResultsBeforeSessionClose(
                session,
                _ => throw new InvalidOperationException("forced coverage finalization failure"));
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void IpcCoverageMessageProcessesWhileSessionIsClosingButNotFinished()
    {
        var session = CreateSession();
        try
        {
            SetPrivateField(session, "_closing", 1);

            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage
                {
                    Source = CodeCoverageReportSource.Coverlet,
                    Value = 80,
                    Backfilled = true,
                    ExecutableLines = 5,
                    CoveredLines = 4,
                });

            session.PublishCodeCoverage();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(80);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            SetPrivateField(session, "_closing", 0);
            CloseAndReset(session);
        }
    }

    [Fact]
    public async Task FinalizeCoverageResultsBeforeSessionCloseWaitsForActiveIpcCallbackBeforeCoverletXmlFallback()
    {
        var session = CreateSession();
        var delegateEntered = new ManualResetEventSlim();
        try
        {
            SetPrivateField(session, "_activeIpcCallbacks", 1);
            var stopwatch = Stopwatch.StartNew();
            var delegateElapsed = TimeSpan.Zero;
            var finalizeTask = Task.Run(
                () => DotnetCommon.FinalizeCoverageResultsBeforeSessionClose(
                    session,
                    _ =>
                    {
                        delegateElapsed = stopwatch.Elapsed;
                        delegateEntered.Set();
                        return DotnetCommon.CoverletCollectorXmlProcessingResult.NotApplicable;
                    }));

            delegateEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            delegateElapsed.Should().BeGreaterThan(
                TimeSpan.FromMilliseconds(50),
                "coverage finalization should wait briefly for active IPC callbacks before processing fallback coverage");
            SetPrivateField(session, "_activeIpcCallbacks", 0);

            var completedTask = await Task.WhenAny(finalizeTask, Task.Delay(TimeSpan.FromSeconds(5)));
            completedTask.Should().Be(finalizeTask);
            await finalizeTask;
            delegateEntered.IsSet.Should().BeTrue();
        }
        finally
        {
            SetPrivateField(session, "_activeIpcCallbacks", 0);
            CloseAndReset(session);
            delegateEntered.Dispose();
        }
    }

    [Fact]
    public void ClosePublishesCoverageReceivedThroughRealIpcBeforeFinalPublish()
    {
        var session = CreateSession();
        try
        {
            session.EnableIpcServer().Should().BeTrue();

            using var client = new IpcClient($"session_{session.Tags.SessionId}");
            SendCoverageMessage(client, 80, backfilled: true, executableLines: 5, coveredLines: 4);

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(80);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackSendsCoverageToParentSession()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var openCoverFile = Path.Combine(coverageDirectory, "coverage.opencover.xml");
        var openCoverXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="1" sequenceCoverage="100" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="Samples/TestSuite.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="1" sequenceCoverage="100" />
                      <Methods>
                        <Method>
                          <Summary numSequencePoints="1" visitedSequencePoints="1" sequenceCoverage="100" />
                          <SequencePoints>
                            <SequencePoint vc="1" sl="23" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            NormalizeDirectorySeparators(Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory)).Should().Be(NormalizeDirectorySeparators(workingDirectory));

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);
            File.WriteAllText(openCoverFile, openCoverXml);
            File.SetLastWriteTimeUtc(openCoverFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackMergedAttachmentsPersistSupersededResultIds()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var firstCoverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        var secondCoverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(firstCoverageDirectory);
        Directory.CreateDirectory(secondCoverageDirectory);
        var firstCoverageFile = Path.Combine(firstCoverageDirectory, "coverage.cobertura.xml");
        var secondCoverageFile = Path.Combine(secondCoverageDirectory, "coverage.cobertura.xml");
        var firstCoverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" lines-covered="0" lines-valid="2">
              <packages>
                <package line-rate="0" lines-covered="0" lines-valid="2">
                  <classes>
                    <class filename="Samples/First.cs" line-rate="0">
                      <lines>
                        <line number="10" hits="0" />
                        <line number="11" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var secondCoverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1" lines-covered="2" lines-valid="2">
              <packages>
                <package line-rate="1" lines-covered="2" lines-valid="2">
                  <classes>
                    <class filename="Samples/Second.cs" line-rate="1">
                      <lines>
                        <line number="20" hits="1" />
                        <line number="21" hits="1" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();

            File.WriteAllText(firstCoverageFile, firstCoverageXml);
            File.SetLastWriteTimeUtc(firstCoverageFile, DateTime.UtcNow);
            File.WriteAllText(secondCoverageFile, secondCoverageXml);
            File.SetLastWriteTimeUtc(secondCoverageFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var persistedResults).Should().BeTrue();
            var persistedResult = persistedResults.Should().ContainSingle().Subject;
            persistedResult.Percentage.Should().Be(50);
            persistedResult.SupersededResultIds.Should().HaveCount(2);

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackCanSelectLowerPriorityReportWhenMergedSetValidates()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var firstCoverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        var secondCoverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(firstCoverageDirectory);
        Directory.CreateDirectory(secondCoverageDirectory);
        var firstCoberturaFile = Path.Combine(firstCoverageDirectory, "coverage.cobertura.xml");
        var firstOpenCoverFile = Path.Combine(firstCoverageDirectory, "coverage.opencover.xml");
        var secondCoberturaFile = Path.Combine(secondCoverageDirectory, "coverage.cobertura.xml");
        var firstCoberturaXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" lines-covered="0" lines-valid="1">
              <packages>
                <package line-rate="0" lines-covered="0" lines-valid="1">
                  <classes>
                    <class filename="Samples/First.cs" line-rate="0">
                      <lines>
                        <line number="9" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var firstOpenCoverXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="Samples/First.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method visited="false">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="10" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var secondCoberturaXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" lines-covered="0" lines-valid="1">
              <packages>
                <package line-rate="0" lines-covered="0" lines-valid="1">
                  <classes>
                    <class filename="Samples/Second.cs" line-rate="0">
                      <lines>
                        <line number="20" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            CoverageBackfillDataStore.Persist(
                TestOptimization.Instance,
                BackfillForLineMap(
                    new Dictionary<string, int>
                    {
                        ["Samples/First.cs"] = 10,
                        ["Samples/Second.cs"] = 20
                    }));
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);

            File.WriteAllText(firstCoberturaFile, firstCoberturaXml);
            File.SetLastWriteTimeUtc(firstCoberturaFile, DateTime.UtcNow);
            File.WriteAllText(firstOpenCoverFile, firstOpenCoverXml);
            File.SetLastWriteTimeUtc(firstOpenCoverFile, DateTime.UtcNow);
            File.WriteAllText(secondCoberturaFile, secondCoberturaXml);
            File.SetLastWriteTimeUtc(secondCoberturaFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var persistedResults).Should().BeTrue();
            var persistedResult = persistedResults.Should().ContainSingle().Subject;
            persistedResult.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
            persistedResult.Percentage.Should().Be(100);
            persistedResult.Backfilled.Should().BeTrue();
            persistedResult.SupersededResultIds.Should().HaveCount(2);
            File.ReadAllText(firstOpenCoverFile).Should().Contain("""<SequencePoint vc="1" sl="10" fileid="1" />""");
            File.ReadAllText(secondCoberturaFile).Should().Contain("""<line number="20" hits="1" />""");
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackDeduplicatesRepeatedIpcWhenPersistenceFails()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var blockingRunFolder = Path.Combine(runFolderBaseDirectory, "run-folder-blocker");
            File.WriteAllText(blockingRunFolder, "not a directory");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, blockingRunFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();
            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Theory]
    [InlineData("dotnet test @coverage.rsp")]
    [InlineData("vstest sample.dll @coverage.rsp")]
    public void InjectedSessionCoverletXmlFallbackResolvesResponseFileFromPropagatedWorkingDirectory(string command)
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var workingDirectory = Path.Combine(runFolderBaseDirectory, "repo");
        var testHostDirectory = Path.Combine(runFolderBaseDirectory, "testhost");
        var resultsDirectory = Path.Combine(workingDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        Directory.CreateDirectory(testHostDirectory);
        var responseFile = Path.Combine(workingDirectory, "coverage.rsp");
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            Directory.SetCurrentDirectory(testHostDirectory);
            var responseFileContents =
                """
                --collect
                XPlat Code Coverage
                --results-directory
                TestResults
                """;
            File.WriteAllText(responseFile, responseFileContents);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, command);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            DotnetCommon.CreateSession().Should().BeNull();
            File.Exists(Path.Combine(Environment.CurrentDirectory, "coverage.rsp")).Should().BeFalse();
            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory).Should().Be(workingDirectory);

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var persistedResults).Should().BeTrue();
            persistedResults.Should().ContainSingle(result => result.Source == CodeCoverageReportSource.CoverletXmlFallback && result.Percentage == 50);

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void PropagatedWorkingDirectoryUsesAbsoluteOriginalPath()
    {
        var workspaceDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-working-directory-{Guid.NewGuid():N}");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        TestSession? session = null;
        try
        {
            Directory.CreateDirectory(workspaceDirectory);
            Directory.SetCurrentDirectory(workspaceDirectory);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: "repo");

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory)
                       .Should()
                       .Be(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "repo")));
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                if (Directory.Exists(workspaceDirectory))
                {
                    Directory.Delete(workspaceDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackRunsThroughNullSessionFinalizer()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            DotnetCommon.CreateSession().Should().BeNull();

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            DotnetCommon.FinalizeSession(null, 0, null);

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackRecordsIpcFailureWhenReportCannotBeProcessedThroughNullSessionFinalizer()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        const string CoverageXml = "<coverage><broken></coverage>";

        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            DotnetCommon.CreateSession().Should().BeNull();

            File.WriteAllText(coverageFile, CoverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            var finalize = () => DotnetCommon.FinalizeSession(null, 0, null);
            finalize.Should().NotThrow();

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.CoverletXmlFallback));
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackDoesNotRecordIpcFailureWhenCommandDoesNotUseCoverletCollector()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);

        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            DotnetCommon.CreateSession().Should().BeNull();

            var finalize = () => DotnetCommon.FinalizeSession(null, 0, null);
            finalize.Should().NotThrow();

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out _).Should().BeFalse();
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackDoesNotRunThroughNullSessionFinalizerWithoutBackfillRunFolder()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            DotnetCommon.CreateSession().Should().BeNull();

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            DotnetCommon.FinalizeSession(null, 0, null);

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out _).Should().BeFalse();
            File.ReadAllText(coverageFile).Should().Be(coverageXml);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackIsIdempotentForSameAttachment()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();

            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();
            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var persistedResults).Should().BeTrue();
            var persistedResult = persistedResults.Should().ContainSingle().Subject;
            persistedResult.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
            persistedResult.Percentage.Should().Be(50);

            session.DrainIpcMessages(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50), waitForFirstMessage: true);
            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void InjectedSessionCoverletXmlFallbackPreservesBackfilledWhenCoverletResultIsPersisted()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);
        var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
        var coverageXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" lines-covered="1" lines-valid="2">
              <packages>
                <package line-rate="0.5" lines-covered="1" lines-valid="2">
                  <classes>
                    <class filename="Samples/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousCommand = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousBackfillPath = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath);
        TestSession? session = null;
        try
        {
            const string workingDirectory = @"C:\evp_demo\working_directory";
            Directory.SetCurrentDirectory(runFolderBaseDirectory);
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, $"vstest sample.dll --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);

            session = CreateSession(propagateEnvironmentVariables: true, workingDirectory: workingDirectory);
            session.EnableIpcServer().Should().BeTrue();
            File.WriteAllText(coverageFile, coverageXml);
            File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 80,
                backfilled: true,
                executableLines: 5,
                coveredLines: 4,
                diagnostic: "coverlet");

            DotnetCommon.TryProcessInjectedSessionCoverletCollectorXmlReports().Should().BeTrue();

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var persistedResults).Should().BeTrue();
            persistedResults.Should().Contain(result => result.Source == CodeCoverageReportSource.Coverlet);
            persistedResults.Should().Contain(result => result.Source == CodeCoverageReportSource.CoverletXmlFallback);

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(50);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand, previousCommand);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, previousBackfillPath);
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalizeSessionSuppressesLocalCoverletResultWhenBackfillIsUnavailableAfterActualItrSkip(bool writeBrokenCoverageReport)
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        var coverageDirectory = Path.Combine(resultsDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coverageDirectory);

        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        TestSession? session = null;
        try
        {
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            session = CreateSession(
                workingDirectory: runFolderBaseDirectory,
                command: $"dotnet test --collect \"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 0,
                backfilled: false,
                executableLines: 1,
                coveredLines: 0,
                diagnostic: "coverlet");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 100,
                backfilled: true,
                executableLines: 1,
                coveredLines: 1,
                diagnostic: "stale-coverlet-xml");

            if (writeBrokenCoverageReport)
            {
                var coverageFile = Path.Combine(coverageDirectory, "coverage.cobertura.xml");
                File.WriteAllText(coverageFile, "<coverage><broken></coverage>");
                File.SetLastWriteTimeUtc(coverageFile, DateTime.UtcNow);
            }

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task FinalizeSessionDoesNotPublishRawCoverageWhenBackfillIsUnavailableAfterActualItrSkip()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);

        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var collector = new CiVisibilityMetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        IMetricsTelemetryCollector? previousMetrics = null;
        TestSession? session = null;
        try
        {
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            session = CreateSession(
                workingDirectory: runFolderBaseDirectory,
                command: $"dotnet test --collect \"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 0,
                backfilled: false,
                executableLines: 1,
                coveredLines: 0,
                diagnostic: "coverlet");
            CoverageBackfillDataStore.RecordCoverageIpcFailure(session.Tags.SessionId, nameof(CodeCoverageReportSource.Coverlet));

            previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);
            DotnetCommon.FinalizeSession(session, 0, null);
            collector.AggregateMetrics();

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
            collector.GetMetrics().Metrics.Should().Contain(metric => metric.Metric == "code_coverage.errors" && metric.Namespace == "civisibility");
        }
        finally
        {
            if (previousMetrics is not null)
            {
                TelemetryFactory.SetMetricsForTesting(previousMetrics);
            }

            await collector.DisposeAsync();
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void TryProcessCoverageXmlDoesNotBackfillWhenNoTestWasSkippedByItr()
    {
        var coveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-no-skip-{Guid.NewGuid():N}.xml");
        var coverageXml =
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.IsCoverageBackfillSafe()).Returns(true);
        skippableFeature.Setup(x => x.GetCoverageBackfillData()).Returns(BackfillForLineMap(new Dictionary<string, int> { ["src/Calculator.cs"] = 1 }));
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test --collect \"XPlat Code Coverage\"");
        try
        {
            File.WriteAllText(coveragePath, coverageXml);

            DotnetCommon.TryProcessCoverageXml(coveragePath, session, out var result).Should().BeTrue();

            result.Percentage.Should().Be(0);
            result.Backfilled.Should().BeFalse();
            File.ReadAllText(coveragePath).Should().Contain("""<line number="1" hits="0" />""");
        }
        finally
        {
            CloseAndReset(session);
            File.Delete(coveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionKeepsValidatedCoverletXmlFallbackWhenParentFallbackIsUnavailableAfterActualItrSkip()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-xml-{Guid.NewGuid():N}");
        var resultsDirectory = Path.Combine(runFolderBaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);

        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        TestSession? session = null;
        try
        {
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            session = CreateSession(
                workingDirectory: runFolderBaseDirectory,
                command: $"dotnet test --collect \"XPlat Code Coverage\" --results-directory \"{resultsDirectory}\"");
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 0,
                backfilled: false,
                executableLines: 1,
                coveredLines: 0,
                diagnostic: "coverlet");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 100,
                backfilled: true,
                executableLines: 1,
                coveredLines: 1,
                diagnostic: "validated-coverlet-xml",
                backfillValidated: true);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void FinalizeSessionSuppressesUnvalidatedBackfilledMicrosoftCoverageAfterActualItrSkip()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-microsoft-xml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runFolderBaseDirectory);

        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        TestSession? session = null;
        try
        {
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            session = CreateSession(workingDirectory: runFolderBaseDirectory);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            session.RecordCodeCoverage(
                CodeCoverageReportSource.DatadogInternal,
                25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "internal");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 100,
                backfilled: true,
                executableLines: 1,
                coveredLines: 1,
                diagnostic: "unvalidated-microsoft");

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(25);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                CoverageBackfillCapability.ResetCommandLineCacheForTests();

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void FinalizeSessionKeepsValidatedMicrosoftCoverageAfterActualItrSkip()
    {
        var runFolderBaseDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-microsoft-xml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runFolderBaseDirectory);

        var previousRunFolder = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder);
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        TestSession? session = null;
        try
        {
            var runFolder = Path.Combine(runFolderBaseDirectory, ".dd", TestOptimization.Instance.RunId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            session = CreateSession(workingDirectory: runFolderBaseDirectory);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            session.RecordCodeCoverage(
                CodeCoverageReportSource.DatadogInternal,
                25,
                backfilled: false,
                executableLines: 4,
                coveredLines: 1,
                diagnostic: "internal");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 100,
                backfilled: true,
                executableLines: 1,
                coveredLines: 1,
                diagnostic: "validated-microsoft",
                backfillValidated: true);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    CloseAndReset(session);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, previousRunFolder);
                Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
                CoverageBackfillCapability.ResetCommandLineCacheForTests();

                if (Directory.Exists(runFolderBaseDirectory))
                {
                    Directory.Delete(runFolderBaseDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void ClosePublishesPersistedCoverageIpcResultWhenIpcMessageIsNotDelivered()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 80,
                backfilled: true,
                executableLines: 5,
                coveredLines: 4,
                diagnostic: "persisted");

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(80);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseIgnoresPersistedCoverageIpcResultForDifferentSession()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId + 1,
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                percentage: 80,
                backfilled: true,
                executableLines: 5,
                coveredLines: 4,
                diagnostic: "other-session");

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseRejectsPersistedSameSourceCoverageAfterDeduplicatingDeliveredIpcResult()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            var deliveredResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 10,
                backfilled: true,
                executableLines: 10,
                coveredLines: 1,
                diagnostic: "delivered");
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 90,
                backfilled: true,
                executableLines: 10,
                coveredLines: 9,
                diagnostic: "persisted-only");

            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage(
                    CodeCoverageReportSource.Coverlet,
                    10,
                    backfilled: true,
                    executableLines: 10,
                    coveredLines: 1,
                    diagnostic: "delivered",
                    resultId: deliveredResultId));

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(nameof(CodeCoverageReportSource.Coverlet))]
    [InlineData(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage))]
    public void CloseDoesNotPublishDeliveredCoverageWhenPersistedCoverageIpcSnapshotIsIncomplete(string sourceName)
    {
        var source = ParseCodeCoverageReportSource(sourceName);
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            var deliveredResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                source,
                percentage: 80,
                backfilled: true,
                executableLines: 10,
                coveredLines: 8,
                diagnostic: "delivered");
            var resultFolder = Path.Combine(runFolder, "coverage-backfill-ipc-results", $"session-{session.Tags.SessionId}");
            File.WriteAllText(Path.Combine(resultFolder, $"{source}-incomplete.json.tmp"), "{}");

            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage(
                    source,
                    80,
                    backfilled: true,
                    executableLines: 10,
                    coveredLines: 8,
                    diagnostic: "delivered",
                    resultId: deliveredResultId));

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(nameof(CodeCoverageReportSource.Coverlet), "dotnet test --collect \"XPlat Code Coverage\"")]
    [InlineData(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage), "dotnet test --collect \"Code Coverage\"")]
    public void CloseDoesNotPublishDeliveredCoverageWhenExpectedPersistedCoverageIpcFolderIsEmpty(string sourceName, string command)
    {
        var source = ParseCodeCoverageReportSource(sourceName);
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, command);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        try
        {
            var resultFolder = Path.Combine(runFolder, "coverage-backfill-ipc-results", $"session-{session.Tags.SessionId}");
            Directory.CreateDirectory(resultFolder);

            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage(
                    source,
                    80,
                    backfilled: true,
                    executableLines: 10,
                    coveredLines: 8,
                    diagnostic: "delivered",
                    resultId: "delivered"));

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void ClosePublishesExternalXmlWhenExpectedPersistedCoverageIpcFolderIsEmpty()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        try
        {
            var resultFolder = Path.Combine(runFolder, "coverage-backfill-ipc-results", $"session-{session.Tags.SessionId}");
            Directory.CreateDirectory(resultFolder);
            session.RecordCodeCoverage(CodeCoverageReportSource.ExternalXml, 70, backfilled: false, executableLines: 10, coveredLines: 7);

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(70);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void FinalizeSessionDoesNotPublishExistingExternalXmlWhenCurrentCoverageCommandWritesAnotherPath()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        var otherCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-other-{Guid.NewGuid():N}.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet-coverage collect --output-format cobertura --output \"{otherCoveragePath}\"");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession(command: $"dotnet-coverage collect --output-format cobertura --output \"{otherCoveragePath}\"");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(existingCoveragePath);
            File.Delete(otherCoveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionDoesNotPublishStaleExistingExternalXmlAfterActualItrSkip()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession(command: "dotnet test");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            var oldTimestamp = session.StartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(existingCoveragePath, oldTimestamp);
            File.SetLastWriteTimeUtc(existingCoveragePath, oldTimestamp);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(existingCoveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionIgnoresGlobalActualSkipWhenCurrentSessionDidNotSkip()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            var oldTimestamp = session.StartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(existingCoveragePath, oldTimestamp);
            File.SetLastWriteTimeUtc(existingCoveragePath, oldTimestamp);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, "1");

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            File.Delete(existingCoveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionSuppressesStaleExternalXmlWhenLegacyPersistedActualSkipExistsWithSkippableFeature()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            var oldTimestamp = session.StartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(existingCoveragePath, oldTimestamp);
            File.SetLastWriteTimeUtc(existingCoveragePath, oldTimestamp);
            CoverageBackfillDataStore.RecordActualItrSkip();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            File.Delete(existingCoveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionSuppressesStaleExternalXmlWhenPersistedActualSkipExistsWithSkippableFeature()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            var oldTimestamp = session.StartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(existingCoveragePath, oldTimestamp);
            File.SetLastWriteTimeUtc(existingCoveragePath, oldTimestamp);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(existingCoveragePath);
        }
    }

    [Fact]
    public void FinalizeSessionIgnoresPersistedActualSkipFromDifferentSessionWithSkippableFeature()
    {
        var existingCoveragePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-existing-{Guid.NewGuid():N}.xml");
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-actual-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath, existingCoveragePath);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, workingDirectory: workingDirectory, command: "dotnet test");
        try
        {
            var existingCoverageXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(existingCoveragePath, existingCoverageXml);
            var oldTimestamp = session.StartTime.UtcDateTime.AddHours(-1);
            File.SetCreationTimeUtc(existingCoveragePath, oldTimestamp);
            File.SetLastWriteTimeUtc(existingCoveragePath, oldTimestamp);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId + 1);

            DotnetCommon.FinalizeSession(session, 0, null);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(100);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            File.Delete(existingCoveragePath);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseSetsTestsSkippedWhenPersistedActualSkipExistsWithSkippableFeature()
    {
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-actual-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);

            session.Close(TestStatus.Pass);

            session.Tags.TestsSkipped.Should().Be("true");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
            CloseAndReset(session);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseSetsTestsSkippedWhenLegacyPersistedActualSkipExistsWithSkippableFeature()
    {
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-actual-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            CoverageBackfillDataStore.RecordActualItrSkip();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);

            session.Close(TestStatus.Pass);

            session.Tags.TestsSkipped.Should().Be("true");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
            CloseAndReset(session);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseDoesNotSetTestsSkippedFromPersistedActualSkipForDifferentSessionWithSkippableFeature()
    {
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-actual-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId + 1);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);

            session.Close(TestStatus.Pass);

            session.Tags.TestsSkipped.Should().Be("false");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
            CloseAndReset(session);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseDoesNotSetTestsSkippedFromGlobalActualSkipWithoutPersistedMarker()
    {
        var previousActualSkip = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip);
        var previousWorkingDirectory = Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-actual-skip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, workingDirectory);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, "1");
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        skippableFeature.Setup(x => x.Enabled).Returns(true);
        skippableFeature.Setup(x => x.HasSkippedTestsByItr(It.IsAny<ulong>())).Returns(false);
        var session = CreateSessionWithSkippableFeature(skippableFeature.Object, command: "dotnet test");
        try
        {
            session.Close(TestStatus.Pass);

            session.Tags.TestsSkipped.Should().Be("false");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, previousActualSkip);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory, previousWorkingDirectory);
            CloseAndReset(session);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ClosePublishesExternalXmlWhenPersistedCoverageIpcSnapshotIsIncomplete()
    {
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        try
        {
            var resultFolder = Path.Combine(runFolder, "coverage-backfill-ipc-results", $"session-{session.Tags.SessionId}");
            Directory.CreateDirectory(resultFolder);
            File.WriteAllText(Path.Combine(resultFolder, "Coverlet-incomplete.json.tmp"), "{}");
            session.RecordCodeCoverage(CodeCoverageReportSource.ExternalXml, 70, backfilled: false, executableLines: 10, coveredLines: 7);

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(70);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(nameof(CodeCoverageReportSource.Coverlet))]
    [InlineData(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage))]
    public void CloseDoesNotPublishPersistedCoverageWhenPersistedCoverageIpcSnapshotIsIncomplete(string sourceName)
    {
        var source = ParseCodeCoverageReportSource(sourceName);
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        var session = CreateSession();
        try
        {
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                source,
                percentage: 80,
                backfilled: true,
                executableLines: 10,
                coveredLines: 8,
                diagnostic: "persisted");
            var resultFolder = Path.Combine(runFolder, "coverage-backfill-ipc-results", $"session-{session.Tags.SessionId}");
            File.WriteAllText(Path.Combine(resultFolder, $"{source}-incomplete.json.tmp"), "{}");

            session.Close(TestStatus.Pass);

            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().BeNull();
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void CloseWaitsForCoverageIpcBeforePublishingCoverage()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect \"dotnet test /p:CollectCoverage=true\"");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        IpcClient? client = null;
        Thread? closeThread = null;
        Exception? closeException = null;
        try
        {
            session.EnableIpcServer().Should().BeTrue();
            client = new IpcClient($"session_{session.Tags.SessionId}");
            closeThread = new Thread(
                () =>
                {
                    try
                    {
                        session.Close(TestStatus.Pass);
                    }
                    catch (Exception ex)
                    {
                        closeException = ex;
                    }
                });
            closeThread.Start();

            var initialCoverageIpcMessageCount = GetPrivateField(session, "_coverageIpcMessageCount");

            SpinWait.SpinUntil(() => GetPrivateField(session, "_closing") == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
            SendSessionTagMessage(client, "_dd.test.coverage_non_coverage_ipc", "received");
            closeThread.IsAlive.Should().BeTrue("the close path should wait for coverage IPC, not just any session IPC message");
            SendCoverageMessage(client, 80, backfilled: true, executableLines: 5, coveredLines: 4);
            SpinWait.SpinUntil(() => GetPrivateField(session, "_coverageIpcMessageCount") > initialCoverageIpcMessageCount, TimeSpan.FromSeconds(5)).Should().BeTrue();

            closeThread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
            closeException.Should().BeNull();
            session.Tags.GetTag("_dd.test.coverage_non_coverage_ipc").Should().Be("received");
            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(80);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            client?.Dispose();
            closeThread?.Join(TimeSpan.FromSeconds(5));
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
        }
    }

    [Fact]
    public void CloseWaitsForCoverageIpcWhenLowerPriorityCoverageAlreadyExists()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        IpcClient? client = null;
        Thread? closeThread = null;
        Exception? closeException = null;
        try
        {
            session.RecordCodeCoverage(CodeCoverageReportSource.DatadogInternal, 10, backfilled: false, executableLines: 10, coveredLines: 1);
            session.EnableIpcServer().Should().BeTrue();
            client = new IpcClient($"session_{session.Tags.SessionId}");
            closeThread = new Thread(
                () =>
                {
                    try
                    {
                        session.Close(TestStatus.Pass);
                    }
                    catch (Exception ex)
                    {
                        closeException = ex;
                    }
                });
            closeThread.Start();

            SpinWait.SpinUntil(() => GetPrivateField(session, "_closing") == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
            SendCoverageMessage(client, 80, backfilled: true, executableLines: 5, coveredLines: 4);

            closeThread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
            closeException.Should().BeNull();
            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(80);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            client?.Dispose();
            closeThread?.Join(TimeSpan.FromSeconds(5));
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
        }
    }

    [Fact]
    public void CloseWaitsForPersistedHigherPriorityCoverageAfterCoverageIpcAlreadyArrived()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var runFolder = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-ipc-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, runFolder);
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        var session = CreateSession();
        Thread? closeThread = null;
        Exception? closeException = null;
        try
        {
            InvokeIpcMessageReceived(
                session,
                new SessionCodeCoverageMessage(
                    CodeCoverageReportSource.Coverlet,
                    10,
                    backfilled: false,
                    executableLines: 10,
                    coveredLines: 1,
                    diagnostic: "ipc-before-close"));
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.Coverlet,
                percentage: 10,
                backfilled: false,
                executableLines: 10,
                coveredLines: 1,
                diagnostic: "persisted-before-close");

            closeThread = new Thread(
                () =>
                {
                    try
                    {
                        session.Close(TestStatus.Pass);
                    }
                    catch (Exception ex)
                    {
                        closeException = ex;
                    }
                });
            closeThread.Start();

            SpinWait.SpinUntil(() => GetPrivateField(session, "_closing") == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
            CoverageBackfillDataStore.RecordCoverageIpcResult(
                session.Tags.SessionId,
                CodeCoverageReportSource.CoverletXmlFallback,
                percentage: 90,
                backfilled: true,
                executableLines: 10,
                coveredLines: 9,
                diagnostic: "persisted-fallback");

            closeThread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
            closeException.Should().BeNull();
            session.Tags.GetMetric(CodeCoverageTags.PercentageOfTotalLines).Should().Be(90);
            session.Tags.GetTag(CodeCoverageTags.Backfilled).Should().Be("true");
        }
        finally
        {
            closeThread?.Join(TimeSpan.FromSeconds(5));
            CloseAndReset(session);
            CoverageBackfillCapability.ResetCommandLineCacheForTests();
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void LateIpcCoverageMessagesSentDuringSessionCloseDoNotThrow()
    {
        var session = CreateSession();
        IpcClient? client = null;
        Thread? senderThread = null;
        Exception? senderException = null;
        var stopSending = new ManualResetEventSlim();
        var firstSendSucceeded = new ManualResetEventSlim();

        try
        {
            session.EnableIpcServer().Should().BeTrue();
            client = new IpcClient($"session_{session.Tags.SessionId}");
            var message = CreateCoverageMessage(80, backfilled: true, executableLines: 5, coveredLines: 4);

            senderThread = new Thread(
                () =>
                {
                    try
                    {
                        while (!stopSending.IsSet)
                        {
                            if (client.TrySendMessage(message))
                            {
                                firstSendSucceeded.Set();
                            }

                            Thread.Sleep(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        senderException = ex;
                    }
                });

            senderThread.Start();
            firstSendSucceeded.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            // Messages sent strictly after server disposal may be ignored; sends racing with Close must remain harmless.
            session.Close(TestStatus.Pass);

            stopSending.Set();
            senderThread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
            Action sendAfterServerDispose = () => client.TrySendMessage(message);

            sendAfterServerDispose.Should().NotThrow();
            senderException.Should().BeNull();
        }
        finally
        {
            stopSending.Set();
            senderThread?.Join(TimeSpan.FromSeconds(5));
            client?.Dispose();
            CloseAndReset(session);
            stopSending.Dispose();
            firstSendSucceeded.Dispose();
        }
    }

    [Fact]
    public void CloseRestoresMissingBackfillRunFolderAfterEnvironmentPropagation()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);
        TestOptimization.Instance.Reset();
        var session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null, propagateEnvironmentVariables: true);
        try
        {
            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder).Should().NotBeNullOrEmpty();

            session.Close(TestStatus.Pass);

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void CloseRestoresActualSkipEnvironmentSetDuringSession()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
        TestOptimization.Instance.Reset();
        var session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null, propagateEnvironmentVariables: true);
        try
        {
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip).Should().Be("1");

            session.Close(TestStatus.Pass);

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    [Fact]
    public void CloseRestoresBackfillPathEnvironmentSetDuringSession()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);
        TestOptimization.Instance.Reset();
        var session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null, propagateEnvironmentVariables: true);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, "later-backfill-path.json");

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath).Should().Be("later-backfill-path.json");

            session.Close(TestStatus.Pass);

            Environment.GetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath).Should().BeNull();
        }
        finally
        {
            CloseAndReset(session);
        }
    }

    private static TestSession CreateSession(bool propagateEnvironmentVariables = false, string? workingDirectory = null, string command = "dotnet test")
    {
        TestOptimization.Instance.Reset();
        return TestSession.GetOrCreate(command, workingDirectory: workingDirectory, framework: null, startDate: null, propagateEnvironmentVariables);
    }

    private static TestSession CreateSessionWithSkippableFeature(ITestOptimizationSkippableFeature skippableFeature, bool propagateEnvironmentVariables = false, string? workingDirectory = null, string command = "dotnet test")
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<TestSessionCoverageTests>());
        testOptimization.Setup(x => x.RunId).Returns(Guid.NewGuid().ToString("N"));
        testOptimization.Setup(x => x.Settings).Returns(TestOptimizationSettings.FromDefaultSources());
        testOptimization.Setup(x => x.CIValues).Returns(CIEnvironmentValues.Instance);
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature);
        testOptimization.Setup(x => x.Reset()).Callback(() => TestOptimization.Instance = new TestOptimization());
        TestOptimization.Instance = testOptimization.Object;
        return TestSession.GetOrCreate(command, workingDirectory: workingDirectory, framework: null, startDate: null, propagateEnvironmentVariables);
    }

    private static CodeCoverageReportSource ParseCodeCoverageReportSource(string sourceName)
        => (CodeCoverageReportSource)Enum.Parse(typeof(CodeCoverageReportSource), sourceName);

    private static string? NormalizeDirectorySeparators(string? path)
        => path?.Replace('\\', '/');

    private static void CloseAndReset(TestSession session)
    {
        session.Close(TestStatus.Pass);
        TestOptimization.Instance.Close();
        TestOptimization.Instance.Reset();
    }

    private static void InvokeIpcMessageReceived(TestSession session, object message)
    {
        typeof(TestSession)
           .GetMethod("OnIpcMessageReceived", BindingFlags.Instance | BindingFlags.NonPublic)!
           .Invoke(session, [message]);
    }

    private static CoverageBackfillData BackfillForLineMap(Dictionary<string, int> lineByPath)
    {
        var coverage = new Dictionary<string, string>();
        foreach (var item in lineByPath)
        {
            var bitmap = new byte[(item.Value + 7) / 8];
            var index = item.Value - 1;
            bitmap[index >> 3] = (byte)(128 >> (index & 7));
            coverage[item.Key] = Convert.ToBase64String(bitmap);
        }

        return CoverageBackfillData.FromBackendCoverage(coverage);
    }

    private static void SetPrivateField(TestSession session, string fieldName, int value)
    {
        typeof(TestSession)
           .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
           .SetValue(session, value);
    }

    private static int GetPrivateField(TestSession session, string fieldName)
    {
        return (int)typeof(TestSession)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(session)!;
    }

    private static void SendCoverageMessage(IpcClient client, double value, bool backfilled, double executableLines, double coveredLines)
    {
        var message = CreateCoverageMessage(value, backfilled, executableLines, coveredLines);
        SendIpcMessage(client, message);
    }

    private static void SendSessionTagMessage(IpcClient client, string name, string value)
    {
        SendIpcMessage(client, new SetSessionTagMessage(name, value));
    }

    private static void SendIpcMessage(IpcClient client, object message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (client.TrySendMessage(message))
            {
                return;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Timed out sending message through IPC.");
    }

    private static SessionCodeCoverageMessage CreateCoverageMessage(double value, bool backfilled, double executableLines, double coveredLines)
        => new(
            CodeCoverageReportSource.Coverlet,
            value,
            backfilled,
            executableLines,
            coveredLines);
}
