// <copyright file="MsTestV2EvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
    public class MsTestV2EvpTests : TestingFrameworkEvpTest
    {
        private const string TestSuiteName = "Samples.MSTestTests.TestSuite";
        private const string TestBundleName = "Samples.MSTestTests";
        private const string ClassInitializationExceptionTestSuiteName = "Samples.MSTestTests.ClassInitializeExceptionTestSuite";

        public MsTestV2EvpTests(ITestOutputHelper output)
            : base("MSTestTests", output)
        {
            SetServiceName("mstest-tests-evp");
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetData()
        {
            foreach (var version in PackageVersions.MSTest)
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
                    """{"data":{"id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"early_flake_detection":{"enabled":true,"slow_test_retries":{"10s":10,"30s":10,"5m":10,"5s":10},"faulty_session_threshold":100},"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true,"known_tests_enabled":true}}}""",
                    """{"data":{"id":"lNemDTwOV8U","type":"ci_app_libraries_tests","attributes":{"tests":{}}}}""",
                    146,
                    148,
                    "all_efd");

                // EFD with 1 test to bypass (TraitPassTest)
                yield return row.Concat(
                    """{"data":{"id":"511938a3f19c12f8bb5e5caa695ca24f4563de3f","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"early_flake_detection":{"enabled":true,"slow_test_retries":{"10s":10,"30s":10,"5m":10,"5s":10},"faulty_session_threshold":100},"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true,"known_tests_enabled":true}}}""",
                    """{"data":{"id":"lNemDTwOV8U","type":"ci_app_libraries_tests","attributes":{"tests":{"Samples.MSTestTests":{"Samples.MSTestTests.TestSuite":["TraitPassTest"]}}}}}""",
                    137,
                    139,
                    "efd_with_test_bypass");
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetData))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
        {
            var version = string.IsNullOrEmpty(packageVersion) ? new Version("2.2.8") : new Version(packageVersion);
            var tests = new List<MockCIVisibilityTest>();
            var testSuites = new List<MockCIVisibilityTestSuite>();
            var testModules = new List<MockCIVisibilityTestModule>();
            var expectedTestCount = version.CompareTo(new Version("2.2.3")) <= 0 ? 20 : 22;

            // Inject session
            InjectSession(
                out var sessionId,
                out var sessionCommand,
                out var sessionWorkingDirectory,
                out var gitRepositoryUrl,
                out var gitBranch,
                out var gitCommitSha);

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
                                            tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
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

                    using (ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: 1))
                    {
                        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
                        settings.UseTextForParameters("packageVersion=" + (expectedTestCount == 20 ? "pre_2_2_4" : "post_2_2_4"));
                        settings.DisableRequireUniquePrefix();
                        await Verifier.Verify(
                            tests
                               .OrderBy(s => s.Resource)
                               .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Name))
                               .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters)),
                            settings);

                        // Check the tests, suites and modules count
                        Assert.Equal(expectedTestCount, tests.Count);
                        testSuites.Should().HaveCountLessThanOrEqualTo(2);
                        Assert.Single(testModules);

                        var testSuite = testSuites[0];
                        var testModule = testModules[0];

                        // Check Suite
                        testSuites.Select(ts => ts.TestSuiteId)
                                  .Intersect(tests.Select(t => t.TestSuiteId))
                                  .Should()
                                  .HaveCountLessThanOrEqualTo(2);
                        Assert.True(testSuite.TestModuleId == testModule.TestModuleId);

                        // ITR tags inside the test suite
                        testSuites.SelectMany(s => s.Metrics)
                                  .Should()
                                  .ContainEquivalentOf(new KeyValuePair<string, double>(IntelligentTestRunnerTags.SkippingCount, 1));

                        // Check Module
                        Assert.True(tests.All(t => t.TestModuleId == testSuite.TestModuleId));

                        // ITR tags inside the test module
                        testModule.Metrics.Should().Contain(IntelligentTestRunnerTags.SkippingCount, 1);
                        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.SkippingType, IntelligentTestRunnerTags.SkippingTypeTest);
                        testModule.Meta.Should().Contain(IntelligentTestRunnerTags.TestsSkipped, "true");

                        // Check Session
                        tests.Should().OnlyContain(t => t.TestSessionId == testSuite.TestSessionId);
                        testSuite.TestSessionId.Should().Be(testModule.TestSessionId);
                        testModule.TestSessionId.Should().Be(sessionId);

                        foreach (var targetTest in tests)
                        {
                            // Remove decision maker tag (not used by the backend for civisibility)
                            targetTest.Meta.Remove(Tags.Propagated.DecisionMaker);

                            // Remove EFD tags
                            targetTest.Meta.Remove(TestTags.TestIsNew);
                            targetTest.Meta.Remove(TestTags.TestIsRetry);

                            // Remove user provided service tag
                            targetTest.Meta.Remove(CommonTags.UserProvidedTestServiceTag);

                            // check the name
                            Assert.Equal("mstestv2.test", targetTest.Name);

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
                            AssertTargetSpanAnyOf(targetTest, TestTags.Suite, TestSuiteName, ClassInitializationExceptionTestSuiteName);

                            // check the test type
                            AssertTargetSpanEqual(targetTest, TestTags.Type, TestTags.TypeTest);

                            // check the test framework
                            AssertTargetSpanContains(targetTest, TestTags.Framework, "MSTestV2");
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
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleSkipParameterizedTest":
                                    CheckSimpleSkipFromAttributeTest(targetTest);
                                    // On callsite the parameters tags are being sent with no parameters, this is not required due the whole test is skipped.
                                    // That behavior has changed in calltarget.
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"(default)\",\"yValue\":\"(default)\",\"expectedResult\":\"(default)\"}}");
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "SimpleErrorParameterizedTest":
                                    CheckSimpleErrorTest(targetTest);
                                    AssertTargetSpanAnyOf(
                                        targetTest,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "UnskippableTest":
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.UnskippableTag, "true");
                                    AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.ForcedRunTag, "false");
                                    CheckSimpleTestSpan(targetTest);
                                    break;

                                case "ClassInitializeExceptionTestMethod":
                                    AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);
                                    targetTest.Error.Should().Be(1);
                                    AssertTargetSpanEqual(targetTest, Tags.ErrorType, "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestFailedException");
                                    AssertTargetSpanContains(targetTest, Tags.ErrorStack, "System.Exception: Class initialize exception");
                                    AssertTargetSpanContains(targetTest, Tags.ErrorMsg, "Class initialize exception.");
                                    break;

                                case "My Custom: CustomTestMethodAttributeTest":
                                case "My Custom 2: CustomRenameTestMethodAttributeTest":
                                case "My Custom 3|1: CustomMultipleResultsTestMethodAttributeTest":
                                case "My Custom 3|2: CustomMultipleResultsTestMethodAttributeTest":
                                    AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusPass);
                                    break;
                            }

                            // check remaining tag (only the name)
                            Assert.Single(targetTest.Meta);
                        }
                    }
                }
            }
            catch
            {
                WriteSpans(tests);
                throw;
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetDataForEarlyFlakeDetection))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        [Trait("Category", "EarlyFlakeDetection")]
        public async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, string settingsJson, string testsJson, int expectedSpansForPre224, int expectedSpansForPost224, string friendlyName)
        {
            // TODO: Fix alpine flakiness
            Skip.If(EnvironmentHelper.IsAlpine(), "This test is currently flaky in alpine, an issue has been opened to investigate the root cause. Meanwhile we are skipping it.");

            SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
            var version = string.IsNullOrEmpty(packageVersion) ? new Version("2.2.8") : new Version(packageVersion);
            var tests = new List<MockCIVisibilityTest>();
            var testSuites = new List<MockCIVisibilityTestSuite>();
            var testModules = new List<MockCIVisibilityTestModule>();
            var expectedTestCount = version.CompareTo(new Version("2.2.3")) <= 0 ? expectedSpansForPre224 : expectedSpansForPost224;

            // Inject session
            InjectSession(
                out var sessionId,
                out var sessionCommand,
                out var sessionWorkingDirectory,
                out var gitRepositoryUrl,
                out var gitBranch,
                out var gitCommitSha);

            try
            {
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
                                        tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
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

                using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion, expectedExitCode: 1);

                var packageVersionDescription = expectedTestCount == expectedSpansForPre224 ? "pre_2_2_4" : "post_2_2_4";
                var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
                settings.UseTextForParameters($"packageVersion={packageVersionDescription}_{friendlyName}");
                settings.DisableRequireUniquePrefix();
                await Verifier.Verify(
                    tests
                       .OrderBy(s => s.Resource)
                       .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Name))
                       .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters))
                       .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.TestIsNew))
                       .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.TestIsRetry))
                       .ThenBy(s => s.Meta.GetValueOrDefault(EarlyFlakeDetectionTags.AbortReason)),
                    settings);

                // Check the tests, suites and modules count
                Assert.Equal(expectedTestCount, tests.Count);
                testSuites.Should().HaveCountLessThanOrEqualTo(2);
                Assert.Single(testModules);
            }
            catch
            {
                WriteSpans(tests);
                throw;
            }
        }
    }
}
