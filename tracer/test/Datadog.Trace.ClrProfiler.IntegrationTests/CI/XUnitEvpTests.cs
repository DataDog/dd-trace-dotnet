// <copyright file="XUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[UsesVerify]
public abstract class XUnitEvpTests : TestingFrameworkEvpTest
{
    private const string TestBundleName = "Samples.XUnitTests";
    private const string TestSuiteName = "Samples.XUnitTests.TestSuite";
    private const string UnSkippableSuiteName = "Samples.XUnitTests.UnSkippableSuite";
    private const int ExpectedTestCount = 16;

    /// <summary>
    /// Repository-relative source file path used by the XUnit sample and backend coverage payload.
    /// </summary>
    private const string XUnitSampleSourcePath = "tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs";

    /// <summary>
    /// MSB-first backend coverage bitmap for line 23 in the XUnit sample source file.
    /// </summary>
    private const string SimplePassTestLineCoverageBitmap = "AAAC";

    /// <summary>
    /// Number of covered lines encoded by <see cref="SimplePassTestLineCoverageBitmap"/>.
    /// </summary>
    private const int SimplePassTestBackfilledLineCount = 1;

    private const string CoverageBackfillMatrixTestArguments = "--collect:\"XPlat Code Coverage;IncludeTestAssembly=true\"";

    private const string MissingLineCoverageBlocksSkip = nameof(MissingLineCoverageBlocksSkip);
    private const string MissingBackendCoverageStillSkips = nameof(MissingBackendCoverageStillSkips);
    private const string EmptyBackendConfigurationsStillSkip = nameof(EmptyBackendConfigurationsStillSkip);
    private const string DivergentBackendConfigurationsBlockSkip = nameof(DivergentBackendConfigurationsBlockSkip);
    private const string SafeAndMissingLineCandidates = nameof(SafeAndMissingLineCandidates);
    private const string ParameterizedCandidateDoesNotSkip = nameof(ParameterizedCandidateDoesNotSkip);
    private const string BackendCoverageDoesNotMatchLocalReport = nameof(BackendCoverageDoesNotMatchLocalReport);
    private const string NoSkippableResponse = nameof(NoSkippableResponse);

    public XUnitEvpTests(ITestOutputHelper output)
        : base("XUnitTests", output)
    {
        SetServiceName("xunit-tests-evp");
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetData()
    {
        foreach (var version in PackageVersions.XUnit)
        {
            // EVP version to remove, expects gzip
            yield return version.Concat("evp_proxy/v2", true);
            yield return version.Concat("evp_proxy/v4", false);
        }
    }

    /// <summary>
    /// Provides a single TCP EVP row for the coverage-backfill integration smoke test.
    /// </summary>
    /// <returns>Package version, EVP endpoint version to remove, and expected compression.</returns>
    public static IEnumerable<object[]> GetDataForCoverageBackfill()
    {
        // Use the newest XUnit row so this focused smoke test exercises the current Coverlet collector and test-platform path.
        yield return PackageVersions.XUnit.Last().Concat("evp_proxy/v4", false);
    }

    /// <summary>
    /// Provides focused rows that run the real XUnit sample against skippable-test responses matching Java reference behavior.
    /// </summary>
    /// <returns>Package version, EVP endpoint version to remove, expected compression, and scenario.</returns>
    public static IEnumerable<object[]> GetDataForCoverageBackfillMatrix()
    {
        var row = PackageVersions.XUnit.Last().Concat("evp_proxy/v4", false);
        yield return row.Concat(MissingLineCoverageBlocksSkip);
        yield return row.Concat(MissingBackendCoverageStillSkips);
        yield return row.Concat(EmptyBackendConfigurationsStillSkip);
        yield return row.Concat(DivergentBackendConfigurationsBlockSkip);
        yield return row.Concat(SafeAndMissingLineCandidates);
        yield return row.Concat(ParameterizedCandidateDoesNotSkip);
        yield return row.Concat(BackendCoverageDoesNotMatchLocalReport);
        yield return row.Concat(NoSkippableResponse);
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "readability")]
    public static IEnumerable<object[]> GetDataForEarlyFlakeDetection()
    {
        foreach (var row in GetData())
        {
            var paginatedKnownTestsFirstPage =
                """
                {
                    "data":{
                        "id":"lNemDTwOV8U",
                        "type":"ci_app_libraries_tests",
                        "attributes":{
                            "tests":{},
                            "page_info":{
                                "cursor":"page-2-cursor",
                                "size":0,
                                "has_next":true
                            }
                        }
                    }
                }
                """;

            // settings json, efd tests json, expected spans, friendlyName

            // EFD for all tests
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    """
                    {
                        "data":{
                            "id":"lNemDTwOV8U",
                            "type":"ci_app_libraries_tests",
                            "attributes":{
                                "tests":{}
                            }
                        }
                    }
                    """,
                    string.Empty),
                1,
                124,
                "all_efd");

            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0", "true"),
                    """
                    {
                        "data":{
                            "id":"lNemDTwOV8U",
                            "type":"ci_app_libraries_tests",
                            "attributes":{
                                "tests":{}
                            }
                        }
                    }
                    """,
                    string.Empty),
                1,
                124,
                "all_efd_with_atr");

            // EFD with 1 test to bypass (TraitPassTest)
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    """
                    {
                        "data":{
                            "id":"lNemDTwOV8U",
                            "type":"ci_app_libraries_tests",
                            "attributes":{
                                "tests":{
                                    "Samples.XUnitTests":{
                                        "Samples.XUnitTests.TestSuite":["TraitPassTest"]
                                    }
                                }
                            }
                        }
                    }
                    """,
                    string.Empty),
                1,
                115,
                "efd_with_test_bypass");

            // EFD with paginated known tests (TraitPassTest arrives on page 2)
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    [
                        paginatedKnownTestsFirstPage,
                        """
                        {
                            "data":{
                                "id":"lNemDTwOV8U",
                                "type":"ci_app_libraries_tests",
                                "attributes":{
                                    "tests":{
                                        "Samples.XUnitTests":{
                                            "Samples.XUnitTests.TestSuite":["TraitPassTest"]
                                        }
                                    },
                                    "page_info":{
                                        "cursor":"",
                                        "size":1,
                                        "has_next":false
                                    }
                                }
                            }
                        }
                        """
                    ],
                    string.Empty),
                1,
                115,
                "efd_with_test_bypass_paginated");

            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    [
                        paginatedKnownTestsFirstPage
                    ],
                    string.Empty),
                1,
                ExpectedTestCount,
                "efd_with_test_bypass_paginated_missing_followup_page");

            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    [
                        paginatedKnownTestsFirstPage,
                        """
                        {
                            "data":{
                                "id":"lNemDTwOV8U",
                                "type":"ci_app_libraries_tests"
                            }
                        }
                        """
                    ],
                    string.Empty),
                1,
                ExpectedTestCount,
                "efd_with_test_bypass_paginated_invalid_followup_payload");

            yield return row.Concat(
                new MockData(
                    GetSettingsJson("true", "true", "false", "0"),
                    [
                        paginatedKnownTestsFirstPage,
                        """
                        {
                            "data":{
                                "id":"lNemDTwOV8U",
                                "type":"ci_app_libraries_tests",
                                "attributes":{
                                    "tests":{
                                        "Samples.XUnitTests":{
                                            "Samples.XUnitTests.TestSuite":["TraitPassTest"]
                                        }
                                    }
                                }
                            }
                        }
                        """
                    ],
                    string.Empty),
                1,
                ExpectedTestCount,
                "efd_with_test_bypass_paginated_missing_followup_page_info");
        }
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "readability")]
    public static IEnumerable<object[]> GetDataForQuarantinedTests()
    {
        foreach (var row in GetData())
        {
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("false", "false", "true", "0"),
                    string.Empty,
                    """
                    {
                        "data": {
                            "id": "878448902e138d339eb9f26a778851f35582b5ea3622ae8ab446209d232399af",
                            "type": "ci_app_libraries_tests",
                            "attributes": {
                                "modules": {
                                    "Samples.XUnitTests": {
                                        "suites": {
                                            "Samples.XUnitTests.TestSuite": {
                                                "tests": {
                                                    "TraitErrorTest": {
                                                        "properties": {
                                                            "quarantined": true
                                                        }
                                                    },
                                                    "SimpleErrorTest": {
                                                        "properties": {
                                                            "quarantined": true
                                                        }
                                                    },
                                                    "SimpleErrorParameterizedTest": {
                                                        "properties": {
                                                            "quarantined": true
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    """),
                0,
                16,
                "quarantined_tests");
        }
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "readability")]
    public static IEnumerable<object[]> GetDataForDisabledTests()
    {
        foreach (var row in GetData())
        {
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("false", "false", "true", "0"),
                    string.Empty,
                    """
                    {
                        "data": {
                            "id": "878448902e138d339eb9f26a778851f35582b5ea3622ae8ab446209d232399af",
                            "type": "ci_app_libraries_tests",
                            "attributes": {
                                "modules": {
                                    "Samples.XUnitTests": {
                                        "suites": {
                                            "Samples.XUnitTests.TestSuite": {
                                                "tests": {
                                                    "TraitErrorTest": {
                                                        "properties": {
                                                            "disabled": true
                                                        }
                                                    },
                                                    "SimpleErrorTest": {
                                                        "properties": {
                                                            "disabled": true
                                                        }
                                                    },
                                                    "SimpleErrorParameterizedTest": {
                                                        "properties": {
                                                            "disabled": false
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    """),
                1,
                16,
                "disabled_tests");
        }
    }

    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "readability")]
    public static IEnumerable<object[]> GetDataForAttemptToFixTests()
    {
        foreach (var row in GetData())
        {
            yield return row.Concat(
                new MockData(
                    GetSettingsJson("false", "false", "true", "10"),
                    string.Empty,
                    """
                    {
                        "data": {
                            "id": "878448902e138d339eb9f26a778851f35582b5ea3622ae8ab446209d232399af",
                            "type": "ci_app_libraries_tests",
                            "attributes": {
                                "modules": {
                                    "Samples.XUnitTests": {
                                        "suites": {
                                            "Samples.XUnitTests.TestSuite": {
                                                "tests": {
                                                    "TraitErrorTest": {
                                                        "properties": {
                                                            "quarantined": true,
                                                            "attempt_to_fix": true
                                                        }
                                                    },
                                                    "SimpleErrorTest": {
                                                        "properties": {
                                                            "disabled": true,
                                                            "attempt_to_fix": true
                                                        }
                                                    },
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    """),
                1,
                34,
                "quarantined_and_disabled");
        }
    }

    public virtual async Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        var tests = new List<MockCIVisibilityTest>();
        var testsCopy = new List<MockCIVisibilityTest>();
        var testSuites = new List<MockCIVisibilityTestSuite>();
        var testModules = new List<MockCIVisibilityTestModule>();

        // Inject session
        InjectSession(
            out var sessionId,
            out var sessionCommand,
            out var sessionWorkingDirectory,
            out var gitRepositoryUrl,
            out var gitBranch,
            out var gitCommitSha,
            out var runId);

        Output.WriteLine("RunId: {0}", runId);
        var codeCoverageReceived = new StrongBox<bool>(false);
        var name = $"session_{sessionId}";
        using var ipcServer = new IpcServer(name);
        ipcServer.SetMessageReceivedCallback(
            o =>
            {
                codeCoverageReceived.Value = codeCoverageReceived.Value || o is SessionCodeCoverageMessage or SessionCodeCoverageReferenceMessage;
            });

        string[] messages = null;

        using var logsIntake = new MockLogsIntakeForCiVisibility();
        EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.XUnit), nameof(XUnitTests));

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true, useStatsD: !IsMacOS());
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(evpVersionToRemove)).ToArray();

        const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse("""{"data":{"id":"b5a855bffe6c0b2ae5d150fb6ad674363464c816","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}} """, 200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                e.Value.Response = new MockTracerResponse($"{{\"data\":[],\"meta\":{{\"correlation_id\":\"{correlationId}\"}}}}", 200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                e.Value.Headers["Content-Encoding"].Should().Be(expectedGzip ? "gzip" : null);

                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                ValidateMetadata(payload.Metadata, sessionCommand);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Content.ToString() is { } eventContent)
                        {
                            if (@event.Type == SpanTypes.Test)
                            {
                                tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                                testsCopy.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                            }
                            else if (@event.Type == SpanTypes.TestSuite)
                            {
                                testSuites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(eventContent));
                            }
                            else if (@event.Type == SpanTypes.TestModule)
                            {
                                testModules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(eventContent));
                            }
                        }
                    }
                }
            }
        };

        using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                      agent,
                                      arguments: "--collect:\"XPlat Code Coverage\"",
                                      packageVersion: packageVersion,
                                      expectedExitCode: 1);

        // Check the tests, suites and modules count
        Assert.Equal(ExpectedTestCount, tests.Count);
        Assert.Equal(2, testSuites.Count);
        Assert.Single(testModules);

        var testSuite = testSuites.First(suite => suite.Resource == TestSuiteName);
        var unskippableTestSuite = testSuites.First(suite => suite.Resource == UnSkippableSuiteName);
        var testModule = testModules[0];

        // Check Suite
        Assert.True(tests.All(t => t.TestSuiteId == testSuite.TestSuiteId || t.TestSuiteId == unskippableTestSuite.TestSuiteId));
        testSuite.TestModuleId.Should().Be(testModule.TestModuleId);
        testSuite.Meta.Should().ContainKey(TestTags.SourceFile);
        testSuite.Meta.Should().ContainKey(TestTags.CodeOwners);
        unskippableTestSuite.TestModuleId.Should().Be(testModule.TestModuleId);

        // ITR tags inside the test suite
        testSuite.Metrics.Should().Contain(IntelligentTestRunnerTags.SkippingCount, 1);
        testSuites.Should().AllSatisfy(suite => suite.Meta.Should().Contain(IntelligentTestRunnerTags.TestTestsSkippingEnabled, "true"));

        // Check Module
        Assert.True(tests.All(t => t.TestModuleId == testSuite.TestModuleId));

        // ITR tags inside the test module
        testModule.Metrics.Should().Contain(IntelligentTestRunnerTags.SkippingCount, 1);
        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.SkippingType, IntelligentTestRunnerTags.SkippingTypeTest);
        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.TestsSkipped, "true");
        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.TestTestsSkippingEnabled, "true");
        tests.Should().AllSatisfy(test => test.Meta.Should().Contain(IntelligentTestRunnerTags.TestTestsSkippingEnabled, "true"));

        // Check Session
        tests.Should().OnlyContain(t => t.TestSessionId == testSuite.TestSessionId);
        testSuite.TestSessionId.Should().Be(testModule.TestSessionId);
        unskippableTestSuite.TestSessionId.Should().Be(testModule.TestSessionId);
        testModule.TestSessionId.Should().Be(sessionId);

        // ***************************************************************************
        try
        {
            foreach (var targetTest in tests)
            {
                // Remove decision maker tag (not used by the backend for civisibility)
                targetTest.Meta.Remove(Tags.Propagated.DecisionMaker);

                // Remove EFD tags
                targetTest.Meta.Remove(TestTags.TestIsNew);
                targetTest.Meta.Remove(TestTags.TestIsRetry);

                // Remove test final status
                targetTest.Meta.Remove(TestTags.TestFinalStatus);

                // Remove capabilities
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesAutoTestRetries);
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementQuarantine);
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesEarlyFlakeDetection);
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestImpactAnalysis);
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementDisable);
                targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementAttemptToFix);

                // Remove user provided service tag
                targetTest.Meta.Remove(CommonTags.UserProvidedTestServiceTag);

                // Remove tags validated outside the per-span checklist
                Assert.True(targetTest.Meta.Remove(IntelligentTestRunnerTags.TestTestsSkippingEnabled));

                // check the name
                Assert.Equal("xunit.test", targetTest.Name);

                // check correlationId
                Assert.Equal(correlationId, targetTest.CorrelationId);

                // check the CIEnvironmentValues decoration.
                CheckCIEnvironmentValuesDecoration(targetTest, gitRepositoryUrl, gitBranch, gitCommitSha);

                // check the runtime values
                CheckRuntimeValues(targetTest);

                // check the bundle name
                AssertTargetSpanEqual(targetTest, TestTags.Bundle, TestBundleName);
                AssertTargetSpanEqual(targetTest, TestTags.Module, TestBundleName);

                // check the suite name
                AssertTargetSpanAnyOf(targetTest, TestTags.Suite, TestSuiteName, UnSkippableSuiteName);

                // check the test type
                AssertTargetSpanEqual(targetTest, TestTags.Type, TestTags.TypeTest);

                // check the test framework
                AssertTargetSpanContains(targetTest, TestTags.Framework, "xUnit");
                Assert.True(targetTest.Meta.Remove(TestTags.FrameworkVersion));

                // check the version
                AssertTargetSpanEqual(targetTest, "version", "1.0.0");

                // checks the origin tag
                CheckOriginTag(targetTest);

                // checks the source tags
                AssertTargetSpanExists(targetTest, TestTags.SourceFile);

                // checks code owners
                AssertTargetSpanExists(targetTest, TestTags.CodeOwners);

                // Check the Environment
                AssertTargetSpanEqual(targetTest, Tags.Env, "integration_tests");

                // Language
                AssertTargetSpanEqual(targetTest, Tags.Language, TracerConstants.Language);

                // CI Library Language
                AssertTargetSpanEqual(targetTest, CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);

                // Check Session data
                AssertTargetSpanEqual(targetTest, TestTags.Command, sessionCommand);
                AssertTargetSpanEqual(targetTest, TestTags.CommandWorkingDirectory, sessionWorkingDirectory);

                // Unskippable data
                if (targetTest.Meta[TestTags.Name] != "UnskippableTest")
                {
                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.UnskippableTag, "false");
                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.ForcedRunTag, "false");
                }

                // check specific test span
                switch (targetTest.Meta[TestTags.Name])
                {
                    case "SimplePassTest":
                        CheckSimpleTestSpan(targetTest);
                        break;

                    case "SimpleSkipFromAttributeTest":
                        CheckSimpleSkipFromAttributeTest(targetTest);
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                        break;

                    case "SkipByITRSimulation":
                        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);
                        AssertTargetSpanEqual(targetTest, TestTags.SkipReason, IntelligentTestRunnerTags.SkippedByReason);
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "true");
                        break;

                    case "SimpleErrorTest":
                        CheckSimpleErrorTest(targetTest);
                        break;

                    case "TraitPassTest":
                        CheckSimpleTestSpan(targetTest);
                        CheckTraitsValues(targetTest);
                        break;

                    case "TraitSkipFromAttributeTest":
                        CheckSimpleSkipFromAttributeTest(targetTest);
                        CheckTraitsValues(targetTest);
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                        break;

                    case "TraitErrorTest":
                        CheckSimpleErrorTest(targetTest);
                        CheckTraitsValues(targetTest);
                        break;

                    case "SimpleParameterizedTest":
                        CheckSimpleTestSpan(targetTest);
                        AssertTargetSpanAnyOf(
                            targetTest,
                            TestTags.Parameters,
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 1, yValue: 1, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 2, yValue: 2, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 3, yValue: 3, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 1, yValue: 1, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 2, yValue: 2, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 3, yValue: 3, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                        break;

                    case "SimpleSkipParameterizedTest":
                        CheckSimpleSkipFromAttributeTest(targetTest);
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                        break;

                    case "SimpleErrorParameterizedTest":
                        CheckSimpleErrorTest(targetTest);
                        AssertTargetSpanAnyOf(
                            targetTest,
                            TestTags.Parameters,
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 1, yValue: 0, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 2, yValue: 0, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                            "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 3, yValue: 0, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 1, yValue: 0, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 2, yValue: 0, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                            "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 3, yValue: 0, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                        break;

                    case "UnskippableTest":
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.UnskippableTag, "true");
                        AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.ForcedRunTag, "false");
                        CheckSimpleTestSpan(targetTest);
                        break;
                }

                // check remaining tag (only the name)
                Assert.Single(targetTest.Meta);

                // check if we received code coverage information at session level
                codeCoverageReceived.Value.Should().BeTrue();
            }
        }
        catch
        {
            WriteSpans(tests);
            throw;
        }

        // Snapshots
        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
        settings.UseTextForParameters("packageVersion=all");
        settings.DisableRequireUniquePrefix();
        settings.UseTypeName(nameof(XUnitEvpTests));
        await Verifier.Verify(testsCopy.OrderBy(s => s.Resource).ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters)), settings);

        // ***************************************************************************
        // Check logs
        messages = logsIntake.Logs.Select(i => i.Message).Where(m => m.StartsWith("Test:")).ToArray();

        Assert.Contains(messages, m => m.StartsWith("Test:SimplePassTest"));
        Assert.Contains(messages, m => m.StartsWith("Test:SimpleErrorTest"));
        Assert.Contains(messages, m => m.StartsWith("Test:TraitPassTest"));
        Assert.Contains(messages, m => m.StartsWith("Test:TraitErrorTest"));
        Assert.Contains(messages, m => m.StartsWith("Test:SimpleParameterizedTest"));
        Assert.Contains(messages, m => m.StartsWith("Test:SimpleErrorParameterizedTest"));

        // Smoke check telemetry
        await agent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));
        var allData = agent.Telemetry.Cast<TelemetryData>().ToArray();

        // we will have multiple app closing events
        TelemetryHelper.GetMetricData(allData, "endpoint_payload.requests", "endpoint:test_cycle", singleAppClosing: false)
                       .Should()
                       .NotBeEmpty()
                       .And.OnlyContain(x => HasCorrectCompressionTag(x.Tags, expectedGzip));
    }

    /// <summary>
    /// Runs the real XUnit sample through ITR and verifies backend coverage is applied to Coverlet IPC coverage.
    /// </summary>
    /// <param name="packageVersion">XUnit package version under test.</param>
    /// <param name="evpVersionToRemove">EVP endpoint version removed from the mock agent to force the target path.</param>
    /// <param name="expectedGzip">Whether the target EVP path should use gzip.</param>
    public virtual async Task ItrCoverageBackfillSendsBackfilledCoverletCoverage(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        Skip.If(
            EnvironmentTools.IsLinux(),
            "Coverlet collector writes a Cobertura attachment on Linux under auto instrumentation but does not invoke the in-process callback validated by this IPC smoke test.");

        var tests = new List<MockCIVisibilityTest>();
        var coverageResults = new List<CodeCoverageAggregationResult>();
        var unresolvedCoverageReferences = new List<string>();
        var evpRequests = new List<string>();
        var skippableRequestBodies = new List<string>();

        InjectSession(
            out var sessionId,
            out _,
            out _,
            out _,
            out _,
            out _,
            out var runId);

        const string sessionCommand = "dotnet test --collect:\"XPlat Code Coverage;IncludeTestAssembly=true\"";
        Output.WriteLine("RunId: {0}", runId);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, sessionCommand);

        var ipcServerName = $"session_{sessionId}";
        using var ipcServer = new IpcServer(ipcServerName);
        ipcServer.SetMessageReceivedCallback(
            message =>
            {
                if (TryResolveCoverageIpcMessage(sessionId, message, out var coverageResult, out var unresolvedReference))
                {
                    lock (coverageResults)
                    {
                        coverageResults.Add(coverageResult);
                    }
                }
                else if (unresolvedReference is not null)
                {
                    lock (unresolvedCoverageReferences)
                    {
                        unresolvedCoverageReferences.Add(unresolvedReference);
                    }
                }
            });

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true, useStatsD: !IsMacOS());
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(evpVersionToRemove)).ToArray();

        const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            lock (evpRequests)
            {
                evpRequests.Add($"{e.Value.PathAndQuery} ({e.Value.Headers["Content-Type"] ?? "unknown"})");
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse("""{"data":{"id":"b5a855bffe6c0b2ae5d150fb6ad674363464c816","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}} """, 200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                lock (skippableRequestBodies)
                {
                    skippableRequestBodies.Add(e.Value.BodyInJson);
                }

                e.Value.Response = new MockTracerResponse(
                    $$"""
                      {
                        "data": [
                          {
                            "id": "Samples.XUnitTests.TestSuite.SimplePassTest",
                            "type": "test_params",
                            "attributes": {
                              "suite": "{{TestSuiteName}}",
                              "name": "SimplePassTest",
                              "_missing_line_code_coverage": false
                            }
                          }
                        ],
                        "meta": {
                          "correlation_id": "{{correlationId}}",
                          "coverage": {
                            "{{XUnitSampleSourcePath}}": "{{SimplePassTestLineCoverageBitmap}}"
                          }
                        }
                      }
                      """,
                    200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Content.ToString() is not { } eventContent)
                        {
                            continue;
                        }

                        if (@event.Type == SpanTypes.Test)
                        {
                            lock (tests)
                            {
                                tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                            }
                        }
                    }
                }
            }
        };

        using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                      agent,
                                      arguments: "--collect:\"XPlat Code Coverage;IncludeTestAssembly=true\"",
                                      packageVersion: packageVersion,
                                      expectedExitCode: 1);

        MockCIVisibilityTest[] receivedTests;
        lock (tests)
        {
            receivedTests = tests.ToArray();
        }

        string[] receivedEvpRequests;
        lock (evpRequests)
        {
            receivedEvpRequests = evpRequests.ToArray();
        }

        string[] receivedSkippableRequestBodies;
        lock (skippableRequestBodies)
        {
            receivedSkippableRequestBodies = skippableRequestBodies.ToArray();
        }

        var skippableRequestBody = receivedSkippableRequestBodies.Should().ContainSingle("received EVP requests: {0}", string.Join(", ", receivedEvpRequests)).Subject;
        var skippableRequest = JObject.Parse(skippableRequestBody);
        var skippableRequestAttributes = skippableRequest["data"]?["attributes"];
        skippableRequestAttributes.Should().NotBeNull("skippable request body: {0}", skippableRequestBody);
        skippableRequestAttributes!["test_level"]?.Value<string>().Should().Be("test");
        var configurations = skippableRequestAttributes["configurations"];
        configurations.Should().NotBeNull("skippable request body: {0}", skippableRequestBody);
        configurations![TestTags.Bundle]?.Value<string>().Should().Be(TestBundleName);
        configurations["os.platform"]?.Value<string>().Should().NotBeNullOrWhiteSpace();
        configurations["os.version"]?.Value<string>().Should().NotBeNullOrWhiteSpace();
        configurations["os.architecture"]?.Value<string>().Should().NotBeNullOrWhiteSpace();
        configurations["runtime.name"]?.Value<string>().Should().NotBeNullOrWhiteSpace();
        configurations["runtime.version"]?.Value<string>().Should().NotBeNullOrWhiteSpace();
        configurations["runtime.architecture"]?.Value<string>().Should().NotBeNullOrWhiteSpace();

        var skippedTest = receivedTests.Should().ContainSingle(test => test.Meta[TestTags.Name] == "SimplePassTest", "received EVP requests: {0}", string.Join(", ", receivedEvpRequests)).Subject;
        skippedTest.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
        skippedTest.Meta[IntelligentTestRunnerTags.SkippedBy].Should().Be("true");
        skippedTest.Meta[TestTags.SkipReason].Should().Be(IntelligentTestRunnerTags.SkippedByReason);
        skippedTest.CorrelationId.Should().Be(correlationId);

        string[] receivedUnresolvedCoverageReferences;
        lock (unresolvedCoverageReferences)
        {
            receivedUnresolvedCoverageReferences = unresolvedCoverageReferences.ToArray();
        }

        receivedUnresolvedCoverageReferences.Should().BeEmpty();

        CodeCoverageAggregationResult[] receivedCoverageResults;
        lock (coverageResults)
        {
            receivedCoverageResults = coverageResults.ToArray();
        }

        // This target uses an injected out-of-process session, so the testhost proves coverage backfill through the IPC message consumed by the parent session.
        var coverageResult = receivedCoverageResults.Should().ContainSingle().Subject;
        coverageResult.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        coverageResult.Backfilled.Should().BeTrue();
        coverageResult.ExecutableLines.Should().HaveValue();
        coverageResult.CoveredLines.Should().HaveValue();

        var executableLines = coverageResult.ExecutableLines.GetValueOrDefault();
        var coveredLines = coverageResult.CoveredLines.GetValueOrDefault();
        var coveredLinesWithoutBackfill = coveredLines - SimplePassTestBackfilledLineCount;
        var rawBackfilledPercentage = coveredLines / executableLines * 100;
        var rawPercentageWithoutBackfill = coveredLinesWithoutBackfill / executableLines * 100;
        // Coverlet exposes Percent truncated to two decimals, while the IPC message carries the line counts used to compute it.
        var expectedBackfilledPercentage = System.Math.Floor(rawBackfilledPercentage * 100) / 100;
        var expectedPercentageWithoutBackfill = System.Math.Floor(rawPercentageWithoutBackfill * 100) / 100;

        executableLines.Should().BeGreaterThan(0);
        coveredLinesWithoutBackfill.Should().BeGreaterThanOrEqualTo(0);
        coverageResult.Percentage.Should().BeApproximately(expectedBackfilledPercentage, 0.0001);
        coverageResult.Percentage.Should().BeGreaterThan(expectedPercentageWithoutBackfill);
    }

    /// <summary>
    /// Runs the real XUnit sample through ITR and verifies the skip decisions that guard coverage backfill.
    /// </summary>
    /// <param name="packageVersion">XUnit package version under test.</param>
    /// <param name="evpVersionToRemove">EVP endpoint version removed from the mock agent to force the target path.</param>
    /// <param name="expectedGzip">Whether the target EVP path should use gzip.</param>
    /// <param name="matrixCase">Backend-response shape under test.</param>
    public virtual async Task ItrCoverageBackfillSkippableDecisionMatrixMatchesJavaBehavior(string packageVersion, string evpVersionToRemove, bool expectedGzip, string matrixCase)
    {
        var tests = new List<MockCIVisibilityTest>();
        var coverageResults = new List<CodeCoverageAggregationResult>();
        var unresolvedCoverageReferences = new List<string>();
        var evpRequests = new List<string>();
        var skippableRequestBodies = new List<string>();

        InjectSession(
            out var sessionId,
            out _,
            out _,
            out _,
            out _,
            out _,
            out var runId);

        var sessionCommand = GetCoverageBackfillMatrixSessionCommand(matrixCase);
        Output.WriteLine("RunId: {0}", runId);
        Output.WriteLine("CoverageBackfillMatrixCase: {0}", matrixCase);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, sessionCommand);

        var ipcServerName = $"session_{sessionId}";
        using var ipcServer = new IpcServer(ipcServerName);
        ipcServer.SetMessageReceivedCallback(
            message =>
            {
                if (TryResolveCoverageIpcMessage(sessionId, message, out var coverageResult, out var unresolvedReference))
                {
                    lock (coverageResults)
                    {
                        coverageResults.Add(coverageResult);
                    }
                }
                else if (unresolvedReference is not null)
                {
                    lock (unresolvedCoverageReferences)
                    {
                        unresolvedCoverageReferences.Add(unresolvedReference);
                    }
                }
            });

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true, useStatsD: !IsMacOS());
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(evpVersionToRemove)).ToArray();

        const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            lock (evpRequests)
            {
                evpRequests.Add($"{e.Value.PathAndQuery} ({e.Value.Headers["Content-Type"] ?? "unknown"})");
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse("""{"data":{"id":"b5a855bffe6c0b2ae5d150fb6ad674363464c816","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}} """, 200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                lock (skippableRequestBodies)
                {
                    skippableRequestBodies.Add(e.Value.BodyInJson);
                }

                var skippableRequest = JObject.Parse(e.Value.BodyInJson);
                var requestConfigurations = skippableRequest["data"]?["attributes"]?["configurations"] as JObject;
                requestConfigurations.Should().NotBeNull("skippable request body: {0}", e.Value.BodyInJson);
                e.Value.Response = new MockTracerResponse(BuildCoverageBackfillMatrixSkippableResponse(matrixCase, correlationId, requestConfigurations), 200);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                e.Value.Headers["Content-Encoding"].Should().Be(expectedGzip ? "gzip" : null);

                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Content.ToString() is not { } eventContent || @event.Type != SpanTypes.Test)
                        {
                            continue;
                        }

                        lock (tests)
                        {
                            tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                        }
                    }
                }
            }
        };

        using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                      agent,
                                      arguments: CoverageBackfillMatrixTestArguments,
                                      packageVersion: packageVersion,
                                      expectedExitCode: 1);

        string[] receivedEvpRequests;
        lock (evpRequests)
        {
            receivedEvpRequests = evpRequests.ToArray();
        }

        string[] receivedSkippableRequestBodies;
        lock (skippableRequestBodies)
        {
            receivedSkippableRequestBodies = skippableRequestBodies.ToArray();
        }

        var skippableRequestBody = receivedSkippableRequestBodies.Should().ContainSingle("received EVP requests: {0}", string.Join(", ", receivedEvpRequests)).Subject;
        var skippableRequestAttributes = JObject.Parse(skippableRequestBody)["data"]?["attributes"];
        skippableRequestAttributes.Should().NotBeNull("skippable request body: {0}", skippableRequestBody);
        skippableRequestAttributes!["test_level"]?.Value<string>().Should().Be("test");
        var configurations = skippableRequestAttributes["configurations"];
        configurations.Should().NotBeNull("skippable request body: {0}", skippableRequestBody);
        configurations![TestTags.Bundle]?.Value<string>().Should().Be(TestBundleName);

        MockCIVisibilityTest[] receivedTests;
        lock (tests)
        {
            receivedTests = tests.ToArray();
        }

        AssertItrDecision(receivedTests, "SimplePassTest", ShouldSkipSimplePassTest(matrixCase), correlationId, receivedEvpRequests);
        if (matrixCase == SafeAndMissingLineCandidates)
        {
            AssertItrDecision(receivedTests, "TraitPassTest", shouldSkip: false, correlationId, receivedEvpRequests);
        }
        else if (matrixCase == ParameterizedCandidateDoesNotSkip)
        {
            receivedTests.Where(test => test.Meta[TestTags.Name] == "SimpleParameterizedTest")
                         .Should()
                         .NotBeEmpty("received EVP requests: {0}", string.Join(", ", receivedEvpRequests))
                         .And
                         .OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusPass);
        }

        if (ShouldAssertNoBackfilledCoverageMessages(matrixCase))
        {
            string[] receivedUnresolvedCoverageReferences;
            lock (unresolvedCoverageReferences)
            {
                receivedUnresolvedCoverageReferences = unresolvedCoverageReferences.ToArray();
            }

            receivedUnresolvedCoverageReferences.Should().BeEmpty();

            CodeCoverageAggregationResult[] receivedCoverageResults;
            lock (coverageResults)
            {
                receivedCoverageResults = coverageResults.ToArray();
            }

            receivedCoverageResults.Should().NotContain(result => result.Backfilled);
        }
    }

    public virtual async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        // TODO: Fix alpine flakiness
        Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine, an issue has been opened to investigate the root cause. Meanwhile we are skipping it.");
        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                expectedGzip,
                new TestScenario(
                    nameof(XUnitEvpTests),
                    friendlyName,
                    mockData,
                    expectedExitCode,
                    expectedSpans,
                    true,
                    (in ExecutionData data) =>
                    {
                        // Check the tests, suites and modules count
                        Assert.Equal(2, data.TestSuites.Count);
                        Assert.Single(data.TestModules);

                        if (friendlyName == "all_efd_with_atr")
                        {
                            AssertEfdSelectedOverAtr(data, "Samples.XUnitTests.TestSuite.SimplePassTest");
                        }
                        else if (friendlyName is
                                     "efd_with_test_bypass_paginated_missing_followup_page" or
                                     "efd_with_test_bypass_paginated_invalid_followup_payload" or
                                     "efd_with_test_bypass_paginated_missing_followup_page_info")
                        {
                            data.Tests.Should().OnlyContain(
                                t => !t.Meta.ContainsKey(TestTags.TestIsNew) &&
                                     !t.Meta.ContainsKey(TestTags.TestIsRetry) &&
                                     !t.Meta.ContainsKey(TestTags.TestRetryReason));
                        }
                    },
                    useDotnetExec: false))
           .ConfigureAwait(false);
    }

    public virtual async Task QuarantinedTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                expectedGzip,
                new TestScenario(
                    nameof(XUnitEvpTests),
                    friendlyName,
                    mockData,
                    expectedExitCode,
                    expectedSpans,
                    true,
                    (in ExecutionData data) =>
                    {
                        // Check the tests, suites and modules count
                        Assert.Equal(2, data.TestSuites.Count);
                        Assert.Single(data.TestModules);
                    },
                    useDotnetExec: false))
           .ConfigureAwait(false);
    }

    public virtual async Task DisabledTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                expectedGzip,
                new TestScenario(
                    nameof(XUnitEvpTests),
                    friendlyName,
                    mockData,
                    expectedExitCode,
                    expectedSpans,
                    true,
                    (in ExecutionData data) =>
                    {
                        // Check the tests, suites and modules count
                        Assert.Equal(2, data.TestSuites.Count);
                        Assert.Single(data.TestModules);
                    },
                    useDotnetExec: false))
           .ConfigureAwait(false);
    }

    public virtual async Task AttemptToFixTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                expectedGzip,
                new TestScenario(
                    nameof(XUnitEvpTests),
                    friendlyName,
                    mockData,
                    expectedExitCode,
                    expectedSpans,
                    true,
                    (in ExecutionData data) =>
                    {
                        // Check the tests, suites and modules count
                        Assert.Equal(2, data.TestSuites.Count);
                        Assert.Single(data.TestModules);
                    },
                    useDotnetExec: false))
           .ConfigureAwait(false);
    }

    private static bool TryResolveCoverageIpcMessage(ulong sessionId, object message, out CodeCoverageAggregationResult result, out string unresolvedReference)
    {
        result = default;
        unresolvedReference = null;

        if (message is SessionCodeCoverageMessage coverageMessage)
        {
            result = new CodeCoverageAggregationResult(
                coverageMessage.Source,
                coverageMessage.Value,
                coverageMessage.Backfilled,
                coverageMessage.ExecutableLines,
                coverageMessage.CoveredLines,
                coverageMessage.Diagnostic,
                coverageMessage.ResultId,
                coverageMessage.BackfillValidated,
                coverageMessage.BackfillNotApplicable,
                coverageMessage.BackfillValidation,
                coverageMessage.SupersededResultIds);
            return true;
        }

        if (message is SessionCodeCoverageReferenceMessage referenceMessage)
        {
            if (CoverageBackfillDataStore.TryReadCoverageIpcResults(sessionId, out var persistedResults))
            {
                foreach (var persistedResult in persistedResults)
                {
                    if (persistedResult.Source == referenceMessage.Source &&
                        string.Equals(persistedResult.ResultId, referenceMessage.ResultId, System.StringComparison.Ordinal))
                    {
                        result = persistedResult;
                        return true;
                    }
                }
            }

            unresolvedReference = $"{referenceMessage.Source}:{referenceMessage.ResultId}";
        }

        return false;
    }

    private static bool ShouldSkipSimplePassTest(string matrixCase)
        => matrixCase is MissingBackendCoverageStillSkips
                         or EmptyBackendConfigurationsStillSkip
                         or SafeAndMissingLineCandidates
                         or BackendCoverageDoesNotMatchLocalReport;

    private static bool ShouldAssertNoBackfilledCoverageMessages(string matrixCase)
        => matrixCase is not EmptyBackendConfigurationsStillSkip
                         and not SafeAndMissingLineCandidates;

    private static string GetCoverageBackfillMatrixSessionCommand(string matrixCase)
        => matrixCase switch
        {
            EmptyBackendConfigurationsStillSkip or DivergentBackendConfigurationsBlockSkip => "dotnet test --framework net8.0 " + CoverageBackfillMatrixTestArguments,
            _ => "dotnet test " + CoverageBackfillMatrixTestArguments
        };

    private static string BuildCoverageBackfillMatrixSkippableResponse(string matrixCase, string correlationId, JObject requestConfigurations)
    {
        var response = new JObject
        {
            ["data"] = CreateCoverageBackfillMatrixSkippableTests(matrixCase, requestConfigurations),
            ["meta"] = new JObject
            {
                ["correlation_id"] = correlationId
            }
        };

        if (matrixCase != MissingBackendCoverageStillSkips)
        {
            response["meta"]!["coverage"] = new JObject
            {
                [matrixCase == BackendCoverageDoesNotMatchLocalReport ? XUnitSampleSourcePath + ".unmatched" : XUnitSampleSourcePath] = SimplePassTestLineCoverageBitmap
            };
        }

        return response.ToString(Formatting.None);
    }

    private static JArray CreateCoverageBackfillMatrixSkippableTests(string matrixCase, JObject requestConfigurations)
    {
        if (matrixCase == NoSkippableResponse)
        {
            return new JArray();
        }

        if (matrixCase == DivergentBackendConfigurationsBlockSkip)
        {
            return new JArray(
                CreateSkippableTestResponse("Samples.XUnitTests.TestSuite.SimplePassTest", "SimplePassTest", missingLineCodeCoverage: false, CloneConfigurations(requestConfigurations)),
                CreateSkippableTestResponse("Samples.XUnitTests.TestSuite.OtherPassTest", "OtherPassTest", missingLineCodeCoverage: false, CloneConfigurations(requestConfigurations, "matrix-different-runtime")));
        }

        if (matrixCase == SafeAndMissingLineCandidates)
        {
            return new JArray(
                CreateSkippableTestResponse("Samples.XUnitTests.TestSuite.SimplePassTest", "SimplePassTest", missingLineCodeCoverage: false, CloneConfigurations(requestConfigurations)),
                CreateSkippableTestResponse("Samples.XUnitTests.TestSuite.TraitPassTest", "TraitPassTest", missingLineCodeCoverage: true, CloneConfigurations(requestConfigurations)));
        }

        var skippableTest = CreateSkippableTestResponse(
            matrixCase == ParameterizedCandidateDoesNotSkip ? "Samples.XUnitTests.TestSuite.SimpleParameterizedTest" : "Samples.XUnitTests.TestSuite.SimplePassTest",
            matrixCase == ParameterizedCandidateDoesNotSkip ? "SimpleParameterizedTest" : "SimplePassTest",
            missingLineCodeCoverage: matrixCase == MissingLineCoverageBlocksSkip,
            matrixCase == EmptyBackendConfigurationsStillSkip ? new JObject() : CloneConfigurations(requestConfigurations));

        if (matrixCase == ParameterizedCandidateDoesNotSkip)
        {
            skippableTest["attributes"]!["parameters"] = """{"metadata":{"test_name":"SimpleParameterizedTest(xValue: 99, yValue: 99, expectedResult: 198)"},"arguments":{"xValue":"99","yValue":"99","expectedResult":"198"}}""";
        }

        return new JArray(skippableTest);
    }

    private static JObject CreateSkippableTestResponse(string id, string name, bool missingLineCodeCoverage, JObject configurations)
    {
        return new JObject
        {
            ["id"] = id,
            ["type"] = "test_params",
            ["attributes"] = new JObject
            {
                ["suite"] = TestSuiteName,
                ["name"] = name,
                ["_missing_line_code_coverage"] = missingLineCodeCoverage,
                ["configurations"] = configurations
            }
        };
    }

    private static JObject CloneConfigurations(JObject configurations, string runtimeVersion = null)
    {
        var clone = (JObject)configurations.DeepClone();
        if (runtimeVersion is not null)
        {
            clone["runtime.version"] = runtimeVersion;
        }

        return clone;
    }

    private static void AssertItrDecision(IReadOnlyCollection<MockCIVisibilityTest> receivedTests, string testName, bool shouldSkip, string correlationId, string[] receivedEvpRequests)
    {
        var test = receivedTests.Should().ContainSingle(test => test.Meta[TestTags.Name] == testName, "received EVP requests: {0}", string.Join(", ", receivedEvpRequests)).Subject;
        if (shouldSkip)
        {
            test.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
            test.Meta[IntelligentTestRunnerTags.SkippedBy].Should().Be("true");
            test.Meta[TestTags.SkipReason].Should().Be(IntelligentTestRunnerTags.SkippedByReason);
            test.CorrelationId.Should().Be(correlationId);
        }
        else
        {
            test.Meta[TestTags.Status].Should().Be(TestTags.StatusPass);
            test.Meta.Should().NotContainKey(IntelligentTestRunnerTags.SkippedBy);
            test.Meta.Should().NotContainKey(TestTags.SkipReason);
        }
    }

    private static bool HasCorrectCompressionTag(string[] tags, bool isGzipped)
        => isGzipped ? tags.Contains("rq_compressed:true") : !tags.Contains("rq_compressed:true");
}
