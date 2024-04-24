// <copyright file="NUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
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

        public static IEnumerable<object[]> GetDataForEarlyFlakeDetection()
        {
            foreach (var row in GetData())
            {
                // settings json, efd tests json, expected spans, friendly name

                // EFD for all tests
                yield return row.Concat(
                    """{"data":{"id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"early_flake_detection":{"enabled":true,"slow_test_retries":{"10s":5,"30s":3,"5m":2,"5s":10},"faulty_session_threshold":100},"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}}""",
                    """{"data":{"id":"lNemDTwOV8U","type":"ci_app_libraries_tests","attributes":{"tests":{}}}}""",
                    249,
                    "all_efd");

                // EFD with 1 test to bypass (TraitPassTest)
                yield return row.Concat(
                    """{"data":{"id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"early_flake_detection":{"enabled":true,"slow_test_retries":{"10s":5,"30s":3,"5m":2,"5s":10},"faulty_session_threshold":100},"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}}""",
                    """{"data":{"id":"lNemDTwOV8U","type":"ci_app_libraries_tests","attributes":{"tests":{"Samples.NUnitTests":{"Samples.NUnitTests.TestSuite":["TraitPassTest"]}}}}}""",
                    240,
                    "efd_with_test_bypass");
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
            var sessionId = RandomIdGenerator.Shared.NextSpanId();
            var sessionCommand = "test command";
            var sessionWorkingDirectory = "C:\\evp_demo\\working_directory";
            SetEnvironmentVariable(HttpHeaderNames.TraceId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
            SetEnvironmentVariable(HttpHeaderNames.ParentId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
            SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, sessionCommand);
            SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable, sessionWorkingDirectory);

            const string gitRepositoryUrl = "git@github.com:DataDog/dd-trace-dotnet.git";
            const string gitBranch = "main";
            const string gitCommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitRepository, gitRepositoryUrl);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitBranch, gitBranch);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitCommitSha, gitCommitSha);

            try
            {
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

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

                    using (ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
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
                            targetTest.Meta.Remove(EarlyFlakeDetectionTags.TestIsNew);
                            targetTest.Meta.Remove(EarlyFlakeDetectionTags.TestIsRetry);

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
        public async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, string settingsJson, string testsJson, int expectedSpans, string friendlyName)
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
            var sessionId = RandomIdGenerator.Shared.NextSpanId();
            var sessionCommand = "test command";
            var sessionWorkingDirectory = "C:\\evp_demo\\working_directory";
            SetEnvironmentVariable(HttpHeaderNames.TraceId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
            SetEnvironmentVariable(HttpHeaderNames.ParentId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
            SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, sessionCommand);
            SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable, sessionWorkingDirectory);

            const string gitRepositoryUrl = "git@github.com:DataDog/dd-trace-dotnet.git";
            const string gitBranch = "main";
            const string gitCommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitRepository, gitRepositoryUrl);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitBranch, gitBranch);
            SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitCommitSha, gitCommitSha);

            try
            {
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

                using var agent = EnvironmentHelper.GetMockAgent();
                agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains(evpVersionToRemove)).ToArray();

                const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
                agent.EventPlatformProxyPayloadReceived += (sender, e) =>
                {
                    if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                    {
                        e.Value.Response = new MockTracerResponse(settingsJson, 200);
                        return;
                    }

                    if (e.Value.PathAndQuery.EndsWith("api/v2/ci/libraries/tests"))
                    {
                        e.Value.Response = new MockTracerResponse(testsJson, 200);
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

                using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion);

                var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
                settings.UseTextForParameters(friendlyName);
                settings.DisableRequireUniquePrefix();
                await Verifier.Verify(
                    tests
                       .OrderBy(s => s.Resource)
                       .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters))
                       .ThenBy(s => s.Meta.GetValueOrDefault(EarlyFlakeDetectionTags.TestIsNew))
                       .ThenBy(s => s.Meta.GetValueOrDefault(EarlyFlakeDetectionTags.TestIsRetry))
                       .ThenBy(s => s.Meta.GetValueOrDefault(EarlyFlakeDetectionTags.AbortReason)),
                    settings);

                // Check the tests, suites and modules count
                Assert.Equal(expectedSpans, tests.Count);
                Assert.Equal(ExpectedTestSuiteCount, testSuites.Count);
                Assert.Single(testModules);
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
