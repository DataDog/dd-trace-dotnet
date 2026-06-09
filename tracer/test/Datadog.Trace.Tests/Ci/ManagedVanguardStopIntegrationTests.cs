// <copyright file="ManagedVanguardStopIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.CIVisibility.TestOptimizationRunId,
    ConfigurationKeys.CIVisibility.TestSessionCommand,
    ConfigurationKeys.CIVisibility.TestSessionWorkingDirectory,
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
public class ManagedVanguardStopIntegrationTests
{
    [Fact]
    public void BackfillsMicrosoftLineXmlAndPersistsScopedCoverageResult()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string CoverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(reportPath, CoverageXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(reportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillsMicrosoftLineXmlFromSessionScopedActualSkipMarker()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string CoverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(reportPath, CoverageXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(reportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void DoesNotBackfillMicrosoftLineXmlFromAnotherSessionActualSkipMarker()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string CoverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(reportPath, CoverageXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId + 1);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(reportPath).Should().Contain("""<line number="1" hits="0" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(0);
            result.Backfilled.Should().BeFalse();
            result.BackfillValidated.Should().BeFalse();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void RepeatedMicrosoftStopUsesStableCoverageResultId()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string CoverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(reportPath, CoverageXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));
            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.ResultId.Should().StartWith("microsoft-xml-");
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void RecordsIpcFailureAndDoesNotPublishCoverageWhenMicrosoftStopThrows()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;
        const string CoverageXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            File.WriteAllText(reportPath, CoverageXml);
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, new InvalidOperationException("Stop failed."), default(CallTargetState));

            File.ReadAllText(reportPath).Should().Be(CoverageXml);
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void DeduplicatesEquivalentMicrosoftLineXmlPathsBeforeBackfill()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string CoverageXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(reportPath, CoverageXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var relativeReportPath = Path.GetFileName(reportPath);
            var proxy = new ManagedVanguardProxy([reportPath, relativeReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(reportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            results.Should().ContainSingle();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void RecordsIpcFailureWhenMicrosoftProvidesNoXmlReports()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void RecordsIpcFailureWhenMicrosoftXmlCannotBeProcessedWithoutBackfill()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(reportPath, "<report>");
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void RecordsIpcFailureWhenOneOfMultipleMicrosoftXmlReportsCannotBeProcessedWithoutBackfill()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;
        const string ValidReportXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(firstReportPath, ValidReportXml);
            File.WriteAllText(secondReportPath, "<report>");
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillsMicrosoftLineXmlWhenBackendCoverageIsSplitAcrossReportsAndPublishesSingleAggregatedResult()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string FirstReportXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            const string SecondReportXml =
                """
                <report>
                  <file path="src/Other.cs">
                    <line number="1" hits="0" />
                  </file>
                </report>
                """;
            File.WriteAllText(firstReportPath, FirstReportXml);
            File.WriteAllText(secondReportPath, SecondReportXml);
            CoverageBackfillDataStore.Persist(
                TestOptimization.Instance,
                CoverageBackfillData.FromBackendCoverage(
                    new Dictionary<string, string>
                    {
                        ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                        ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
                    }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(firstReportPath).Should().Contain("""<line number="1" hits="1" />""");
            File.ReadAllText(secondReportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void MergesOverlappingMicrosoftLineXmlReportsByLineIdentity()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string FirstReportXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="1" />
                    <line number="2" hits="0" />
                  </file>
                </report>
                """;
            const string SecondReportXml =
                """
                <report>
                  <file path="src/Calculator.cs">
                    <line number="1" hits="0" />
                    <line number="2" hits="1" />
                  </file>
                </report>
                """;
            File.WriteAllText(firstReportPath, FirstReportXml);
            File.WriteAllText(secondReportPath, SecondReportXml);
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeFalse();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void FailsClosedWhenMultipleMicrosoftXmlReportsCannotBeMergedByLineIdentity()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            const string AggregateOnlyReportXml =
                """
                <results>
                  <modules>
                    <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1" />
                  </modules>
                </results>
                """;
            File.WriteAllText(firstReportPath, AggregateOnlyReportXml);
            File.WriteAllText(secondReportPath, AggregateOnlyReportXml);
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out _).Should().BeFalse();
            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out var reason).Should().BeTrue();
            reason.Should().Contain(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillRequiredRestoresReportsWhenCrossReportPathMatchIsUnsafe()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;
        const string FirstReportXml =
            """
            <report>
              <file path="repo-a/src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;
        const string SecondReportXml =
            """
            <report>
              <file path="repo-b/src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(firstReportPath, FirstReportXml);
            File.WriteAllText(secondReportPath, SecondReportXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(firstReportPath).Should().Be(FirstReportXml);
            File.ReadAllText(secondReportPath).Should().Be(SecondReportXml);
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(0);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(0);
            result.Backfilled.Should().BeFalse();
            result.BackfillValidated.Should().BeTrue();
            CoverageBackfillDataStore.TryReadCoverageIpcFailure(session.Tags.SessionId, out _).Should().BeFalse();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillRequiredMergesReportsWhenMultipleReportsRepresentSameBackendLine()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;
        const string FirstReportXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;
        const string SecondReportXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(firstReportPath, FirstReportXml);
            File.WriteAllText(secondReportPath, SecondReportXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(firstReportPath).Should().Contain("""<line number="1" hits="1" />""");
            File.ReadAllText(secondReportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillRequiredMergesReportsByValidatedBackendIdentityAcrossReportDirectories()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        var firstReportDirectory = Path.Combine(workspacePath, "first");
        var secondReportDirectory = Path.Combine(workspacePath, "second");
        Directory.CreateDirectory(firstReportDirectory);
        Directory.CreateDirectory(secondReportDirectory);
        var firstReportPath = Path.Combine(firstReportDirectory, "coverage.xml");
        var secondReportPath = Path.Combine(secondReportDirectory, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        TestSession? session = null;
        const string FirstReportXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
                <line number="2" hits="0" />
              </file>
            </report>
            """;
        const string SecondReportXml =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
                <line number="2" hits="1" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(firstReportPath, FirstReportXml);
            File.WriteAllText(secondReportPath, SecondReportXml);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: workspacePath, framework: null, startDate: null, propagateEnvironmentVariables: true);
            CoverageBackfillDataStore.RecordActualItrSkip(session.Tags.SessionId);
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(firstReportPath).Should().Contain("""<line number="1" hits="1" />""");
            File.ReadAllText(secondReportPath).Should().Contain("""<line number="1" hits="1" />""");
            CoverageBackfillDataStore.TryReadCoverageIpcResults(session.Tags.SessionId, out var results).Should().BeTrue();
            var result = results.Should().ContainSingle().Subject;
            result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.BackfillValidated.Should().BeTrue();
        }
        finally
        {
            try
            {
                session?.Close(TestStatus.Pass);
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillRequiredRestoresEarlierReportsWhenLaterReportFails()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var firstReportPath = Path.Combine(workspacePath, "first.xml");
        var secondReportPath = Path.Combine(workspacePath, "second.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        const string OriginalFirstReport =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(firstReportPath, OriginalFirstReport);
            File.WriteAllText(secondReportPath, "<report>");
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            CoverageBackfillDataStore.RecordActualItrSkip();
            var proxy = new ManagedVanguardProxy([firstReportPath, secondReportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(firstReportPath).Should().Be(OriginalFirstReport);
        }
        finally
        {
            try
            {
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
    }

    [Fact]
    public void BackfillRequiredDoesNotMutateReportWhenParentSessionContextIsMissing()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-vanguard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        var reportPath = Path.Combine(workspacePath, "coverage.xml");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        const string OriginalReport =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;

        try
        {
            Directory.SetCurrentDirectory(workspacePath);
            TestOptimization.Instance.Reset();
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, Path.Combine(Environment.CurrentDirectory, ".dd", TestOptimization.Instance.RunId));
            File.WriteAllText(reportPath, OriginalReport);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]) }));
            CoverageBackfillDataStore.RecordActualItrSkip();
            var proxy = new ManagedVanguardProxy([reportPath]);

            ManagedVanguardStopIntegration.OnMethodEnd(proxy, exception: null, default(CallTargetState));

            File.ReadAllText(reportPath).Should().Be(OriginalReport);
        }
        finally
        {
            try
            {
                TestOptimization.Instance.Close();
                TestOptimization.Instance.Reset();
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                DeleteWorkspacePath(workspacePath);
            }
        }
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
            if (string.Equals(Path.GetFullPath(workspaceRunFolder), Path.GetFullPath(currentDirectoryMirror), StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(currentDirectoryMirror))
            {
                continue;
            }

            Directory.Delete(currentDirectoryMirror, recursive: true);
        }
    }

    private sealed class ManagedVanguardProxy : ManagedVanguardStopIntegration.IManagedVanguardProxy
    {
        private readonly IList<string> _files;

        public ManagedVanguardProxy(IList<string> files)
        {
            _files = files;
        }

        public object Instance => this;

        public Type Type => GetType();

        public ref TReturn? GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return nameof(ManagedVanguardProxy);
        }

        public IList<string>? GetOutputCoverageFiles()
        {
            return _files;
        }
    }
}
