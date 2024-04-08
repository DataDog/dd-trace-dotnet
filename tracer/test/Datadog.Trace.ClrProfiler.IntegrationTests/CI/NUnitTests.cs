// <copyright file="NUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public class NUnitTests : TestingFrameworkTest
    {
        private const int ExpectedSpanCount = 33;

        private const string TestBundleName = "Samples.NUnitTests";
        private static string[] _testSuiteNames =
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

        public NUnitTests(ITestOutputHelper output)
            : base("NUnitTests", output)
        {
            SetServiceName("nunit-tests");
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.NUnit), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task SubmitTraces(string packageVersion)
        {
            if (new Version(FrameworkDescription.Instance.ProductVersion).Major >= 5)
            {
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) < new Version("3.13"))
                {
                    // Ignore due https://github.com/nunit/nunit/issues/3565#issuecomment-726835235
                    return;
                }
            }

            List<MockSpan> spans = null;
            try
            {
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

                using (var agent = EnvironmentHelper.GetMockAgent())
                {
                    // We remove the evp_proxy endpoint to force the APM protocol compatibility
                    agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy/v2") && !e.Contains("evp_proxy/v4")).ToArray();
                    using (ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
                    {
                        spans = agent.WaitForSpans(ExpectedSpanCount)
                                     .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                                     .ToList();

                        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings("all");
                        settings.DisableRequireUniquePrefix();
                        await Verifier.Verify(spans.OrderBy(s => s.Resource).ThenBy(s => s.Tags.GetValueOrDefault(TestTags.Parameters)), settings);

                        // Check the span count
                        spans.Should().HaveCount(ExpectedSpanCount);

                        foreach (var targetSpan in spans.ToArray())
                        {
                            // Remove decision maker tag (not used by the backend for civisibility)
                            targetSpan.Tags.Remove(Tags.Propagated.DecisionMaker);
                            targetSpan.Tags.Remove(Tags.Propagated.TraceIdUpper);

                            // Remove git metadata added by the apm agent writer.
                            targetSpan.Tags.Remove(Tags.GitCommitSha);
                            targetSpan.Tags.Remove(Tags.GitRepositoryUrl);

                            // Remove EFD tags
                            targetSpan.Tags.Remove(EarlyFlakeDetectionTags.TestIsNew);
                            targetSpan.Tags.Remove(EarlyFlakeDetectionTags.TestIsRetry);

                            // check the name
                            targetSpan.Name.Should().Be("nunit.test");

                            // check the CIEnvironmentValues decoration.
                            CheckCIEnvironmentValuesDecoration(targetSpan);

                            // check the runtime values
                            CheckRuntimeValues(targetSpan);

                            // check the bundle name
                            AssertTargetSpanAnyOf(targetSpan, TestTags.Bundle, TestBundleName);
                            AssertTargetSpanAnyOf(targetSpan, TestTags.Module, TestBundleName);

                            // check the suite name
                            var suite = AssertTargetSpanAnyOf(targetSpan, TestTags.Suite, _testSuiteNames);

                            // check the test type
                            AssertTargetSpanEqual(targetSpan, TestTags.Type, TestTags.TypeTest);

                            // check the test framework
                            AssertTargetSpanContains(targetSpan, TestTags.Framework, "NUnit");
                            targetSpan.Tags.Remove(TestTags.FrameworkVersion).Should().BeTrue();

                            // check the version
                            AssertTargetSpanEqual(targetSpan, "version", "1.0.0");

                            // checks the runtime id tag
                            AssertTargetSpanExists(targetSpan, Tags.RuntimeId);

                            // checks the source tags
                            AssertTargetSpanExists(targetSpan, TestTags.SourceFile);

                            // checks code owners
                            AssertTargetSpanExists(targetSpan, TestTags.CodeOwners);

                            // checks the origin tag
                            CheckOriginTag(targetSpan);

                            // Check the Environment
                            AssertTargetSpanEqual(targetSpan, Tags.Env, "integration_tests");

                            // Language
                            AssertTargetSpanEqual(targetSpan, Tags.Language, TracerConstants.Language);

                            // CI Library Language
                            AssertTargetSpanEqual(targetSpan, CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);

                            // Session data
                            AssertTargetSpanExists(targetSpan, TestTags.Command);
                            AssertTargetSpanExists(targetSpan, TestTags.CommandWorkingDirectory);

                            // Unskippable data
                            if (targetSpan.Tags[TestTags.Name] != "UnskippableTest")
                            {
                                AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.UnskippableTag, "false");
                                AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.ForcedRunTag, "false");
                            }

                            // check specific test span
                            switch (targetSpan.Tags[TestTags.Name])
                            {
                                case "SimplePassTest":
                                case "Test" when !suite.Contains("SetupError") && !suite.Contains("TearDownError"):
                                case "IsNull" when !suite.Contains("SetupError") && !suite.Contains("TearDownError"):
                                    CheckSimpleTestSpan(targetSpan);
                                    break;

                                case "SkipByITRSimulation":
                                    AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusSkip);
                                    AssertTargetSpanEqual(targetSpan, TestTags.SkipReason, IntelligentTestRunnerTags.SkippedByReason);
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "true");
                                    break;

                                case "SimpleSkipFromAttributeTest":
                                    CheckSimpleSkipFromAttributeTest(targetSpan);
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "SimpleErrorTest":
                                    CheckSimpleErrorTest(targetSpan);
                                    break;

                                case "TraitPassTest":
                                    CheckSimpleTestSpan(targetSpan);
                                    CheckTraitsValues(targetSpan);
                                    break;

                                case "TraitSkipFromAttributeTest":
                                    CheckSimpleSkipFromAttributeTest(targetSpan);
                                    CheckTraitsValues(targetSpan);
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "TraitErrorTest":
                                    CheckSimpleErrorTest(targetSpan);
                                    CheckTraitsValues(targetSpan);
                                    break;

                                case "SimpleParameterizedTest":
                                    CheckSimpleTestSpan(targetSpan);
                                    CheckParametrizedTraitsValues(targetSpan);
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(1,1,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(2,2,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(3,3,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleSkipParameterizedTest":
                                    CheckSimpleSkipFromAttributeTest(targetSpan);
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(1,1,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(2,2,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleSkipParameterizedTest(3,3,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "SimpleErrorParameterizedTest":
                                    CheckSimpleErrorTest(targetSpan);
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(1,0,2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(2,0,4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(3,0,6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleAssertPassTest":
                                    CheckSimpleTestSpan(targetSpan);
                                    break;

                                case "SimpleAssertInconclusive":
                                    CheckSimpleSkipFromAttributeTest(targetSpan, "The test is inconclusive.");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "Test" when suite.Contains("SetupError"):
                                case "Test01" when suite.Contains("SetupError"):
                                case "Test02" when suite.Contains("SetupError"):
                                case "Test03" when suite.Contains("SetupError"):
                                case "Test04" when suite.Contains("SetupError"):
                                case "Test05" when suite.Contains("SetupError"):
                                case "IsNull" when suite.Contains("SetupError") || suite.Contains("TearDownError"):
                                    CheckSetupOrTearDownErrorTest(targetSpan);
                                    break;

                                case "UnskippableTest":
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.UnskippableTag, "true");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.ForcedRunTag, "false");
                                    CheckSimpleTestSpan(targetSpan);
                                    break;
                            }

                            // check remaining tag (only the name)
                            targetSpan.Tags.Should().ContainSingle();

                            spans.Remove(targetSpan);
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Framework Version: " + new Version(FrameworkDescription.Instance.ProductVersion));
                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    Console.WriteLine("Package Version: " + new Version(packageVersion));
                }

                WriteSpans(spans);
                throw;
            }
        }

        protected override void CheckSimpleTestSpan(MockSpan targetSpan)
        {
            base.CheckSimpleTestSpan(targetSpan);

            // Check the `test.message` tag. We check if contains the default or the custom message.
            if (targetSpan.Tags.ContainsKey(TestTags.Message))
            {
                AssertTargetSpanAnyOf(targetSpan, TestTags.Message, ["Test is ok", "The test passed."]);
            }
        }

        private void CheckSetupOrTearDownErrorTest(MockSpan targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

            // Check the span error flag
            targetTest.Error.Should().Be(1);

            // Check the error type
            AssertTargetSpanAnyOf(targetTest, Tags.ErrorType, "SetUpException", "Exception", "System.Exception", "TearDownException");

            // Check the error message
            AssertTargetSpanAnyOf(targetTest, Tags.ErrorMsg, "SetUp exception.", "TearDown exception.");

            // Remove the stacktrace
            targetTest.Tags.Remove(Tags.ErrorStack);
        }

        private void CheckParametrizedTraitsValues(MockSpan targetTest)
        {
            // Check the traits tag value
            AssertTargetSpanAnyOf(
                targetTest,
                TestTags.Traits,
                "{\"Category\":[\"ParemeterizedTest\",\"FirstCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"SecondCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"ThirdCase\"]}");
        }
    }
}
