// <copyright file="TestOptimizationFeatureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
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
using Datadog.Trace.Processors;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;
using MsTestIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2.MsTestIntegration;
using MsTestMethod = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2.ITestMethod;
using NUnitAttemptToFixRetryBehavior = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.AttemptToFixRetryBehavior;
using NUnitIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.NUnitIntegration;
using NUnitMethodInfo = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IMethodInfo;
using NUnitPropertyBag = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IPropertyBag;
using NUnitResultState = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IResultState;
using NUnitRunState = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.RunState;
using NUnitTest = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITest;
using NUnitTestResult = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITestResult;
using NUnitTestStatus = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.TestStatus;
using NUnitTypeInfo = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.ITypeInfo;
using NUnitWorkItem = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.IWorkItem;
using NUnitWorkItemPerformWorkIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit.NUnitWorkItemPerformWorkIntegration;
using XUnitV3Context = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestMethodRunnerBaseContextV3;
using XUnitV3RunTestCaseIntegration = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.XUnitTestMethodRunnerBaseRunTestCaseV3Integration;
using XUnitV3TestCase = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestCaseV3;
using XUnitV3TestClass = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestClassV3;
using XUnitV3TestMethod = Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3.IXunitTestMethodV3;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
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
    public void CoverageBackfillSkipGateAllowsTargetFrameworkSelectionWhenBackendConfigurationsAreHomogeneous()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --framework net8.0 --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var configurations = CreateBackendConfigurations("8.0.8");
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations, missingLineCodeCoverage: false);
            var otherCandidate = new SkippableTest("OtherPassTest", "Samples.XUnitTests.TestSuite", parameters: null, configurations, missingLineCodeCoverage: false);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate, otherCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            var skippableTests = skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");

            skippableTests.Should().ContainSingle();
            skippableFeature.CanSkipWithCoverageBackfill(skippableTests[0], "Samples.XUnitTests", out var reason).Should().BeTrue();
            skippableFeature.RecordTestSkipCoverageBackfill(skippableTests[0], "Samples.XUnitTests");

            reason.Should().BeEmpty();
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            skippableFeature.GetCoverageBackfillData().IsPresent.Should().BeTrue();
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
    public void CoverageBackfillSkipGateFailsTargetFrameworkSelectionWhenBackendConfigurationsDiffer()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --framework net8.0 --collect \"XPlat Code Coverage\"");
            var settings = CreateSettings();
            var remoteSettings = CreateRemoteSettingsResponse(testsSkippingEnabled: true);
            var candidate = new SkippableTest("SimplePassTest", "Samples.XUnitTests.TestSuite", parameters: null, CreateBackendConfigurations("8.0.8"), missingLineCodeCoverage: false);
            var otherCandidate = new SkippableTest("OtherPassTest", "Samples.XUnitTests.TestSuite", parameters: null, CreateBackendConfigurations("9.0.0"), missingLineCodeCoverage: false);
            var response = new TestOptimizationClient.SkippableTestsResponse(
                correlationId: "correlation-id",
                tests: [candidate, otherCandidate],
                CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { ["src/Calculator.cs"] = "wA==" }),
                isCoverageBackfillSafe: true);
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, remoteSettings, client, testOptimization.Object);

            var skippableTests = skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");

            skippableTests.Should().ContainSingle();
            skippableFeature.CanSkipWithCoverageBackfill(skippableTests[0], "Samples.XUnitTests", out var reason).Should().BeFalse();

            reason.Should().Contain("multiple configurations");
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

    [Theory]
    [InlineData(null, true)]
    [InlineData("{}", true)]
    [InlineData(null, false)]
    public async Task CoverageBackfillSkipGateAllowsConfigurationsRemovedByBackend(string responseConfigurations, bool selectsTargetFramework)
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            var frameworkOption = selectsTargetFramework ? " --framework net10.0" : string.Empty;
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, $"dotnet test{frameworkOption} --collect \"Code Coverage;Format=Xml\"");
            var settings = CreateSettings();
            var testOptimization = CreateTestOptimization(settings, workspacePath, runId: "injected-run");
            var configurationAttribute = responseConfigurations is null ? string.Empty : $", \"configurations\": {responseConfigurations}";
            var responseJson = $$$"""
                                 {
                                   "data": [{"id": "test-id", "type": "test", "attributes": {
                                     "name": "SimplePassTest", "suite": "Samples.XUnitTests.TestSuite"{{{configurationAttribute}}}
                                   }}],
                                   "meta": {"correlation_id": "correlation-id", "coverage": {"src/Calculator.cs": "wA=="}}
                                 }
                                 """;
            var apiResponse = new Mock<IApiResponse>();
            apiResponse.Setup(x => x.StatusCode).Returns(200);
            apiResponse.Setup(x => x.GetStreamAsync()).ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(responseJson)));
            var requestJson = string.Empty;
            var apiRequest = new Mock<IApiRequest>();
            apiRequest.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>()))
                      .Callback<ArraySegment<byte>, string>((body, _) => requestJson = Encoding.UTF8.GetString(body.Array, body.Offset, body.Count))
                      .ReturnsAsync(apiResponse.Object);
            var requestFactory = new Mock<IApiRequestFactory>();
            requestFactory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns<string>(path => new Uri("http://localhost/" + path));
            requestFactory.Setup(x => x.Create(It.IsAny<Uri>())).Returns(apiRequest.Object);
            var tracerManagement = new Mock<ITestOptimizationTracerManagement>();
            tracerManagement.Setup(x => x.EventPlatformProxySupport).Returns(EventPlatformProxySupport.V4);
            tracerManagement.Setup(x => x.GetRequestFactory(It.IsAny<TracerSettings>(), It.IsAny<TimeSpan>())).Returns(requestFactory.Object);
            testOptimization.Setup(x => x.TracerManagement).Returns(tracerManagement.Object);

            var scope = SkippableTestsRequestScope.Create(testOptimization.Object, "Samples.XUnitTests");
            var response = await TestOptimizationClient.Create(workspacePath, testOptimization.Object).GetSkippableTestsAsync(scope);
            var requestConfigurations = JObject.Parse(requestJson)["data"]["attributes"]["configurations"].ToObject<TestsConfigurations>();
            var client = new TestOptimizationClientStub(skippableTestsResponse: response);
            var skippableFeature = TestOptimizationSkippableFeature.Create(settings, CreateRemoteSettingsResponse(testsSkippingEnabled: true), client, testOptimization.Object);
            var candidate = skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests").Should().ContainSingle().Which;

            skippableFeature.CanSkipWithCoverageBackfill(candidate, "Samples.XUnitTests", out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
            if (selectsTargetFramework)
            {
                candidate.Configurations.Should().BeEquivalentTo(requestConfigurations);
            }

            requestConfigurations.TestBundle.Should().Be("Samples.XUnitTests");
            skippableFeature.RecordTestSkipCoverageBackfill(candidate, "Samples.XUnitTests");
            skippableFeature.IsCoverageBackfillSafe().Should().BeTrue();
            CoverageBackfillDataStore.HasActualItrSkip(testOptimization.Object).Should().BeTrue();
        }
        finally
        {
            ClearCoverageBackfillEnvironment();
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void CoverageBackfillSkipGateFailsTargetFrameworkSelectionWhenBackendConfigurationsAreMissing()
    {
        ClearCoverageBackfillEnvironment();
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-skippable-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        try
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --framework net8.0 --collect \"XPlat Code Coverage\"");
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

            var skippableTests = skippableFeature.GetSkippableTestsFromSuiteAndName("Samples.XUnitTests.TestSuite", "SimplePassTest", "Samples.XUnitTests");

            skippableTests.Should().ContainSingle();
            skippableFeature.CanSkipWithCoverageBackfill(skippableTests[0], "Samples.XUnitTests", out var reason).Should().BeFalse();

            reason.Should().Contain("did not include configurations");
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
    public void CommonShouldSkipIgnoresMalformedParameters()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        const string ParametersPrefix = "{\"arguments\":{\"value\":\"";
        var parameters = ParametersPrefix + new string('a', 5_000 - ParametersPrefix.Length - 3) + "...";
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);

        parameters.Should().HaveLength(5_000);
        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [1], method.GetParameters()).Should().BeFalse();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("{\"arguments\":")]
    public void SkippableTestTryGetParametersReturnsFalseWithoutParsedParameters(string rawParameters)
    {
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), typeof(TestOptimizationFeatureTests).FullName!, rawParameters, configurations: null);

        candidate.TryGetParameters(out var parameters).Should().BeFalse();
        parameters.Should().BeNull();
    }

    [Fact]
    public void SkippableTestTryGetParametersReturnsNonNullParametersOnSuccess()
    {
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), typeof(TestOptimizationFeatureTests).FullName!, """{"arguments":{"value":"1"}}""", configurations: null);

        candidate.TryGetParameters(out var parameters).Should().BeTrue();
        parameters.Should().NotBeNull();
    }

    [Fact]
    public void CommonShouldSkipContinuesAfterMalformedParameters()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var malformedCandidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, "{\"arguments\":{\"value\":\"...", configurations: null);
        var validCandidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, """{"arguments":{"value":"1"}}""", configurations: null);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([malformedCandidate, validCandidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [1], method.GetParameters()).Should().BeTrue();
        }
        finally
        {
            TestOptimization.Instance = new TestOptimization();
            TestOptimization.Instance.Reset();
        }
    }

    [Fact]
    public void SmallTestParametersKeepOriginalJson()
    {
        var testParameters = new TestParameters
        {
            Metadata = new Dictionary<string, object>(),
            Arguments = new Dictionary<string, object> { ["value"] = "1" }
        };

        testParameters.ToJSON().Should().Be("""{"metadata":{},"arguments":{"value":"1"}}""");
        testParameters.TryGetFingerprint(out var fingerprint).Should().BeFalse();
        fingerprint.Should().BeNull();
    }

    [Fact]
    public void TestParametersUseFingerprintOnlyAboveMetaValueLimit()
    {
        var testParameters = new TestParameters
        {
            Metadata = new Dictionary<string, object>(),
            Arguments = new Dictionary<string, object> { ["value"] = string.Empty }
        };
        var emptyValueJson = JsonHelper.SerializeObject(testParameters);
        testParameters.Arguments["value"] = new string('a', TruncatorTagsProcessor.MaxMetaValLen - Encoding.UTF8.GetByteCount(emptyValueJson));

        var maximumLengthJson = testParameters.ToJSON();

        Encoding.UTF8.GetByteCount(maximumLengthJson).Should().Be(TruncatorTagsProcessor.MaxMetaValLen);
        maximumLengthJson.Should().NotContain("_dd.parameters_fingerprint");

        testParameters.Arguments["value"] += "a";

        testParameters.ToJSON().Should().Contain("_dd.parameters_fingerprint");
    }

    [Fact]
    public void OversizedTestParametersUseBoundedFingerprintJson()
    {
        var testParameters = new TestParameters
        {
            Metadata = new Dictionary<string, object> { [TestTags.MetadataTestName] = "SampleParameterizedItrTest(value: long)" },
            Arguments = new Dictionary<string, object> { ["value"] = new string('\u00e9', 3_000) }
        };

        var json = testParameters.ToJSON();

        Encoding.UTF8.GetByteCount(json).Should().BeLessThanOrEqualTo(TruncatorTagsProcessor.MaxMetaValLen);
        json.Should().Contain("\"_dd.parameters_format\":\"sha256-v1\"");
        var serializedParameters = JsonHelper.DeserializeObject<TestParameters>(json);
        serializedParameters.Should().NotBeNull();
        serializedParameters.TryGetFingerprint(out var fingerprint).Should().BeTrue();
        fingerprint.Should().NotBeNull();

        var processedJson = json;
        var key = TestTags.Parameters;
        new TruncatorTagsProcessor().ProcessMeta(ref key, ref processedJson);
        processedJson.Should().Be(json);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3_000)]
    public void TestParametersWithInvalidUtf16DoNotThrow(int argumentLength)
    {
        var testParameters = new TestParameters
        {
            Metadata = new Dictionary<string, object>(),
            Arguments = new Dictionary<string, object> { ["value"] = new string('\uD83D', argumentLength) }
        };

        var action = testParameters.ToJSON;

        action.Should().NotThrow();
    }

    [Fact]
    public void CommonShouldSkipMatchesOversizedParametersByFingerprint()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var value = new string('a', 6_000);
        var displayName = $"SampleParameterizedItrTest(value: {value})";
        var testParameters = new TestParameters
        {
            Metadata = new Dictionary<string, object> { [TestTags.MetadataTestName] = displayName },
            Arguments = new Dictionary<string, object> { ["value"] = value }
        };
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, testParameters.ToJSON(), configurations: null);

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleParameterizedItrTest), It.IsAny<string>())).Returns([candidate]);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [value], method.GetParameters(), metadataTestName: displayName).Should().BeTrue();
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [value.Substring(0, value.Length - 1) + "b"], method.GetParameters(), metadataTestName: displayName).Should().BeFalse();
            Common.ShouldSkip(testSuite, nameof(SampleParameterizedItrTest), [value], method.GetParameters(), metadataTestName: displayName + " changed").Should().BeFalse();
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
    public void MsTestShouldSkipMatchesOversizedParametersWithoutEmittedDisplayNameMetadata()
    {
        var skippableFeature = new Mock<ITestOptimizationSkippableFeature>();
        var testOptimization = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory());
        testOptimization.Setup(x => x.SkippableFeature).Returns(skippableFeature.Object);

        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleParameterizedItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var testSuite = typeof(TestOptimizationFeatureTests).FullName!;
        var value = new string('a', 6_000);
        const string DisplayName = "Custom display name";
        var parameters = Common.CreateTestParameters([value], method.GetParameters(), metadataTestName: null, useParameterIndexForUnnamedParameters: true).ToJSON();
        var candidate = new SkippableTest(nameof(SampleParameterizedItrTest), testSuite, parameters, configurations: null);
        var testMethod = new MsTestMethodStub(method, [value], DisplayName);

        parameters.Should().Contain("_dd.parameters_fingerprint");
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
    public void NUnitDependencyChainIsExcludedFromItrSkip()
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var fixture = new NUnitTestStub(method, "fixture", [], testType: "TestFixture");
        var prerequisite = new NUnitTestStub(method, "prerequisite", [], parent: fixture);
        var dependent = new NUnitTestStub(method, "dependent", [], parent: fixture) { DependantTest = prerequisite };
        var independent = new NUnitTestStub(method, "independent", [], parent: fixture);
        fixture.Tests = new[] { prerequisite, dependent, independent };

        NUnitIntegration.HasDependencies(prerequisite).Should().BeTrue();
        NUnitIntegration.HasDependencies(dependent).Should().BeTrue();
        NUnitIntegration.HasDependencies(independent).Should().BeFalse();
    }

    [Fact]
    public void NUnitPerformWorkKeepsPrerequisiteRunningWhenItrSuggestsSkippingIt()
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
        var prerequisite = new NUnitTestStub(method, nameof(SampleXUnitItrTest), [], parent: assemblyTest);
        var dependent = new NUnitTestStub(method, "dependent", [], parent: assemblyTest) { DependantTest = prerequisite };
        assemblyTest.Tests = new[] { prerequisite, dependent };
        var reason = string.Empty;
        var previousModule = TestModule.Current;
        TestModule module = null;

        skippableFeature.Setup(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), ModuleName)).Returns([candidate]);
        skippableFeature.Setup(x => x.IsCoverageBackfillRequired()).Returns(true);
        skippableFeature.Setup(x => x.CanSkipWithCoverageBackfill(candidate, ModuleName, out reason)).Returns(true);
        TestOptimization.Instance = testOptimization.Object;

        try
        {
            module = TestModule.Create(ModuleName, CommonTags.TestingFrameworkNameNUnit, "5.0.0");
            NUnitIntegration.SetTestModuleTo(assemblyTest, module);
            TestModule.Current = null;

            NUnitWorkItemPerformWorkIntegration.OnMethodBegin(new NUnitWorkItemStub(prerequisite));

            prerequisite.RunState.Should().Be(NUnitRunState.Runnable);
            prerequisite.Properties.Get(NUnitIntegration.SkipReasonKey).Should().BeNull();
            skippableFeature.Verify(x => x.GetSkippableTestsFromSuiteAndName(testSuite, nameof(SampleXUnitItrTest), ModuleName), Times.Once);
            skippableFeature.Verify(x => x.RecordTestSkipCoverageBackfill(It.IsAny<SkippableTest>(), It.IsAny<string>()), Times.Never);
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
    [InlineData((int)NUnitTestStatus.Passed, TestTags.StatusPass)]
    [InlineData((int)NUnitTestStatus.Warning, TestTags.StatusPass)]
    [InlineData((int)NUnitTestStatus.Skipped, TestTags.StatusSkip)]
    [InlineData((int)NUnitTestStatus.Inconclusive, TestTags.StatusSkip)]
    public void NUnitFinalResultClearsErrorsFromFailedNativeRetryIterations(int status, string expectedStatus)
    {
        var previousTestOptimization = TestOptimization.Instance;
        TestOptimization.Instance = CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory()).Object;
        TestModule module = null;
        TestSuite suite = null;
        var resultState = new Mock<NUnitResultState>();
        resultState.Setup(x => x.Status).Returns((NUnitTestStatus)status);
        var result = new Mock<NUnitTestResult>();
        result.Setup(x => x.ResultState).Returns(resultState.Object);

        try
        {
            module = TestModule.Create("NUnitRepeat", "NUnit", "5.0.0");
            suite = module.GetOrCreateSuite("RepeatFixture");
            var test = suite.CreateTest("RepeatWithThreshold");
            test.SetErrorInfo(new InvalidOperationException("An intermediate iteration failed"));

            NUnitIntegration.FinishTest(test, result.Object);

            test.GetTags().Status.Should().Be(expectedStatus);
            test.GetInternalSpan().Error.Should().BeFalse();
            test.GetInternalSpan().GetTag(Tags.ErrorType).Should().BeNull();
            test.GetInternalSpan().GetTag(Tags.ErrorMsg).Should().BeNull();
            test.GetInternalSpan().GetTag(Tags.ErrorStack).Should().BeNull();
        }
        finally
        {
            suite?.Close();
            module?.Close();
            TestOptimization.Instance = previousTestOptimization;
        }
    }

    [Fact]
    public void NUnitFixtureDependencyProtectsTestsInBothFixtures()
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var assembly = new NUnitTestStub(method, "assembly", [], testType: "Assembly");
        var prerequisite = new NUnitTestStub(method, "prerequisite", [], testType: "TestFixture", parent: assembly);
        var dependent = new NUnitTestStub(method, "dependent", [], testType: "TestFixture", parent: assembly) { DependantTest = prerequisite };
        assembly.Tests = new[] { prerequisite, dependent };
        var prerequisiteTest = new NUnitTestStub(method, "first", [], parent: prerequisite);
        var dependentTest = new NUnitTestStub(method, "second", [], parent: dependent);
        prerequisite.Tests = new[] { prerequisiteTest };
        dependent.Tests = new[] { dependentTest };

        NUnitIntegration.HasDependencies(prerequisiteTest).Should().BeTrue();
        NUnitIntegration.HasDependencies(dependentTest).Should().BeTrue();
    }

    [Fact]
    public void NUnitParameterizedDependencyProtectsEveryCase()
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var fixture = new NUnitTestStub(method, "fixture", [], testType: "TestFixture");
        var prerequisite = new NUnitTestStub(method, "prerequisite", [], testType: "ParameterizedMethod", parent: fixture);
        var dependent = new NUnitTestStub(method, "dependent", [], parent: fixture) { DependantTest = prerequisite };
        fixture.Tests = new[] { prerequisite, dependent };
        var firstCase = new NUnitTestStub(method, "first case", [], parent: prerequisite);
        var secondCase = new NUnitTestStub(method, "second case", [], parent: prerequisite);
        prerequisite.Tests = new[] { firstCase, secondCase };

        NUnitIntegration.HasDependencies(firstCase).Should().BeTrue();
        NUnitIntegration.HasDependencies(secondCase).Should().BeTrue();
    }

    [Theory]
    [InlineData((int)NUnitTestStatus.Passed, false)]
    [InlineData((int)NUnitTestStatus.Warning, false)]
    [InlineData((int)NUnitTestStatus.Skipped, false)]
    [InlineData((int)NUnitTestStatus.Inconclusive, false)]
    [InlineData((int)NUnitTestStatus.Failed, true)]
    public void NUnitDependencyChainsOnlyHideFailuresFromNUnit(int status, bool expected)
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var assembly = new NUnitTestStub(method, "assembly", [], testType: "Assembly");
        var prerequisiteFixture = new NUnitTestStub(method, "prerequisite fixture", [], testType: "TestFixture", parent: assembly);
        var dependentFixture = new NUnitTestStub(method, "dependent fixture", [], testType: "TestFixture", parent: assembly) { DependantTest = prerequisiteFixture };
        assembly.Tests = new[] { prerequisiteFixture, dependentFixture };
        var fixtureTest = new NUnitTestStub(method, "fixture test", [], parent: prerequisiteFixture);
        prerequisiteFixture.Tests = new[] { fixtureTest };
        var prerequisite = new NUnitTestStub(method, "prerequisite", [], parent: dependentFixture);
        var dependent = new NUnitTestStub(method, "dependent", [], parent: dependentFixture) { DependantTest = prerequisite };
        dependentFixture.Tests = new[] { prerequisite, dependent };

        var fixtureTestResult = CreateNUnitResult(fixtureTest, (NUnitTestStatus)status);
        var prerequisiteResult = CreateNUnitResult(prerequisite, (NUnitTestStatus)status);
        var dependentResult = CreateNUnitResult(dependent, (NUnitTestStatus)status);

        NUnitIntegration.TryHideResult(fixtureTestResult.Object, "hidden").Should().Be(expected);
        NUnitIntegration.TryHideResult(prerequisiteResult.Object, "hidden").Should().Be(expected);
        NUnitIntegration.TryHideResult(dependentResult.Object, "hidden").Should().BeTrue();

        var hiddenTimes = expected ? Times.Once() : Times.Never();
        fixtureTestResult.Verify(x => x.SetResult(It.IsAny<NUnitResultState>(), "hidden", string.Empty), hiddenTimes);
        prerequisiteResult.Verify(x => x.SetResult(It.IsAny<NUnitResultState>(), "hidden", string.Empty), hiddenTimes);
        dependentResult.Verify(x => x.SetResult(It.IsAny<NUnitResultState>(), "hidden", string.Empty), Times.Once);
    }

    [Theory]
    [InlineData((int)NUnitTestStatus.Passed, true)]
    [InlineData((int)NUnitTestStatus.Warning, true)]
    [InlineData((int)NUnitTestStatus.Failed, false)]
    [InlineData((int)NUnitTestStatus.Skipped, false)]
    public void NUnitHiddenFailureStillBlocksDependentsOfPrerequisiteSuite(int suiteStatus, bool expectedBlocked)
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var assembly = new NUnitTestStub(method, "assembly", [], testType: "Assembly");
        var prerequisiteFixture = new NUnitTestStub(method, "prerequisite fixture", [], testType: "TestFixture", parent: assembly);
        var dependentFixture = new NUnitTestStub(method, "dependent fixture", [], testType: "TestFixture", parent: assembly) { DependantTest = prerequisiteFixture };
        assembly.Tests = new[] { prerequisiteFixture, dependentFixture };
        var hiddenFailure = new NUnitTestStub(method, "hidden failure", [], parent: prerequisiteFixture);
        prerequisiteFixture.Tests = new[] { hiddenFailure };
        var dependentFailure = new NUnitTestStub(method, "dependent failure", [], parent: dependentFixture);
        dependentFixture.Tests = new[] { dependentFailure };
        var prerequisiteSuiteResult = CreateNUnitResult(prerequisiteFixture, (NUnitTestStatus)suiteStatus);
        var dependentSuiteResult = CreateNUnitResult(dependentFixture, (NUnitTestStatus)suiteStatus);
        var ignoredState = prerequisiteSuiteResult.Object.ResultState.StaticIgnored;

        NUnitIntegration.TryHideResult(CreateNUnitResult(hiddenFailure, NUnitTestStatus.Failed).Object, "hidden").Should().BeTrue();
        NUnitIntegration.TryHideResult(CreateNUnitResult(dependentFailure, NUnitTestStatus.Failed).Object, "hidden").Should().BeTrue();
        NUnitIntegration.BlockDependentsOfHiddenFailures(prerequisiteFixture, prerequisiteSuiteResult.Object);
        NUnitIntegration.BlockDependentsOfHiddenFailures(dependentFixture, dependentSuiteResult.Object);

        prerequisiteSuiteResult.Verify(x => x.SetResult(ignoredState, It.IsAny<string>(), string.Empty), expectedBlocked ? Times.Once() : Times.Never());
        dependentSuiteResult.Verify(x => x.SetResult(It.IsAny<NUnitResultState>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NUnitAttemptToFixDoesNotHidePassingPrerequisiteResult(bool quarantined, bool disabled)
    {
        var method = typeof(TestOptimizationFeatureTests).GetMethod(nameof(SampleXUnitItrTest), BindingFlags.NonPublic | BindingFlags.Static)!;
        var fixture = new NUnitTestStub(method, "fixture", [], testType: "TestFixture");
        var prerequisite = new NUnitTestStub(method, "prerequisite", [], parent: fixture);
        var dependent = new NUnitTestStub(method, "dependent", [], parent: fixture) { DependantTest = prerequisite };
        fixture.Tests = new[] { prerequisite, dependent };
        var properties = new TestOptimizationClient.TestManagementResponseTestPropertiesAttributes(quarantined, disabled, attemptToFix: true);
        var behavior = new NUnitAttemptToFixRetryBehavior(CreateTestOptimization(CreateSettings(), Directory.GetCurrentDirectory()).Object, properties);
        var prerequisiteResult = CreateNUnitResult(prerequisite, NUnitTestStatus.Passed);
        var dependentResult = CreateNUnitResult(dependent, NUnitTestStatus.Passed);
        var ignoredState = dependentResult.Object.ResultState.StaticIgnored;

        behavior.ResultChanges(prerequisiteResult.Object);
        behavior.ResultChanges(dependentResult.Object);

        prerequisiteResult.Verify(x => x.SetResult(It.IsAny<NUnitResultState>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        dependentResult.Verify(x => x.SetResult(ignoredState, It.IsAny<string>(), string.Empty), Times.Once);
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
    public void SharedXUnitQuarantinedOrDisabledRetryRunSummaryIsHidden()
    {
        var runSummary = new XUnitRunSummary
        {
            Total = 3,
            Failed = 2,
            Skipped = 0,
            NotRun = 1
        };

        runSummary.HideQuarantinedOrDisabledResult();

        runSummary.Total.Should().Be(1);
        runSummary.Failed.Should().Be(0);
        runSummary.Skipped.Should().Be(0);
        runSummary.NotRun.Should().Be(0);
    }

    [Fact]
    public void SharedXUnitQuarantinedOrDisabledFinalRunSummaryIsReportedAsSkipped()
    {
        var runSummary = new XUnitRunSummary
        {
            Total = 3,
            Failed = 2,
            Skipped = 0,
            NotRun = 1
        };

        runSummary.ReportQuarantinedOrDisabledResultAsSkipped();

        runSummary.Total.Should().Be(1);
        runSummary.Failed.Should().Be(0);
        runSummary.Skipped.Should().Be(1);
        runSummary.NotRun.Should().Be(0);
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

    [Theory]
    [InlineData("true", null, "3,2,1,1,1", new[] { 7, 7, 7, 7, 7 }, null)]
    [InlineData("false", "true", "3,2,1,1,1", new[] { 7, 7, 7, 7, 7 }, null)]
    [InlineData("true", "true", null, new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "1", " 3, 2,1,1 ,20", new[] { 3, 2, 1, 1, 20 }, true)]
    [InlineData("true", "true", "", new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "true", "3,2,1,1", new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "true", "3,2,1,1,1,1", new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "true", "3,2,a,1,1", new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "true", "3,2,0,1,1", new[] { 4, 5, 2, 1, 1 }, false)]
    [InlineData("true", "true", "3,2,21,1,1", new[] { 4, 5, 2, 1, 1 }, false)]
    public async Task FlakyRetryCountDependsOnFirstExecutionDurationWhenDynamicRetriesAreEnabled(string flakyRetryEnabled, string dynamicFlakyRetryEnabled, string dynamicFlakyRetryBuckets, int[] expectedRetryCounts, bool? expectedCustomBuckets)
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, flakyRetryEnabled),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "7"),
            (ConfigurationKeys.CIVisibility.DynamicFlakyRetryEnabled, dynamicFlakyRetryEnabled),
            (ConfigurationKeys.CIVisibility.DynamicFlakyRetryBuckets, dynamicFlakyRetryBuckets));
        var remoteSettings = new TestOptimizationClient.SettingsResponse(
            codeCoverage: false,
            testsSkipping: false,
            requireGit: false,
            impactedTestsEnabled: false,
            flakyTestRetries: true,
            earlyFlakeDetection: new TestOptimizationClient.EarlyFlakeDetectionSettingsResponse(
                enabled: false,
                slowTestRetries: new TestOptimizationClient.SlowTestRetriesSettingsResponse(fiveSeconds: 4, tenSeconds: null, thirtySeconds: 2, fiveMinutes: 0),
                faultySessionThreshold: 0),
            knownTestsEnabled: false,
            testManagement: default,
            dynamicInstrumentationEnabled: false);

        var collector = new CiVisibilityMetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);
        try
        {
            var flakyRetryFeature = TestOptimizationFlakyRetryFeature.Create(settings, remoteSettings);

            // The slowest first execution of each bucket: up to 5s, 10s, 30s, 5m, and slower. The upper limits are inclusive.
            TimeSpan[] firstExecutionDurations = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1)];
            firstExecutionDurations.Select(flakyRetryFeature.GetFlakyRetryCount).Should().Equal(expectedRetryCounts);

            collector.AggregateMetrics();
            var metrics = (collector.GetMetrics().Metrics ?? []).Where(metric => metric.Metric == "dynamic_atr_retries.enabled").ToList();
            if (expectedCustomBuckets is { } customBuckets)
            {
                metrics.Should().ContainSingle().Which.Tags.Should().BeEquivalentTo(customBuckets ? ["has_custom_buckets:true"] : null);
            }
            else
            {
                metrics.Should().BeEmpty();
            }
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
            await collector.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("4,1,1,1,1", 4)]
    public void DynamicFlakyRetriesUseDefaultCountsWhenSettingsHaveNoSlowTestRetries(string dynamicFlakyRetryBuckets, int expectedRetryCount)
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "7"),
            (ConfigurationKeys.CIVisibility.DynamicFlakyRetryEnabled, "true"),
            (ConfigurationKeys.CIVisibility.DynamicFlakyRetryBuckets, dynamicFlakyRetryBuckets));

        // The same local response that child processes of `dd-trace ci run` use when they skip the settings request.
        var localSettings = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);

        TestOptimizationFlakyRetryFeature.Create(settings, localSettings).GetFlakyRetryCount(TimeSpan.FromSeconds(1)).Should().Be(expectedRetryCount);
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

    private static TestsConfigurations CreateBackendConfigurations(string runtimeVersion)
        => new(
            osPlatform: "linux",
            osVersion: "test-os",
            osArchitecture: "x64",
            runtimeName: ".NET",
            runtimeVersion: runtimeVersion,
            runtimeArchitecture: "x64",
            custom: null,
            testBundle: "Samples.XUnitTests");

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

    private static Mock<NUnitTestResult> CreateNUnitResult(NUnitTest test, NUnitTestStatus status)
    {
        var ignoredState = new Mock<NUnitResultState>();
        ignoredState.Setup(x => x.Status).Returns(NUnitTestStatus.Skipped);
        var resultState = new Mock<NUnitResultState>();
        resultState.Setup(x => x.Status).Returns(status);
        resultState.Setup(x => x.StaticIgnored).Returns(ignoredState.Object);
        var result = new Mock<NUnitTestResult>();
        result.Setup(x => x.Test).Returns(test);
        result.Setup(x => x.ResultState).Returns(resultState.Object);
        return result;
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

        public bool IsSuite => _testType != "TestMethod";

        public bool HasChildren => false;

        public IList Tests { get; set; }

        public NUnitTest DependantTest { get; set; }

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
