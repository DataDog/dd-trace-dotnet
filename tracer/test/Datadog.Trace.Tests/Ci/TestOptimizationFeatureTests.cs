// <copyright file="TestOptimizationFeatureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using MsTestIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2.MsTestIntegration;
using MsTestMethod = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2.ITestMethod;
using NUnitIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.NUnitIntegration;
using NUnitMethodInfo = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IMethodInfo;
using NUnitPropertyBag = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IPropertyBag;
using NUnitRunState = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.RunState;
using NUnitTest = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITest;
using NUnitTestResult = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITestResult;
using NUnitTypeInfo = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITypeInfo;
using NUnitWorkItem = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IWorkItem;
using NUnitWorkItemPerformWorkIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.NUnitWorkItemPerformWorkIntegration;
using XUnitV3Context = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestMethodRunnerBaseContextV3;
using XUnitV3RunSummary = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.RunSummaryUnsafeStruct;
using XUnitV3RunTestCaseIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.XUnitTestMethodRunnerBaseRunTestCaseV3Integration;
using XUnitV3TestCase = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestCaseV3;
using XUnitV3TestClass = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestClassV3;
using XUnitV3TestMethod = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestMethodV3;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.GlobalTags,
    ConfigurationKeys.CIVisibility.TestSessionCommand,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillCommand,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillPath,
    ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder)]
public class TestOptimizationFeatureTests : SettingsTestsBase
{
    [Fact]
    public void InvalidKnownTestsResponseDisablesKnownTestsAndEarlyFlakeDetection()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(knownTestsResponse: default);

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        knownTestsFeature.Enabled.Should().BeFalse();
        settings.KnownTestsEnabled.Should().BeFalse();
        settings.EarlyFlakeDetectionEnabled.Should().BeFalse();
        earlyFlakeDetectionFeature.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ExplicitEmptyKnownTestsPayloadKeepsKnownTestsAndEarlyFlakeDetectionEnabled()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(
            new TestOptimizationClient.KnownTestsResponse(new TestOptimizationClient.KnownTestsResponse.KnownTestsModules()));

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        knownTestsFeature.Enabled.Should().BeTrue();
        settings.KnownTestsEnabled.Should().BeTrue();
        settings.EarlyFlakeDetectionEnabled.Should().BeTrue();
        earlyFlakeDetectionFeature.Enabled.Should().BeTrue();
    }

    [Fact]
    public void RemoteEnabledSkippingWithCoverageUsesScopedSkippableRequests()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
        var settings = CreateSettings();
        var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
        var client = new TestOptimizationClientStub();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());

        var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

        skippableFeature.Enabled.Should().BeTrue();
        settings.TestsSkippingEnabled.Should().BeTrue();
        skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");
        client.SkippableRequestScopes.Should().ContainSingle();
        client.SkippableRequestScopes[0].TestBundle.Should().Be("Samples.XUnitTests");
        client.SkippableRequestScopes[0].HasFingerprint.Should().BeTrue();
    }

    [Theory]
    [InlineData("queue")]
    [InlineData(TestTags.Bundle)]
    [InlineData(TestTags.Module)]
    public void ScopedSkippableFingerprintIncludesModuleNamedCustomConfigurations(string customConfigurationKey)
    {
        var workspacePath = Directory.GetCurrentDirectory();
        Environment.SetEnvironmentVariable(ConfigurationKeys.GlobalTags, $"test.configuration.{customConfigurationKey}:custom-a");
        var firstTestOptimization = CreateTestOptimization(CreateSettings(), workspacePath);
        var firstScope = SkippableTestsRequestScope.Create(firstTestOptimization.Object, "Samples.XUnitTests");

        Environment.SetEnvironmentVariable(ConfigurationKeys.GlobalTags, $"test.configuration.{customConfigurationKey}:custom-b");
        var secondTestOptimization = CreateTestOptimization(CreateSettings(), workspacePath);
        var secondScope = SkippableTestsRequestScope.Create(secondTestOptimization.Object, "Samples.XUnitTests");

        firstScope.TestBundle.Should().Be("Samples.XUnitTests");
        secondScope.TestBundle.Should().Be("Samples.XUnitTests");
        firstScope.Fingerprint.Should().NotBe(secondScope.Fingerprint);
    }

    [Fact]
    public void ScopedSkippableCoverageUsesInjectedTestOptimizationForPersistence()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");

            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");

            client.SkippableRequestScopes.Should().ContainSingle();
            var persistedPath = Path.Combine(workspacePath, ".dd", "injected-run", "coverage-backfill-scopes", $"{client.SkippableRequestScopes[0].Fingerprint}.json");
            File.Exists(persistedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void ScopedSkippableCorrelationIdUsesMatchingModuleScope()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var client = new TestOptimizationClientStub(
                skippableTestsResponseFactory: scope => new TestOptimizationClient.SkippableTestsResponse(
                    correlationId: $"{scope.TestBundle}-correlation-id",
                    tests: [],
                    CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                    isCoverageBackfillSafe: true));
            var testOptimization = CreateTestOptimization(settings, workspacePath);

            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");
            skippableFeature.GetSkippableTestsFromSuiteAndName("Other.Tests.TestSuite", "SimplePassTest", "Other.Tests");

            skippableFeature.GetCorrelationId("Samples.XUnitTests").Should().Be("Samples.XUnitTests-correlation-id");
            skippableFeature.GetCorrelationId("Other.Tests").Should().Be("Other.Tests-correlation-id");
            skippableFeature.GetCorrelationId("Unknown.Tests").Should().BeNull();
            skippableFeature.GetCorrelationId().Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateAllowsSkipWhenScopedSkippableRequestFaults()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var client = new TestOptimizationClientStub(
                skippableTestsResponseFactory: _ => throw new InvalidOperationException("skippable request failed"));
            var testOptimization = CreateTestOptimization(settings, workspacePath);
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();

            reason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateAllowsSkipWhenBackendCoverageIsMissing()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null, missingLineCodeCoverage: false);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                coverageBackfillData: null,
                isCoverageBackfillSafe: false);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests").Should().ContainSingle();
            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();

            reason.Should().BeEmpty();
            skippableFeature.IsCoverageBackfillSafe().Should().BeFalse();
            skippableFeature.GetCoverageBackfillData().IsPresent.Should().BeFalse();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateFailsClosedWhenCoverageModeIsNotBackfillable()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet-coverage collect \"dotnet test\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null, missingLineCodeCoverage: false);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests").Should().ContainSingle();
            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeFalse();

            reason.Should().Contain("supported external XML report path");
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateDoesNotRecordActualSkipStateBeforeFrameworkCommitsToSkip()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();

            reason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateAllowsScopedResponseWithMultipleBackfillCandidates()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var otherCandidate = new SkippableTest("OtherPassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate, otherCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();
            skippableFeature.CanSkipWithCoverageBackfill(otherCandidate, "Samples.XUnitTests", out var otherReason).Should().BeTrue();

            reason.Should().BeEmpty();
            otherReason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateAllowsSkipWhenCandidateIsMissingFromScopedResponse()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var otherCandidate = new SkippableTest("OtherPassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [otherCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();

            reason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateFailsClosedWhenBackendReportsMissingLineCoverage()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null, missingLineCodeCoverage: true);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeFalse();

            reason.Should().Contain("missing line coverage");
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void XUnitUnskippableForcedRunDoesNotRecordCoverageBackfillState()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var testSuite = typeof(TestOptimizationFeatureTests).FullName;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var reason = string.Empty;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), It.IsAny<string>())).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(It.IsAny<SkippableTest>(), It.IsAny<string>(), out reason)).Returns(true);

        var runnerInstance = new TestRunnerStruct
        {
            TestClass = typeof(TestOptimizationFeatureTests),
            TestMethod = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static),
            TestCase = new XUnitTestCaseStub
            {
                Traits = new Dictionary<string, List<string>>
                {
                    [IntelligentTestRunnerTags.UnskippableTraitName] = ["true"]
                }
            }
        };

        try
        {
            TestOptimization.Instance = testOptimization.Object;

            XUnitIntegration.ShouldSkip(ref runnerInstance, out var isUnskippable, out var isForcedRun).Should().BeFalse();

            isUnskippable.Should().BeTrue();
            isForcedRun.Should().BeTrue();
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitUnskippableForcedRunSurvivesUnsafeCoverageBackfill()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var testSuite = typeof(TestOptimizationFeatureTests).FullName;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var reason = "backend marked the test as missing line coverage";

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), It.IsAny<string>())).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(It.IsAny<SkippableTest>(), It.IsAny<string>(), out reason)).Returns(false);

        var runnerInstance = new TestRunnerStruct
        {
            TestClass = typeof(TestOptimizationFeatureTests),
            TestMethod = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static),
            TestCase = new XUnitTestCaseStub
            {
                Traits = new Dictionary<string, List<string>>
                {
                    [IntelligentTestRunnerTags.UnskippableTraitName] = ["true"]
                }
            }
        };

        try
        {
            TestOptimization.Instance = testOptimization.Object;

            XUnitIntegration.ShouldSkip(ref runnerInstance, out var isUnskippable, out var isForcedRun).Should().BeFalse();

            isUnskippable.Should().BeTrue();
            isForcedRun.Should().BeTrue();
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitShouldSkipUsesCurrentModuleForCoverageBackfill()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var testSuite = typeof(TestOptimizationFeatureTests).FullName;
        var moduleName = "Samples.XUnitTests";
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var reason = string.Empty;
        var previousModule = TestModule.Current;
        TestModule module = null;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), moduleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason)).Returns(true);

        var runnerInstance = new TestRunnerStruct
        {
            TestClass = typeof(TestOptimizationFeatureTests),
            TestMethod = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static),
            TestCase = new XUnitTestCaseStub()
        };

        try
        {
            TestOptimization.Instance = testOptimization.Object;
            module = TestModule.Create(moduleName, CommonTags.TestingFrameworkNameXUnit, "2.0.0");

            XUnitIntegration.ShouldSkip(ref runnerInstance, out var isUnskippable, out var isForcedRun, out var skippableTest).Should().BeTrue();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
            skippableTest.Should().Be(candidate);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), moduleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason), Times.Once);
        }
        finally
        {
            module?.Close();
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitShouldSkipUsesTestClassAssemblyForCoverageBackfillWhenNoModuleIsCurrent()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var testSuite = typeof(TestOptimizationFeatureTests).FullName;
        var moduleName = typeof(TestOptimizationFeatureTests).Assembly.GetName().Name;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var reason = string.Empty;
        var previousModule = TestModule.Current;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), moduleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason)).Returns(true);

        var runnerInstance = new TestRunnerStruct
        {
            TestClass = typeof(TestOptimizationFeatureTests),
            TestMethod = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static),
            TestCase = new XUnitTestCaseStub()
        };

        try
        {
            TestOptimization.Instance = testOptimization.Object;
            TestModule.Current = null;

            XUnitIntegration.ShouldSkip(ref runnerInstance, out var isUnskippable, out var isForcedRun, out var skippableTest).Should().BeTrue();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
            skippableTest.Should().Be(candidate);
            XUnitIntegration.GetTestModuleName(ref runnerInstance).Should().Be(moduleName);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), moduleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason), Times.Once);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitRunAsyncRecordsExactCoverageBackfillCandidateForItrSkip()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.IsRunning).Returns(true);
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var testSuite = typeof(TestOptimizationFeatureTests).FullName;
        var moduleName = typeof(TestOptimizationFeatureTests).Assembly.GetName().Name;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var reason = string.Empty;
        var previousModule = TestModule.Current;
        var previousSuite = TestSuite.Current;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), moduleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason)).Returns(true);

        var runner = new XUnitRunnerStub
        {
            TestClass = typeof(TestOptimizationFeatureTests),
            TestMethod = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static),
            TestMethodArguments = [],
            TestCase = new XUnitTestCaseStub()
        };

        try
        {
            TestOptimization.Instance = testOptimization.Object;
            TestModule.Current = null;
            TestSuite.Current = null;

            XUnitTestRunnerRunAsyncIntegration.OnMethodBegin(runner);

            runner.SkipReason.Should().Be(IntelligentTestRunnerTags.SkippedByReason);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, moduleName), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestSuite.Current = previousSuite;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonRecordsCoverageBackfillStateOnlyWhenFrameworkCommitsToSkip()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.RecordTestSkipCoverageBackfill();

            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill((string)null), Times.Once);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonShouldSkipReturnsMatchedCandidateWhenCoverageBackfillBlocksSkip()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        const string ModuleName = "Samples.NUnitTests";
        const string TestSuite = "Samples.NUnitTests.TestSuite";
        const string TestName = "SimplePassTest";
        var candidate = new SkippableTest(TestName, TestSuite, parameters: null, configurations: null);
        var reason = "backend marked the test as missing line coverage";

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(TestSuite, TestName, ModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason)).Returns(false);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(TestSuite, TestName, testMethodArguments: null, methodParameters: null, out var matchedSkippableTest, ModuleName).Should().BeFalse();

            matchedSkippableTest.Should().Be(candidate);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(TestSuite, TestName, ModuleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason), Times.Once);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonShouldSkipUsesExplicitModuleNameForCoverageBackfill()
    {
        var settings = CreateSettings();
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        const string ModuleName = "Samples.NUnitTests";
        const string TestSuite = "Samples.NUnitTests.TestSuite";
        const string TestName = "SimplePassTest";
        var candidate = new SkippableTest(TestName, TestSuite, parameters: null, configurations: null);
        var reason = string.Empty;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(TestSuite, TestName, ModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason)).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(TestSuite, TestName, testMethodArguments: null, methodParameters: null, out var matchedSkippableTest, ModuleName).Should().BeTrue();

            matchedSkippableTest.Should().Be(candidate);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(TestSuite, TestName, ModuleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason), Times.Once);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonShouldSkipMatchesParametersMetadataTestName()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static);
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string DisplayName = "SampleParameterizedItrTest(value: 1)";
        var parameters = """{"metadata":{"test_name":"SampleParameterizedItrTest(value: 1)"},"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [1], method!.GetParameters(), metadataTestName: DisplayName).Should().BeTrue();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonShouldSkipRejectsDifferentParametersMetadataTestName()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static);
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var parameters = """{"metadata":{"test_name":"SampleParameterizedItrTest(value: 1)"},"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [1], method!.GetParameters(), metadataTestName: "SampleParameterizedItrTest(value: 2)").Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void CommonShouldSkipRejectsBackendCandidateWithExtraArguments()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static);
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var parameters = """{"arguments":{"value":"1","oldValue":"2"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [1], method!.GetParameters(), out var matchedSkippableTest).Should().BeFalse();

            matchedSkippableTest.Should().BeNull();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitV3RunTestCaseUsesTestCaseArgumentsForParameterizedRows()
    {
        var testCaseArguments = new object[] { 1 };
        var methodArguments = new object[] { 2 };
        var testMethod = new Mock<XUnitV3TestMethod>();
        testMethod.Setup(x => x.TestMethodArguments).Returns(methodArguments);
        var testCase = new Mock<XUnitV3TestCase>();
        testCase.Setup(x => x.TestMethod).Returns(testMethod.Object);
        var originalTestCase = new XUnitV3TestCaseWithArguments
        {
            TestMethodArguments = testCaseArguments
        };

        XUnitV3RunTestCaseIntegration.GetTestCaseMethodArguments(originalTestCase, testCase.Object)
                                     .Should()
                                     .BeSameAs(testCaseArguments);
    }

    [Fact]
    public void XUnitV3RunTestCaseRecordsExactCoverageBackfillCandidateForItrSkip()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.IsRunning).Returns(true);
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var moduleName = method.DeclaringType!.Assembly.GetName().Name!;
        var candidate = new SkippableTest(method.Name, testSuite, parameters: null, configurations: null);
        var reason = string.Empty;
        var previousModule = TestModule.Current;
        var previousSuite = TestSuite.Current;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, method.Name, moduleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, moduleName, out reason)).Returns(true);

        var testClass = new Mock<XUnitV3TestClass>();
        testClass.Setup(x => x.Class).Returns(typeof(TestOptimizationFeatureTests));
        testClass.Setup(x => x.TestClassName).Returns(testSuite);
        testClass.Setup(x => x.Traits).Returns(new Dictionary<string, IReadOnlyCollection<string>>());

        var testMethod = new Mock<XUnitV3TestMethod>();
        testMethod.Setup(x => x.Method).Returns(method);
        testMethod.Setup(x => x.TestClass).Returns(testClass.Object);
        testMethod.Setup(x => x.TestMethodArguments).Returns([]);
        testMethod.Setup(x => x.UniqueID).Returns(method.Name);

        var testCase = new Mock<XUnitV3TestCase>();
        testCase.SetupProperty(x => x.SkipReason);
        testCase.Setup(x => x.Instance).Returns(testCase.Object);
        testCase.Setup(x => x.Type).Returns(testCase.Object.GetType());
        testCase.Setup(x => x.TestClass).Returns(testClass.Object);
        testCase.Setup(x => x.TestClassName).Returns(testSuite);
        testCase.Setup(x => x.TestMethod).Returns(testMethod.Object);
        testCase.Setup(x => x.TestMethodName).Returns(method.Name);
        testCase.Setup(x => x.TestCaseDisplayName).Returns(method.Name);
        testCase.Setup(x => x.Traits).Returns([]);
        testCase.Setup(x => x.UniqueID).Returns(method.Name);

        var context = new Mock<XUnitV3Context>();
        context.Setup(x => x.Instance).Returns(context.Object);
        context.Setup(x => x.Type).Returns(context.Object.GetType());
        context.Setup(x => x.TestMethod).Returns(testMethod.Object);
        context.Setup(x => x.MessageBus).Returns(new object());
        context.Setup(x => x.ConstructorArguments).Returns([]);

        try
        {
            TestOptimization.Instance = testOptimization.Object;
            TestModule.Current = null;
            TestSuite.Current = null;

            XUnitV3RunTestCaseIntegration.OnMethodBegin(new object(), context.Object, testCase.Object);

            testCase.Object.SkipReason.Should().Be(IntelligentTestRunnerTags.SkippedByReason);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, moduleName), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestSuite.Current = previousSuite;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestUpdateTestParametersPersistsLateDisplayNameMetadata()
    {
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        TestOptimization.Instance = testOptimization.Object;

        TestSession session = null;
        TestModule module = null;
        TestSuite suite = null;
        Test test = null;
        try
        {
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null);
            module = session.CreateModule("Samples.MSTestTests");
            suite = module.GetOrCreateSuite("Samples.MSTestTests.TestSuite");
            test = suite.CreateTest(nameof(SampleParameterizedItrTest));
            var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var testMethod = new MsTestMethodStub(method, [1], nameof(SampleParameterizedItrTest));

            MsTestIntegration.UpdateTestParameters(test, testMethod, displayName: "Custom display name");

            test.GetTags().Parameters.Should().Contain("\"metadata\":{\"test_name\":\"Custom display name\"}");
            test.GetTags().Parameters.Should().Contain("\"arguments\":{\"value\":\"1\"}");
        }
        finally
        {
            test?.Close(TestStatus.Pass);
            suite?.Close();
            module?.Close();
            session?.Close(TestStatus.Pass);
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestUpdateTestParametersPersistsLateDisplayNameMetadataWithoutArguments()
    {
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        TestOptimization.Instance = testOptimization.Object;

        TestSession session = null;
        TestModule module = null;
        TestSuite suite = null;
        Test test = null;
        try
        {
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null);
            module = session.CreateModule("Samples.MSTestTests");
            suite = module.GetOrCreateSuite("Samples.MSTestTests.TestSuite");
            test = suite.CreateTest(nameof(SampleXUnitItrTest));
            var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
            var testMethod = new MsTestMethodStub(method, [], nameof(SampleXUnitItrTest));

            MsTestIntegration.UpdateTestParameters(test, testMethod, displayName: "No argument display name");

            test.GetTags().Parameters.Should().Contain("\"metadata\":{\"test_name\":\"No argument display name\"}");
        }
        finally
        {
            test?.Close(TestStatus.Pass);
            suite?.Close();
            module?.Close();
            session?.Close(TestStatus.Pass);
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipMatchesParametersMetadataTestName()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string DisplayName = "SampleParameterizedItrTest(value: 1)";
        var parameters = """{"metadata":{"test_name":"SampleParameterizedItrTest(value: 1)"},"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [1], DisplayName);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out var isUnskippable, out var isForcedRun, traits: []).Should().BeTrue();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipRejectsLateDisplayNameMetadataUntilDisplayNameIsResolved()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var parameters = """{"metadata":{"test_name":"SampleParameterizedItrTest(value: 1)"},"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [1], nameof(SampleParameterizedItrTest));

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out var isUnskippable, out var isForcedRun, traits: []).Should().BeFalse();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipRejectsLateDisplayNameMetadataWhenMethodHasNoArguments()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var parameters = """{"metadata":{"test_name":"Custom display name"}}""";
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [], nameof(SampleXUnitItrTest));

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out _, out _, traits: []).Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipMatchesCandidateNamedByDisplayName()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string DisplayName = "Custom display name";
        var parameters = """{"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(DisplayName, testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [1], DisplayName);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, DisplayName, It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out var isUnskippable, out var isForcedRun, traits: []).Should().BeTrue();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipRejectsDisplayNameCandidateWhenMetadataDiffers()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string DisplayName = "Custom display name";
        var parameters = """{"metadata":{"test_name":"Other display name"},"arguments":{"value":"1"}}""";
        var candidate = new SkippableTest(DisplayName, testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [1], DisplayName);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, DisplayName, It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out _, out _, traits: []).Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipRejectsBackendCandidateWithExtraArguments()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var parameters = """{"arguments":{"value":"1","oldValue":"2"}}""";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [1], nameof(SampleParameterizedItrTest));

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            MsTestIntegration.ShouldSkip(testMethod, out _, out _, traits: []).Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestShouldSkipUsesMethodAssemblyModuleForCoverageBackfillWhenNoModuleIsCurrent()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var testMethod = new MsTestMethodStub(method, [], nameof(SampleXUnitItrTest));
        var reason = string.Empty;
        var previousModule = TestModule.Current;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), expectedModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, expectedModuleName, out reason)).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            TestModule.Current = null;

            MsTestIntegration.ShouldSkip(testMethod, out var isUnskippable, out var isForcedRun, out var skippableTest, traits: []).Should().BeTrue();

            isUnskippable.Should().BeFalse();
            isForcedRun.Should().BeFalse();
            skippableTest.Should().Be(candidate);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), expectedModuleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, expectedModuleName, out reason), Times.Once);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void MsTestUnskippableForcedRunSurvivesUnsafeCoverageBackfill()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var testMethod = new MsTestMethodStub(method, [], nameof(SampleXUnitItrTest));
        var reason = "backend marked the test as missing line coverage";
        var previousModule = TestModule.Current;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), expectedModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, expectedModuleName, out reason)).Returns(false);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            TestModule.Current = null;

            MsTestIntegration.ShouldSkip(
                testMethod,
                out var isUnskippable,
                out var isForcedRun,
                out var skippableTest,
                new Dictionary<string, List<string>>
                {
                    [IntelligentTestRunnerTags.UnskippableTraitName] = ["true"]
                }).Should().BeFalse();

            isUnskippable.Should().BeTrue();
            isForcedRun.Should().BeTrue();
            skippableTest.Should().BeNull();
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, expectedModuleName, out reason), Times.Once);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void NUnitUnskippableForcedRunSurvivesUnsafeCoverageBackfill()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var currentTest = new NUnitTestStub(method, nameof(SampleXUnitItrTest), []);
        var reason = "backend marked the test as missing line coverage";

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), It.IsAny<string>())).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, It.IsAny<string>(), out reason)).Returns(false);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            NUnitIntegration.ShouldSkip(
                currentTest,
                out var isUnskippable,
                out var isForcedRun,
                out var skippableTest,
                new Dictionary<string, List<string>>
                {
                    [IntelligentTestRunnerTags.UnskippableTraitName] = ["true"]
                }).Should().BeFalse();

            isUnskippable.Should().BeTrue();
            isForcedRun.Should().BeTrue();
            skippableTest.Should().BeNull();
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, It.IsAny<string>(), out reason), Times.Once);
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void NUnitPerformWorkRecordsExactCoverageBackfillCandidateForRunnableItrSkip()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.IsRunning).Returns(true);
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string ModuleName = "Samples.NUnitTests";
        var candidate = new SkippableTest(nameof(SampleXUnitItrTest), testSuite, parameters: null, configurations: null);
        var assemblyTest = new NUnitTestStub(method, ModuleName, [], testType: "Assembly");
        var currentTest = new NUnitTestStub(method, nameof(SampleXUnitItrTest), [], parent: assemblyTest)
        {
            RunState = NUnitRunState.Runnable
        };
        var workItem = new NUnitWorkItemStub(currentTest);
        var reason = string.Empty;
        var previousModule = TestModule.Current;
        TestModule module = null;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), ModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason)).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            module = TestModule.Create(ModuleName, CommonTags.TestingFrameworkNameNUnit, "3.0.0");
            NUnitIntegration.SetTestModuleTo(assemblyTest, module);
            TestModule.Current = null;

            NUnitWorkItemPerformWorkIntegration.OnMethodBegin(workItem);

            currentTest.RunState.Should().Be(NUnitRunState.Ignored);
            currentTest.Properties.Get(NUnitIntegration.SkipReasonKey).Should().Be(IntelligentTestRunnerTags.SkippedByReason);
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), ModuleName), Times.Once);
            skippableFeature.Verify(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(candidate, ModuleName), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            module?.Close();
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Theory]
    [InlineData((int)NUnitRunState.Ignored)]
    [InlineData((int)NUnitRunState.Skipped)]
    [InlineData((int)NUnitRunState.Explicit)]
    [InlineData((int)NUnitRunState.NotRunnable)]
    public void NUnitNonRunnableTestCannotBeConvertedToItrSkip(int runState)
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var currentTest = new NUnitTestStub(method, nameof(SampleXUnitItrTest), [])
        {
            RunState = (NUnitRunState)runState
        };

        NUnitWorkItemPerformWorkIntegration.CanApplyItrSkip(currentTest, testManagementProperties: null).Should().BeFalse();
    }

    [Fact]
    public void NUnitRunnableTestCanBeConvertedToItrSkipWhenTestManagementDidNotDisableIt()
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var currentTest = new NUnitTestStub(method, nameof(SampleXUnitItrTest), [])
        {
            RunState = NUnitRunState.Runnable
        };

        NUnitWorkItemPerformWorkIntegration.CanApplyItrSkip(currentTest, testManagementProperties: null).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void TestManagementTestsThatNeedExecutionCannotBeConvertedToItrSkip(bool quarantined, bool disabled, bool attemptToFix)
    {
        var properties = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined, disabled, attemptToFix);

        Common.CanApplyItrSkip(properties).Should().BeFalse();
    }

    [Fact]
    public void DisabledTestManagementTestWithoutAttemptToFixCannotBeConvertedToItrSkip()
    {
        var properties = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined: false, disabled: true, attemptToFix: false);

        Common.IsDisabledByTestManagement(properties).Should().BeTrue();
        Common.CanApplyItrSkip(properties).Should().BeFalse();
    }

    [Fact]
    public void DefaultTestManagementPropertiesCanBeConvertedToItrSkip()
    {
        Common.CanApplyItrSkip(TestOptimizationClient.TestManagementResponseTestPropertiesAttributes.Default).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    public void NUnitRunnableTestWithTestManagementExecutionPolicyCannotBeConvertedToItrSkip(bool quarantined, bool disabled, bool attemptToFix)
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var currentTest = new NUnitTestStub(method, nameof(SampleXUnitItrTest), [])
        {
            RunState = NUnitRunState.Runnable
        };
        var properties = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined, disabled, attemptToFix);

        NUnitWorkItemPerformWorkIntegration.CanApplyItrSkip(currentTest, properties).Should().BeFalse();
    }

    [Fact]
    public void MsTestRecordsCoverageBackfillSkipWithMethodAssemblyModuleWhenNoModuleIsCurrent()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var expectedModuleName = method.DeclaringType!.Assembly.GetName().Name!;
        var testMethod = new MsTestMethodStub(method, [], nameof(SampleXUnitItrTest));
        var previousModule = TestModule.Current;

        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            TestModule.Current = null;

            MsTestIntegration.RecordTestSkipCoverageBackfill(testMethod);

            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(expectedModuleName), Times.Once);
        }
        finally
        {
            TestModule.Current = previousModule;
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void XUnitV3QuarantinedOrDisabledRetryRunSummaryIsHidden()
    {
        var runSummary = new XUnitV3RunSummary
        {
            Total = 3,
            Failed = 2,
            Skipped = 0,
            NotRun = 1
        };

        XUnitV3RunTestCaseIntegration.HideQuarantinedOrDisabledRunSummary(ref runSummary);

        runSummary.Total.Should().Be(1);
        runSummary.Failed.Should().Be(0);
        runSummary.Skipped.Should().Be(0);
        runSummary.NotRun.Should().Be(0);
    }

    [Fact]
    public void XUnitV3QuarantinedOrDisabledFinalRunSummaryIsReportedAsSkipped()
    {
        var runSummary = new XUnitV3RunSummary
        {
            Total = 3,
            Failed = 2,
            Skipped = 0,
            NotRun = 1
        };

        XUnitV3RunTestCaseIntegration.ReportQuarantinedOrDisabledRunSummaryAsSkipped(ref runSummary);
        var returnedRunSummary = XUnitV3RunTestCaseIntegration.ToRunSummaryReturnValue<XUnitV3RunSummary>(ref runSummary);

        returnedRunSummary.Total.Should().Be(1);
        returnedRunSummary.Failed.Should().Be(0);
        returnedRunSummary.Skipped.Should().Be(1);
        returnedRunSummary.NotRun.Should().Be(0);
    }

    [Fact]
    public void CoverageBackfillStateUsesScopedCoverageAfterFirstActualSkip()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var otherCandidate = new SkippableTest("OtherPassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate, otherCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var firstReason).Should().BeTrue();
            skippableFeature.RecordTestSkipCoverageBackfill(candidate, "Samples.XUnitTests");

            firstReason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeTrue();
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            var featureBackfillDataAfterFirstSkip = skippableFeature.GetCoverageBackfillData();
            featureBackfillDataAfterFirstSkip.IsPresent.Should().BeTrue();
            featureBackfillDataAfterFirstSkip.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillDataAfterFirstSkip).Should().BeTrue();
            coverageBackfillDataAfterFirstSkip.IsPresent.Should().BeTrue();

            skippableFeature.CanSkipWithCoverageBackfill(otherCandidate, "Samples.XUnitTests", out var secondReason).Should().BeTrue();
            skippableFeature.RecordTestSkipCoverageBackfill(otherCandidate, "Samples.XUnitTests");

            secondReason.Should().BeEmpty();
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            var featureBackfillData = skippableFeature.GetCoverageBackfillData();
            featureBackfillData.IsPresent.Should().BeTrue();
            featureBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();
            coverageBackfillData.IsPresent.Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillStateOnlyUsesScopesWithActualSkips()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var client = new TestOptimizationClientStub(
                skippableTestsResponseFactory: scope =>
                {
                    var backendPath = scope.TestBundle == "Skipped.Tests" ? "src/Skipped.cs" : "src/OnlyRan.cs";
                    return new TestOptimizationClient.SkippableTestsResponse(
                        correlationId: $"{scope.TestBundle}-correlation-id",
                        tests: [new SkippableTest("SimplePassTest", $"{scope.TestBundle}.TestSuite", parameters: null, configurations: null)],
                        CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { [backendPath] = "gA==" }),
                        isCoverageBackfillSafe: true);
                });
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            var skippedCandidate = skippableFeature.GetSkippableTestsFromSuiteAndName("Skipped.Tests.TestSuite", "SimplePassTest", "Skipped.Tests").Should().ContainSingle().Subject;
            skippableFeature.GetSkippableTestsFromSuiteAndName("OnlyRan.Tests.TestSuite", "SimplePassTest", "OnlyRan.Tests").Should().ContainSingle();
            skippableFeature.RecordTestSkipCoverageBackfill(skippedCandidate, "Skipped.Tests");

            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            var featureBackfillData = skippableFeature.GetCoverageBackfillData();
            featureBackfillData.IsPresent.Should().BeTrue();
            featureBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Skipped.cs");
            featureBackfillData.ExecutedLinesByRelativePath.Should().NotContainKey("src/OnlyRan.cs");
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillStateKeepsBackendCoverageForMixedScopedCandidates()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var scopedCandidate = new SkippableTest(
                "SimplePassTest",
                "Samples.XUnitTests.TestSuite",
                parameters: null,
                new TestsConfigurations(
                    "linux",
                    "1",
                    "x64",
                    runtimeName: null,
                    runtimeVersion: null,
                    runtimeArchitecture: null,
                    custom: null,
                    testBundle: "Samples.XUnitTests"));
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate, scopedCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();
            skippableFeature.RecordTestSkipCoverageBackfill(candidate, "Samples.XUnitTests");

            reason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeTrue();
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            var featureBackfillData = skippableFeature.GetCoverageBackfillData();
            featureBackfillData.IsPresent.Should().BeTrue();
            featureBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void CoverageBackfillStateIsRecordedWhenSingleScopedCandidateIsSkipped()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations: null);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var firstReason).Should().BeTrue();
            skippableFeature.RecordTestSkipCoverageBackfill(candidate, "Samples.XUnitTests");

            firstReason.Should().BeEmpty();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeTrue();
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            var featureBackfillData = skippableFeature.GetCoverageBackfillData();
            featureBackfillData.IsPresent.Should().BeTrue();
            featureBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordedItrSkipTagDoesNotRecordCoverageBackfillState()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var client = new TestOptimizationClientStub();
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            skippableFeature.RecordTestSkippedByItr(123, "Samples.XUnitTests");

            skippableFeature.HasSkippedTestsByItr(123).Should().BeTrue();
            skippableFeature.HasSkippedTestsByItr(456).Should().BeFalse();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeFalse();
            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out _).Should().BeFalse();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void ClosingFrameworkSkippedTestWithItrReasonDoesNotRecordCoverageBackfillState()
    {
        var settings = CreateSettings();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);
        TestOptimization.Instance = testOptimization.Object;

        TestSession session = null;
        TestModule module = null;
        TestSuite suite = null;
        try
        {
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: null, startDate: null);
            module = session.CreateModule("Samples.XUnitTests");
            suite = module.GetOrCreateSuite("Samples.XUnitTests.TestSuite");
            var test = suite.CreateTest("SkipByITRSimulation");

            test.Close(TestStatus.Skip, TimeSpan.Zero, IntelligentTestRunnerTags.SkippedByReason);

            skippableFeature.Verify(x => x.RecordTestSkippedByItr(session.Tags.SessionId, "Samples.XUnitTests"), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            suite?.Close();
            module?.Close();
            session?.Close(TestStatus.Pass);
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void SessionSkippedFlagDoesNotReuseItrSkipFromPreviousSession()
    {
        var settings = CreateSettings();
        var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
        var client = new TestOptimizationClientStub();
        var testOptimization = CreateTestOptimization(settings, Directory.GetCurrentDirectory());
        var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature);
        TestOptimization.Instance = testOptimization.Object;

        TestSession firstSession = null;
        TestModule firstModule = null;
        TestSuite firstSuite = null;
        TestSession secondSession = null;
        try
        {
            firstSession = TestSession.GetOrCreate("dotnet test first", workingDirectory: null, framework: null, startDate: null);
            firstModule = firstSession.CreateModule("Samples.XUnitTests");
            firstSuite = firstModule.GetOrCreateSuite("Samples.XUnitTests.TestSuite");
            var firstTest = firstSuite.CreateTest("SkipByITRSimulation");

            firstTest.Close(TestStatus.Skip, TimeSpan.Zero, IntelligentTestRunnerTags.SkippedByReason);
            firstSuite.Close();
            firstModule.Close();
            firstSession.Close(TestStatus.Pass);

            secondSession = TestSession.GetOrCreate("dotnet test second", workingDirectory: null, framework: null, startDate: null);
            secondSession.Close(TestStatus.Pass);

            skippableFeature.HasSkippedTestsByItr(firstSession.Tags.SessionId).Should().BeTrue();
            skippableFeature.HasSkippedTestsByItr(secondSession.Tags.SessionId).Should().BeFalse();
            secondSession.Tags.TestsSkipped.Should().Be("false");
        }
        finally
        {
            firstSuite?.Close();
            firstModule?.Close();
            firstSession?.Close(TestStatus.Pass);
            secondSession?.Close(TestStatus.Pass);
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void SkippableCandidateDoesNotUseCustomConfigurationsAsModuleScope()
    {
        var configurations = new TestsConfigurations(
            osPlatform: "linux",
            osVersion: "test-os",
            osArchitecture: "x64",
            runtimeName: ".NET",
            runtimeVersion: "10.0",
            runtimeArchitecture: "x64",
            custom: new Dictionary<string, string>
            {
                [TestTags.Bundle] = string.Empty,
                [TestTags.Module] = "Samples.XUnitTests"
            });
        var skippableTest = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations);

        skippableTest.TryGetModuleScope(out _).Should().BeFalse();
        skippableTest.MatchesModuleScope("Samples.XUnitTests").Should().BeTrue();
        skippableTest.MatchesModuleScope("Other.Tests").Should().BeTrue();
    }

    [Fact]
    public void SkippableResponseDoesNotFilterCandidatesByCustomConfigurations()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect \"XPlat Code Coverage\"");
            Environment.SetEnvironmentVariable(ConfigurationKeys.GlobalTags, "test.configuration.environment:local");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var configurations = new TestsConfigurations(
                osPlatform: "linux",
                osVersion: "test-os",
                osArchitecture: "x64",
                runtimeName: ".NET",
                runtimeVersion: "10.0",
                runtimeArchitecture: "x64",
                custom: new Dictionary<string, string>
                {
                    ["environment"] = "backend"
                },
                testBundle: "Samples.XUnitTests");
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath);
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            var skippableTests = skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");

            skippableTests.Should().ContainSingle();
            skippableTests[0].Configurations?.Custom.Should().Contain("environment", "backend");
            skippableFeature.CanSkipWithCoverageBackfill(skippableTests[0], "Samples.XUnitTests", out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
    }

    [Fact]
    public void InvalidKnownTestsResponseDisablesEarlyFlakeDetectionWhenQueriedFirst()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.KnownTestsEnabled, "true"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "true"));
        var remoteSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var client = new TestOptimizationClientStub(knownTestsResponse: default);

        var knownTestsFeature = TestOptimizationKnownTestsFeature.Create(settings, remoteSettings, client);
        var earlyFlakeDetectionFeature = TestOptimizationEarlyFlakeDetectionFeature.Create(settings, remoteSettings, knownTestsFeature);

        earlyFlakeDetectionFeature.Enabled.Should().BeFalse();
        settings.KnownTestsEnabled.Should().BeFalse();
        settings.EarlyFlakeDetectionEnabled.Should().BeFalse();
        knownTestsFeature.Enabled.Should().BeFalse();
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
    {
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        return new TestOptimizationSettings(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance);
    }

    private static void ClearCoverageBackfillEnvironment()
    {
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillActualSkip, null);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillPath, null);
        Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibilityItrCoverageBackfillRunFolder, null);
    }

    private static TestOptimizationClient.SettingsResponse CreateRemoteSettingsResponse(bool? testsSkippingEnabled)
        => new(
            codeCoverage: true,
            testsSkipping: testsSkippingEnabled,
            requireGit: false,
            impactedTestsEnabled: false,
            flakyTestRetries: false,
            earlyFlakeDetection: new TestOptimizationClient.EarlyFlakeDetectionSettingsResponse(
                enabled: false,
                slowTestRetries: new TestOptimizationClient.SlowTestRetriesSettingsResponse(),
                faultySessionThreshold: 0),
            knownTestsEnabled: false,
            testManagement: new TestOptimizationClient.TestManagementSettingsResponse(
                enabled: false,
                attemptToFixRetries: 0),
            dynamicInstrumentationEnabled: false);

    private static Mock<ITestOptimization> CreateTestOptimization(TestOptimizationSettings settings, string workspacePath, string runId = "test-run")
    {
        var hostInfo = new Mock<ITestOptimizationHostInfo>();
        hostInfo.Setup(x => x.GetOperatingSystemVersion()).Returns("test-os-version");

        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.RunId).Returns(runId);
        testOptimization.Setup(x => x.Settings).Returns(settings);
        testOptimization.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(workspacePath));
        testOptimization.Setup(x => x.HostInfo).Returns(hostInfo.Object);
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(TestOptimizationFeatureTests)));
        return testOptimization;
    }

    private static void SampleXUnitItrTest()
    {
    }

    private static void SampleParameterizedItrTest(int value)
    {
    }

    private sealed class XUnitV3TestCaseWithArguments
    {
        public object[] TestMethodArguments { get; set; }
    }

    private sealed class XUnitTestCaseStub : ITestCase
    {
        public object Instance => this;

        public Type Type => typeof(XUnitTestCaseStub);

        public string DisplayName { get; set; } = nameof(SampleXUnitItrTest);

        public Dictionary<string, List<string>> Traits { get; set; } = [];

        public string UniqueID { get; set; } = nameof(SampleXUnitItrTest);

        public ref TReturn GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotImplementedException();
        }
    }

    private sealed class XUnitRunnerStub : ITestRunner
    {
        public Type TestClass { get; set; }

        public MethodInfo TestMethod { get; set; }

        public object[] TestMethodArguments { get; set; }

        public ITestCase TestCase { get; set; }

        public IExceptionAggregator Aggregator { get; set; }

        public string SkipReason { get; set; }

        public string DisplayName => TestCase?.DisplayName ?? string.Empty;

        public object MessageBus { get; set; } = new object();

        public object RunAsync()
        {
            throw new NotImplementedException();
        }
    }

    private sealed class MsTestMethodStub : MsTestMethod
    {
        private readonly MethodInfo _methodInfo;
        private readonly object[] _arguments;
        private readonly string _testMethodName;

        public MsTestMethodStub(MethodInfo methodInfo, object[] arguments, string testMethodName)
        {
            _methodInfo = methodInfo;
            _arguments = arguments;
            _testMethodName = testMethodName;
        }

        public object Instance => this;

        public Type Type => typeof(MsTestMethodStub);

        public string TestMethodName => _testMethodName;

        public string TestClassName => typeof(TestOptimizationFeatureTests).FullName;

        public MethodInfo MethodInfo => _methodInfo;

        public object[] Arguments => _arguments;

        public ref TReturn GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotImplementedException();
        }
    }

    private sealed class NUnitTestStub : NUnitTest
    {
        private readonly NUnitMethodInfoStub _method;
        private readonly string _name;
        private readonly object[] _arguments;
        private readonly string _testType;
        private readonly NUnitTest _parent;

        public NUnitTestStub(MethodInfo methodInfo, string name, object[] arguments, string testType = "TestMethod", NUnitTest parent = null)
        {
            _method = new NUnitMethodInfoStub(methodInfo);
            _name = name;
            _arguments = arguments;
            _testType = testType;
            _parent = parent;
        }

        public object Instance => this;

        public Type Type => typeof(NUnitTestStub);

        public string Id => "nunit-test-id";

        public string Name => _name;

        public string TestType => _testType;

        public string FullName => $"{typeof(TestOptimizationFeatureTests).FullName}.{_name}";

        public string ClassName => typeof(TestOptimizationFeatureTests).FullName;

        public string MethodName => _name;

        public NUnitTypeInfo TypeInfo => null;

        public NUnitMethodInfo Method => _method;

        public NUnitRunState RunState { get; set; } = NUnitRunState.Runnable;

        public int TestCaseCount => 1;

        public NUnitPropertyBag Properties { get; } = new NUnitPropertyBagStub();

        public bool IsSuite => false;

        public bool HasChildren => false;

        public IList Tests => null;

        public object Fixture => null;

        public object[] Arguments => _arguments;

        public NUnitTest Parent => _parent;

        public NUnitTestResult MakeTestResult()
        {
            throw new NotImplementedException();
        }

        public ref TReturn GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotImplementedException();
        }
    }

    private sealed class NUnitWorkItemStub : NUnitWorkItem
    {
        public NUnitWorkItemStub(NUnitTest test)
        {
            Test = test;
        }

        public object Instance => this;

        public Type Type => typeof(NUnitWorkItemStub);

        public NUnitTest Test { get; }

        public NUnitTestResult Result => null;

        public ref TReturn GetInternalDuckTypedInstance<TReturn>()
        {
            throw new NotImplementedException();
        }
    }

    private sealed class NUnitMethodInfoStub : NUnitMethodInfo
    {
        public NUnitMethodInfoStub(MethodInfo methodInfo)
        {
            MethodInfo = methodInfo;
        }

        public MethodInfo MethodInfo { get; }
    }

    private sealed class NUnitPropertyBagStub : NUnitPropertyBag
    {
        private readonly Dictionary<string, IList> _values = [];

        public ICollection<string> Keys => _values.Keys;

        public IList this[string key] => _values.TryGetValue(key, out var values) ? values : null;

        public object Get(string key) => _values.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

        public void Set(string key, object value)
        {
            _values[key] = new ArrayList { value };
        }
    }

    private sealed class TestOptimizationClientStub(
        TestOptimizationClient.KnownTestsResponse knownTestsResponse = default,
        TestOptimizationClient.SkippableTestsResponse skippableTestsResponse = default,
        Func<SkippableTestsRequestScope, TestOptimizationClient.SkippableTestsResponse> skippableTestsResponseFactory = null) : ITestOptimizationClient
    {
        public List<SkippableTestsRequestScope> SkippableRequestScopes { get; } = [];

        public Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
            => Task.FromResult(default(TestOptimizationClient.SettingsResponse));

        public Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync()
            => Task.FromResult(knownTestsResponse);

        public Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
            => Task.FromResult(default(TestOptimizationClient.SearchCommitResponse));

        public Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync(SkippableTestsRequestScope scope = default)
        {
            SkippableRequestScopes.Add(scope);
            return Task.FromResult(skippableTestsResponseFactory?.Invoke(scope) ?? skippableTestsResponse);
        }

        public Task<long> SendPackFilesAsync(string commitSha, string[] commitsToInclude, string[] commitsToExclude)
            => Task.FromResult(0L);

        public Task<long> UploadRepositoryChangesAsync()
            => Task.FromResult(0L);

        public Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
            => Task.FromResult(default(TestOptimizationClient.TestManagementResponse));
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
            Repository = "https://github.com/DataDog/dd-trace-dotnet";
            Commit = "abcdef123456";
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
