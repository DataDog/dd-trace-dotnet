// <copyright file="NUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class NUnitTests : TestHelper
    {
        private const int ExpectedSpanCount = 32;

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
        public void SubmitTraces(string packageVersion)
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
                    agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy/v2")).ToArray();
                    using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
                    {
                        spans = agent.WaitForSpans(ExpectedSpanCount)
                                     .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                                     .ToList();

                        // Check the span count
                        Assert.Equal(ExpectedSpanCount, spans.Count);

                        foreach (var targetSpan in spans.ToArray())
                        {
                            // Remove decision maker tag (not used by the backend for civisibility)
                            targetSpan.Tags.Remove(Tags.Propagated.DecisionMaker);

                            // Remove git metadata added by the apm agent writer.
                            targetSpan.Tags.Remove(Tags.GitCommitSha);
                            targetSpan.Tags.Remove(Tags.GitRepositoryUrl);

                            // check the name
                            Assert.Equal("nunit.test", targetSpan.Name);

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
                            Assert.True(targetSpan.Tags.Remove(TestTags.FrameworkVersion));

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
                                case "Test" when !suite.Contains("SetupError"):
                                case "IsNull" when !suite.Contains("SetupError"):
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
                                case "IsNull" when suite.Contains("SetupError"):
                                    CheckSetupErrorTest(targetSpan);
                                    break;

                                case "UnskippableTest":
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.UnskippableTag, "true");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.ForcedRunTag, "false");
                                    CheckSimpleTestSpan(targetSpan);
                                    break;
                            }

                            // check remaining tag (only the name)
                            Assert.Single(targetSpan.Tags);

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

        private static void WriteSpans(List<MockSpan> spans)
        {
            if (spans is null || spans.Count == 0)
            {
                return;
            }

            Console.WriteLine("***********************************");

            int i = 0;
            foreach (var span in spans)
            {
                Console.Write($" {i++}) ");
                Console.Write($"TraceId={span.TraceId}, ");
                Console.Write($"SpanId={span.SpanId}, ");
                Console.Write($"Service={span.Service}, ");
                Console.Write($"Name={span.Name}, ");
                Console.Write($"Resource={span.Resource}, ");
                Console.Write($"Type={span.Type}, ");
                Console.Write($"Error={span.Error}");
                Console.WriteLine();
                Console.WriteLine($"   Tags=");
                foreach (var kv in span.Tags)
                {
                    Console.WriteLine($"       => {kv.Key} = {kv.Value}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("***********************************");
        }

        private static string AssertTargetSpanAnyOf(MockSpan targetSpan, string key, params string[] values)
        {
            string actualValue = targetSpan.Tags[key];
            Assert.Contains(actualValue, values);
            targetSpan.Tags.Remove(key);
            return actualValue;
        }

        private static void AssertTargetSpanEqual(MockSpan targetSpan, string key, string value)
        {
            Assert.Equal(value, targetSpan.Tags[key]);
            targetSpan.Tags.Remove(key);
        }

        private static void AssertTargetSpanExists(MockSpan targetSpan, string key)
        {
            Assert.True(targetSpan.Tags.ContainsKey(key));
            targetSpan.Tags.Remove(key);
        }

        private static void AssertTargetSpanContains(MockSpan targetSpan, string key, string value)
        {
            Assert.Contains(value, targetSpan.Tags[key]);
            targetSpan.Tags.Remove(key);
        }

        private static void CheckCIEnvironmentValuesDecoration(MockSpan targetSpan)
        {
            var context = new SpanContext(parent: null, traceContext: null, serviceName: null);
            var span = new Span(context, DateTimeOffset.UtcNow);
            CIEnvironmentValues.Instance.DecorateSpan(span);

            AssertEqual(CommonTags.CIProvider);
            AssertEqual(CommonTags.CIPipelineId);
            AssertEqual(CommonTags.CIPipelineName);
            AssertEqual(CommonTags.CIPipelineNumber);
            AssertEqual(CommonTags.CIPipelineUrl);
            AssertEqual(CommonTags.CIJobUrl);
            AssertEqual(CommonTags.CIJobName);
            AssertEqual(CommonTags.StageName);
            AssertEqual(CommonTags.CIWorkspacePath);
            AssertEqual(CommonTags.GitRepository);
            AssertEqual(CommonTags.GitCommit);
            AssertEqual(CommonTags.GitBranch);
            AssertEqual(CommonTags.GitTag);
            AssertEqual(CommonTags.GitCommitAuthorName);
            AssertEqual(CommonTags.GitCommitAuthorEmail);
            AssertEqual(CommonTags.GitCommitAuthorDate);
            AssertEqual(CommonTags.GitCommitCommitterName);
            AssertEqual(CommonTags.GitCommitCommitterEmail);
            AssertEqual(CommonTags.GitCommitCommitterDate);
            AssertEqual(CommonTags.GitCommitMessage);
            AssertEqual(CommonTags.BuildSourceRoot);

            void AssertEqual(string key)
            {
                if (span.GetTag(key) is not null)
                {
                    Assert.Equal(span.GetTag(key), targetSpan.Tags[key]);
                    targetSpan.Tags.Remove(key);
                }
            }
        }

        private static void CheckRuntimeValues(MockSpan targetSpan)
        {
            AssertTargetSpanExists(targetSpan, CommonTags.RuntimeName);
            AssertTargetSpanExists(targetSpan, CommonTags.RuntimeVersion);
            AssertTargetSpanExists(targetSpan, CommonTags.RuntimeArchitecture);
            AssertTargetSpanExists(targetSpan, CommonTags.OSArchitecture);
            AssertTargetSpanExists(targetSpan, CommonTags.OSPlatform);
            AssertTargetSpanEqual(targetSpan, CommonTags.OSVersion, CIVisibility.GetOperatingSystemVersion());
        }

        private static void CheckTraitsValues(MockSpan targetSpan)
        {
            // Check the traits tag value
            AssertTargetSpanEqual(targetSpan, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
        }

        private static void CheckParametrizedTraitsValues(MockSpan targetTest)
        {
            // Check the traits tag value
            AssertTargetSpanAnyOf(
                targetTest,
                TestTags.Traits,
                "{\"Category\":[\"ParemeterizedTest\",\"FirstCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"SecondCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"ThirdCase\"]}");
        }

        private static void CheckOriginTag(MockSpan targetSpan)
        {
            // Check the test origin tag
            AssertTargetSpanEqual(targetSpan, Tags.Origin, TestTags.CIAppTestOriginName);
        }

        private static void CheckSimpleTestSpan(MockSpan targetSpan)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusPass);

            // Check the `test.message` tag. We check if contains the default or the custom message.
            if (targetSpan.Tags.ContainsKey(TestTags.Message))
            {
                AssertTargetSpanAnyOf(targetSpan, TestTags.Message, new string[] { "Test is ok", "The test passed." });
            }
        }

        private static void CheckSimpleSkipFromAttributeTest(MockSpan targetSpan, string skipReason = "Simple skip reason")
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusSkip);

            // Check the Test skip reason
            AssertTargetSpanEqual(targetSpan, TestTags.SkipReason, skipReason);
        }

        private static void CheckSimpleErrorTest(MockSpan targetSpan)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusFail);

            // Check the span error flag
            Assert.Equal(1, targetSpan.Error);

            // Check the error type
            AssertTargetSpanEqual(targetSpan, Tags.ErrorType, typeof(DivideByZeroException).FullName);

            // Check the error stack
            AssertTargetSpanContains(targetSpan, Tags.ErrorStack, typeof(DivideByZeroException).FullName);

            // Check the error message
            AssertTargetSpanEqual(targetSpan, Tags.ErrorMsg, new DivideByZeroException().Message);
        }

        private static void CheckSetupErrorTest(MockSpan targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

            // Check the span error flag
            Assert.Equal(1, targetTest.Error);

            // Check the error type
            AssertTargetSpanAnyOf(targetTest, Tags.ErrorType, "SetUpException", "Exception");

            // Check the error message
            AssertTargetSpanEqual(targetTest, Tags.ErrorMsg, "System.Exception : SetUp exception.");

            // Remove the stacktrace
            targetTest.Tags.Remove(Tags.ErrorStack);
        }
    }
}
