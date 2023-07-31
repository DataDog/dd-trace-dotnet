// <copyright file="NUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class NUnitEvpTests : TestHelper
    {
        private const int ExpectedTestCount = 31;
        private const int ExpectedTestSuiteCount = 9;

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
        };

        public NUnitEvpTests(ITestOutputHelper output)
            : base("NUnitTests", output)
        {
            SetServiceName("nunit-tests-evp");
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

            try
            {
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.ForceAgentsEvpProxy, "1");

                using (var agent = EnvironmentHelper.GetMockAgent())
                {
                    agent.EventPlatformProxyPayloadReceived += (sender, e) =>
                    {
                        if (e.Value.PathAndQuery != "/evp_proxy/v2/api/v2/citestcycle")
                        {
                            return;
                        }

                        var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                        if (payload.Events?.Length > 0)
                        {
                            foreach (var @event in payload.Events)
                            {
                                if (@event.Type == SpanTypes.Test)
                                {
                                    var testObject = JsonConvert.DeserializeObject<MockCIVisibilityTest>(@event.Content.ToString());
                                    Output.WriteLine($"Test: {testObject.Meta[TestTags.Suite]}.{testObject.Meta[TestTags.Name]} | {testObject.Meta[TestTags.Status]}");
                                    tests.Add(testObject);
                                }
                                else if (@event.Type == SpanTypes.TestSuite)
                                {
                                    var suiteObject = JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(@event.Content.ToString());
                                    Output.WriteLine($"Suite: {suiteObject.Meta[TestTags.Suite]} | {suiteObject.Meta[TestTags.Status]}");
                                    testSuites.Add(suiteObject);
                                }
                                else if (@event.Type == SpanTypes.TestModule)
                                {
                                    var moduleObject = JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(@event.Content.ToString());
                                    Output.WriteLine($"Module: {moduleObject.Meta[TestTags.Module]} | {moduleObject.Meta[TestTags.Status]}");
                                    testModules.Add(moduleObject);
                                }
                            }
                        }
                    };

                    using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
                    {
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

                            // check the name
                            Assert.Equal("nunit.test", targetTest.Name);

                            // check the CIEnvironmentValues decoration.
                            CheckCIEnvironmentValuesDecoration(targetTest);

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

                            // check specific test span
                            switch (targetTest.Meta[TestTags.Name])
                            {
                                case "SimplePassTest":
                                case "Test" when !suite.Contains("SetupError"):
                                case "IsNull" when !suite.Contains("SetupError"):
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
                                case "IsNull" when suite.Contains("SetupError"):
                                    CheckSetupErrorTest(targetTest);
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

        private static string AssertTargetSpanAnyOf(MockCIVisibilityTest targetTest, string key, params string[] values)
        {
            string actualValue = targetTest.Meta[key];
            Assert.Contains(actualValue, values);
            targetTest.Meta.Remove(key);
            return actualValue;
        }

        private static void AssertTargetSpanEqual(MockCIVisibilityTest targetTest, string key, string value)
        {
            Assert.Equal(value, targetTest.Meta[key]);
            targetTest.Meta.Remove(key);
        }

        private static void AssertTargetSpanExists(MockCIVisibilityTest targetTest, string key)
        {
            Assert.True(targetTest.Meta.ContainsKey(key));
            targetTest.Meta.Remove(key);
        }

        private static void AssertTargetSpanContains(MockCIVisibilityTest targetTest, string key, string value)
        {
            Assert.Contains(value, targetTest.Meta[key]);
            targetTest.Meta.Remove(key);
        }

        private static void CheckCIEnvironmentValuesDecoration(MockCIVisibilityTest targetTest)
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
            AssertEqualDate(CommonTags.GitCommitAuthorDate);
            AssertEqual(CommonTags.GitCommitCommitterName);
            AssertEqual(CommonTags.GitCommitCommitterEmail);
            AssertEqualDate(CommonTags.GitCommitCommitterDate);
            AssertEqual(CommonTags.GitCommitMessage);
            AssertEqual(CommonTags.BuildSourceRoot);

            void AssertEqual(string key)
            {
                if (span.GetTag(key) is { } keyValue)
                {
                    Assert.Equal(keyValue, targetTest.Meta[key]);
                    targetTest.Meta.Remove(key);
                }
            }

            void AssertEqualDate(string key)
            {
                if (span.GetTag(key) is { } keyValue)
                {
                    Assert.Equal(DateTimeOffset.Parse(keyValue), DateTimeOffset.Parse(targetTest.Meta[key]));
                    targetTest.Meta.Remove(key);
                }
            }
        }

        private static void CheckRuntimeValues(MockCIVisibilityTest targetTest)
        {
            AssertTargetSpanExists(targetTest, CommonTags.RuntimeName);
            AssertTargetSpanExists(targetTest, CommonTags.RuntimeVersion);
            AssertTargetSpanExists(targetTest, CommonTags.RuntimeArchitecture);
            AssertTargetSpanExists(targetTest, CommonTags.OSArchitecture);
            AssertTargetSpanExists(targetTest, CommonTags.OSPlatform);
            AssertTargetSpanEqual(targetTest, CommonTags.OSVersion, CIVisibility.GetOperatingSystemVersion());
        }

        private static void CheckTraitsValues(MockCIVisibilityTest targetTest)
        {
            // Check the traits tag value
            AssertTargetSpanEqual(targetTest, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
        }

        private static void CheckParametrizedTraitsValues(MockCIVisibilityTest targetTest)
        {
            // Check the traits tag value
            AssertTargetSpanAnyOf(
                targetTest,
                TestTags.Traits,
                "{\"Category\":[\"ParemeterizedTest\",\"FirstCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"SecondCase\"]}",
                "{\"Category\":[\"ParemeterizedTest\",\"ThirdCase\"]}");
        }

        private static void CheckOriginTag(MockCIVisibilityTest targetTest)
        {
            // Check the test origin tag
            AssertTargetSpanEqual(targetTest, Tags.Origin, TestTags.CIAppTestOriginName);
        }

        private static void CheckSimpleTestSpan(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusPass);

            // Check the `test.message` tag. We check if contains the default or the custom message.
            if (targetTest.Meta.ContainsKey(TestTags.Message))
            {
                AssertTargetSpanAnyOf(targetTest, TestTags.Message, new string[] { "Test is ok", "The test passed." });
            }
        }

        private static void CheckSimpleSkipFromAttributeTest(MockCIVisibilityTest targetTest, string skipReason = "Simple skip reason")
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);

            // Check the Test skip reason
            AssertTargetSpanEqual(targetTest, TestTags.SkipReason, skipReason);
        }

        private static void CheckSimpleErrorTest(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

            // Check the span error flag
            Assert.Equal(1, targetTest.Error);

            // Check the error type
            AssertTargetSpanEqual(targetTest, Tags.ErrorType, typeof(DivideByZeroException).FullName);

            // Check the error stack
            AssertTargetSpanContains(targetTest, Tags.ErrorStack, typeof(DivideByZeroException).FullName);

            // Check the error message
            AssertTargetSpanEqual(targetTest, Tags.ErrorMsg, new DivideByZeroException().Message);
        }

        private static void CheckSetupErrorTest(MockCIVisibilityTest targetTest)
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
            targetTest.Meta.Remove(Tags.ErrorStack);
        }

        private void WriteSpans(List<MockCIVisibilityTestSuite> suites)
        {
            if (suites is null || suites.Count == 0)
            {
                return;
            }

            var sb = StringBuilderCache.Acquire(250);
            sb.AppendLine("***********************************");

            int i = 0;
            foreach (var suite in suites)
            {
                sb.Append($" {i++}) ");
                sb.Append($"TestSuiteId={suite.TestSuiteId}, ");
                sb.Append($"Service={suite.Service}, ");
                sb.Append($"Name={suite.Name}, ");
                sb.Append($"Resource={suite.Resource}, ");
                sb.Append($"Type={suite.Type}, ");
                sb.Append($"Error={suite.Error}");
                sb.AppendLine();
                sb.AppendLine("   Tags=");
                foreach (var kv in suite.Meta)
                {
                    sb.AppendLine($"       => {kv.Key} = {kv.Value}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("***********************************");
            Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
        }

        private void WriteSpans(List<MockCIVisibilityTest> tests)
        {
            if (tests is null || tests.Count == 0)
            {
                return;
            }

            var sb = StringBuilderCache.Acquire(250);
            sb.AppendLine("***********************************");

            int i = 0;
            foreach (var test in tests)
            {
                sb.Append($" {i++}) ");
                sb.Append($"TraceId={test.TraceId}, ");
                sb.Append($"SpanId={test.SpanId}, ");
                sb.Append($"Service={test.Service}, ");
                sb.Append($"Name={test.Name}, ");
                sb.Append($"Resource={test.Resource}, ");
                sb.Append($"Type={test.Type}, ");
                sb.Append($"Error={test.Error}");
                sb.AppendLine();
                sb.AppendLine("   Tags=");
                foreach (var kv in test.Meta)
                {
                    sb.AppendLine($"       => {kv.Key} = {kv.Value}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("***********************************");
            Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
        }
    }
}
