// <copyright file="NUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class NUnitEvpTests : TestingFrameworkEvpTest
    {
        private const int ExpectedTestCount = 33;
        private const int ExpectedTestSuiteCount = 10;

        private const string TestBundleName = "Samples.NUnitTests";
        private static readonly string[] _testSuiteNames =
        {
            "Samples.NUnitTests.TestSuite",
            "Samples.NUnitTests.TestFixtureTest(\"Test01\")",
            "Samples.NUnitTests.TestFixtureTest(\"Test02\")",
            "Samples.NUnitTests.TestString",
            "Samples.NUnitTests.TestFixtureSetupError(\"Test01\")",
            "Samples.NUnitTests.TestFixtureSetupError(\"Test02\")",
            "Samples.NUnitTests.TestSetupError",
            "Samples.NUnitTests.TestTearDownError",
            "Samples.NUnitTests.TestTearDown2Error",
            "Samples.NUnitTests.UnSkippableSuite",
        };

        public NUnitEvpTests(ITestOutputHelper output)
            : base("NUnitTests", output)
        {
            SetServiceName("nunit-tests-evp");
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetData()
        {
            foreach (var version in PackageVersions.NUnit)
            {
                // EVP version to remove, expects gzip
                yield return version.Concat("evp_proxy/v2", true);
                yield return version.Concat("evp_proxy/v4", false);
            }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:Parameter should not span multiple lines", Justification = "readability")]
        public static IEnumerable<object[]> GetDataForEarlyFlakeDetection()
        {
            foreach (var row in GetData())
            {
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
                    249,
                    "all_efd");

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
                                        "Samples.NUnitTests":{
                                            "Samples.NUnitTests.TestSuite":["TraitPassTest"]
                                        }
                                    }
                                }
                            }
                        }
                        """,
                        string.Empty),
                    1,
                    240,
                    "efd_with_test_bypass");
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
                                        "Samples.NUnitTests": {
                                            "suites": {
                                                "Samples.NUnitTests.TestSuite": {
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
                    1,
                    ExpectedTestCount,
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
                                        "Samples.NUnitTests": {
                                            "suites": {
                                                "Samples.NUnitTests.TestSuite": {
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
                    ExpectedTestCount,
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
                                        "Samples.NUnitTests": {
                                            "suites": {
                                                "Samples.NUnitTests.TestSuite": {
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
                    ExpectedTestCount + 9 + 9,
                    "quarantined_and_disabled");
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetData))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
        {
            if (new Version(FrameworkDescription.Instance.ProductVersion).Major >= 5)
            {
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) < new Version("3.13"))
                {
                    // Ignore due https://github.com/nunit/nunit/issues/3565#issuecomment-726835235
                    return;
                }
            }

            var tests = new List<MockCIVisibilityTest>();
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
            try
            {
                using (var agent = EnvironmentHelper.GetMockAgent())
                {
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
                                            var testObject = JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent);
                                            Output.WriteLine($"Test: {testObject.Meta[TestTags.Suite]}.{testObject.Meta[TestTags.Name]} | {testObject.Meta[TestTags.Status]}");
                                            tests.Add(testObject);
                                        }
                                        else if (@event.Type == SpanTypes.TestSuite)
                                        {
                                            var suiteObject = JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(eventContent);
                                            Output.WriteLine($"Suite: {suiteObject.Meta[TestTags.Suite]} | {suiteObject.Meta[TestTags.Status]}");
                                            testSuites.Add(suiteObject);
                                        }
                                        else if (@event.Type == SpanTypes.TestModule)
                                        {
                                            var moduleObject = JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(eventContent);
                                            Output.WriteLine($"Module: {moduleObject.Meta[TestTags.Module]} | {moduleObject.Meta[TestTags.Status]}");
                                            testModules.Add(moduleObject);
                                        }
                                    }
                                }
                            }
                        }
                    };

                    using (ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: 1))
                    {
                        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
                        settings.UseTextForParameters("packageVersion=all");
                        settings.DisableRequireUniquePrefix();
                        await Verifier.Verify(tests.OrderBy(s => s.Resource).ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters)), settings);

                        // Check the tests, suites and modules count
                        Assert.Equal(ExpectedTestCount, tests.Count);
                        Assert.Equal(ExpectedTestSuiteCount, testSuites.Count);
                        Assert.Single(testModules);
                        var testModule = testModules[0];

                        // Check suites
                        Assert.True(tests.All(t => testSuites.Find(s => s.TestSuiteId == t.TestSuiteId) != null));
                        Assert.True(tests.All(t => t.TestModuleId == testModule.TestModuleId));
                        testSuites.Should().AllSatisfy(testSuite => testSuite.Meta.Should().ContainKey(TestTags.SourceFile));
                        testSuites.Should().AllSatisfy(testSuite => testSuite.Meta.Should().ContainKey(TestTags.CodeOwners));

                        // Check Module
                        Assert.True(tests.All(t => t.TestModuleId == testSuites[0].TestModuleId));

                        // ITR tags inside the test module
                        testModule.Metrics.Should().Contain(IntelligentTestRunnerTags.SkippingCount, 1);
                        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.SkippingType, IntelligentTestRunnerTags.SkippingTypeTest);
                        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.TestsSkipped, "true");

                        // Check Session
                        tests.Should().OnlyContain(t => t.TestSessionId == testSuites[0].TestSessionId);
                        testSuites[0].TestSessionId.Should().Be(testModule.TestSessionId);
                        testModule.TestSessionId.Should().Be(sessionId);

                        foreach (var targetSuite in testSuites.ToArray())
                        {
                            switch (targetSuite.Meta[TestTags.Suite])
                            {
                                case "Samples.NUnitTests.TestSuite":
                                case "Samples.NUnitTests.TestSetupError":
                                case "Samples.NUnitTests.TestFixtureSetupError(\"Test01\")":
                                case "Samples.NUnitTests.TestFixtureSetupError(\"Test02\")":
                                case "Samples.NUnitTests.TestTearDownError":
                                case "Samples.NUnitTests.TestTearDown2Error":
                                    Assert.Equal(TestTags.StatusFail, targetSuite.Meta[TestTags.Status]);
                                    Assert.True(targetSuite.Meta.ContainsKey(Tags.ErrorType));
                                    Assert.True(targetSuite.Meta.ContainsKey(Tags.ErrorMsg));
                                    break;
                                default:
                                    Assert.Equal(TestTags.StatusPass, targetSuite.Meta[TestTags.Status]);
                                    break;
                            }
                        }

                        foreach (var targetTest in tests.ToArray())
                        {
                            // Remove decision maker tag (not used by the backend for civisibility)
                            targetTest.Meta.Remove(Tags.Propagated.DecisionMaker);

                            // Remove EFD tags
                            targetTest.Meta.Remove(TestTags.TestIsNew);
                            targetTest.Meta.Remove(TestTags.TestIsRetry);

                            // Remove capabilities
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesAutoTestRetries);
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementQuarantine);
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesEarlyFlakeDetection);
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestImpactAnalysis);
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementDisable);
                            targetTest.Meta.Remove(CapabilitiesTags.LibraryCapabilitiesTestManagementAttemptToFix);

                            // Remove user provided service tag
                            targetTest.Meta.Remove(CommonTags.UserProvidedTestServiceTag);

                            // check the name
                            Assert.Equal("nunit.test", targetTest.Name);

                            // check correlationId
                            Assert.Equal(correlationId, targetTest.CorrelationId);

                            // check the CIEnvironmentValues decoration.
                            CheckCIEnvironmentValuesDecoration(targetTest, gitRepositoryUrl, gitBranch, gitCommitSha);

                            // check the runtime values
                            CheckRuntimeValues(targetTest);

                            // check the bundle name
                            AssertTargetSpanAnyOf(targetTest, TestTags.Bundle, TestBundleName);
                            AssertTargetSpanAnyOf(targetTest, TestTags.Module, TestBundleName);

                            // check the suite name
                            var suite = AssertTargetSpanAnyOf(targetTest, TestTags.Suite, _testSuiteNames);

                            // check the test type
                            AssertTargetSpanEqual(targetTest, TestTags.Type, TestTags.TypeTest);

                            // check the test framework
                            AssertTargetSpanContains(targetTest, TestTags.Framework, "NUnit");
                            Assert.True(targetTest.Meta.Remove(TestTags.FrameworkVersion));

                            // check the version
                            AssertTargetSpanEqual(targetTest, "version", "1.0.0");

                            // checks the source tags
                            AssertTargetSpanExists(targetTest, TestTags.SourceFile);

                            // checks code owners
                            AssertTargetSpanExists(targetTest, TestTags.CodeOwners);

                            // checks the origin tag
                            CheckOriginTag(targetTest);

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
                                case "Test" when !suite.Contains("SetupError") && !suite.Contains("TearDownError"):
                                case "IsNull" when !suite.Contains("SetupError") && !suite.Contains("TearDownError"):
                                    CheckSimpleTestSpan(targetTest);
                                    break;

                                case "SkipByITRSimulation":
                                    AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);
                                    AssertTargetSpanEqual(targetTest, TestTags.SkipReason, IntelligentTestRunnerTags.SkippedByReason);
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "true");
                                    break;

                                case "SimpleSkipFromAttributeTest":
                                    CheckSimpleSkipFromAttributeTest(targetTest);
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
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
                                    CheckParametrizedTraitsValues(targetTest);
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(1,1,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(2,2,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(3,3,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleSkipParameterizedTest":
                                    CheckSimpleSkipFromAttributeTest(targetTest);
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(1,1,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(2,2,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(3,3,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "SimpleErrorParameterizedTest":
                                    CheckSimpleErrorTest(targetTest);
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(1,0,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(2,0,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(3,0,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleAssertPassTest":
                                    CheckSimpleTestSpan(targetTest);
                                    break;

                                case "SimpleAssertInconclusive":
                                    CheckSimpleSkipFromAttributeTest(targetTest, "The test is inconclusive.");
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "Test" when suite.Contains("SetupError"):
                                case "Test01" when suite.Contains("SetupError"):
                                case "Test02" when suite.Contains("SetupError"):
                                case "Test03" when suite.Contains("SetupError"):
                                case "Test04" when suite.Contains("SetupError"):
                                case "Test05" when suite.Contains("SetupError"):
                                case "IsNull" when suite.Contains("SetupError") || suite.Contains("TearDownError"):
                                    CheckSetupOrTearDownErrorTest(targetTest);
                                    break;

                                case "UnskippableTest":
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.UnskippableTag, "true");
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.ForcedRunTag, "false");
                                    CheckSimpleTestSpan(targetTest);
                                    break;
                            }

                            // check remaining tag (only the name)
                            Assert.Single(targetTest.Meta);

                            tests.Remove(targetTest);
                        }
                    }
                }
            }
            catch
            {
                Output.WriteLine("Framework Version: " + new Version(FrameworkDescription.Instance.ProductVersion));
                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    Output.WriteLine("Package Version: " + new Version(packageVersion));
                }

                WriteSpans(testSuites);
                WriteSpans(tests);
                throw;
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetDataForEarlyFlakeDetection))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        [Trait("Category", "EarlyFlakeDetection")]
        public async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
        {
            // TODO: Fix alpine flakiness
            Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine, an issue has been opened to investigate the root cause. Meanwhile we are skipping it.");

            if (new Version(FrameworkDescription.Instance.ProductVersion).Major >= 5)
            {
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) < new Version("3.13"))
                {
                    // Ignore due https://github.com/nunit/nunit/issues/3565#issuecomment-726835235
                    return;
                }
            }

            await ExecuteTestAsync(
                    packageVersion,
                    evpVersionToRemove,
                    expectedGzip,
                    new TestScenario(
                        nameof(NUnitEvpTests),
                        friendlyName,
                        mockData,
                        expectedExitCode,
                        expectedSpans,
                        true,
                        (in ExecutionData data) =>
                        {
                            // Check the tests, suites and modules count
                            Assert.Equal(ExpectedTestSuiteCount, data.TestSuites.Count);
                            Assert.Single(data.TestModules);
                        },
                        useDotnetExec: false))
               .ConfigureAwait(false);
        }

        [SkippableTheory]
        [MemberData(nameof(GetDataForQuarantinedTests))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        [Trait("Category", "QuarantinedTests")]
        public async Task QuarantinedTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
        {
            await ExecuteTestAsync(
                    packageVersion,
                    evpVersionToRemove,
                    expectedGzip,
                    new TestScenario(
                        nameof(NUnitEvpTests),
                        friendlyName,
                        mockData,
                        expectedExitCode,
                        expectedSpans,
                        true,
                        (in ExecutionData data) =>
                        {
                            // Check the tests, suites and modules count
                            Assert.Equal(ExpectedTestSuiteCount, data.TestSuites.Count);
                            Assert.Single(data.TestModules);
                        },
                        useDotnetExec: false))
               .ConfigureAwait(false);
        }

        [SkippableTheory]
        [MemberData(nameof(GetDataForDisabledTests))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        [Trait("Category", "DisabledTests")]
        public async Task DisabledTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
        {
            await ExecuteTestAsync(
                    packageVersion,
                    evpVersionToRemove,
                    expectedGzip,
                    new TestScenario(
                        nameof(NUnitEvpTests),
                        friendlyName,
                        mockData,
                        expectedExitCode,
                        expectedSpans,
                        true,
                        (in ExecutionData data) =>
                        {
                            // Check the tests, suites and modules count
                            Assert.Equal(ExpectedTestSuiteCount, data.TestSuites.Count);
                            Assert.Single(data.TestModules);
                        },
                        useDotnetExec: false))
               .ConfigureAwait(false);
        }

        [SkippableTheory]
        [MemberData(nameof(GetDataForAttemptToFixTests))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        [Trait("Category", "AttemptToFixTests")]
        public async Task AttemptToFixTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
        {
            await ExecuteTestAsync(
                    packageVersion,
                    evpVersionToRemove,
                    expectedGzip,
                    new TestScenario(
                        nameof(NUnitEvpTests),
                        friendlyName,
                        mockData,
                        expectedExitCode,
                        expectedSpans,
                        true,
                        (in ExecutionData data) =>
                        {
                            // Check the tests, suites and modules count
                            Assert.Equal(ExpectedTestSuiteCount, data.TestSuites.Count);
                            Assert.Single(data.TestModules);
                        },
                        useDotnetExec: false))
               .ConfigureAwait(false);
        }

        protected override void CheckSimpleTestSpan(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            base.CheckSimpleTestSpan(targetTest);

            // Check the `test.message` tag. We check if contains the default or the custom message.
            if (targetTest.Meta.ContainsKey(TestTags.Message))
            {
                AssertTargetSpanAnyOf(targetTest, TestTags.Message, new string[] { "Test is ok", "The test passed." });
            }
        }

        private void CheckParametrizedTraitsValues(MockCIVisibilityTest targetTest)
        {
            // Check the traits tag value
            AssertTargetSpanAnyOf(
                targetTest,
                TestTags.Traits,
                "{\"Category\":[\"ParemeterizedTest\",\"FirstCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"SecondCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"ThirdCase\"]}");
        }

        private void CheckSetupOrTearDownErrorTest(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

            // Check the span error flag
            Assert.Equal(1, targetTest.Error);

            // Check the error type
            AssertTargetSpanAnyOf(targetTest, Tags.ErrorType, "SetUpException", "System.Exception", "TearDownException");

            // Check the error message
            AssertTargetSpanAnyOf(targetTest, Tags.ErrorMsg, "SetUp exception.", "TearDown exception.");

            // Remove the stacktrace
            targetTest.Meta.Remove(Tags.ErrorStack);
        }
    }
}
