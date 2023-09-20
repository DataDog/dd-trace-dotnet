// <copyright file="MsTestV2EvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class MsTestV2EvpTests : TestHelper
    {
        private const string TestSuiteName = "Samples.MSTestTests.TestSuite";
        private const string TestBundleName = "Samples.MSTestTests";

        public MsTestV2EvpTests(ITestOutputHelper output)
            : base("MSTestTests", output)
        {
            SetServiceName("mstest-tests-evp");
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MSTest), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public void SubmitTraces(string packageVersion)
        {
            var version = string.IsNullOrEmpty(packageVersion) ? new Version("2.2.8") : new Version(packageVersion);
            var tests = new List<MockCIVisibilityTest>();
            var testSuites = new List<MockCIVisibilityTestSuite>();
            var testModules = new List<MockCIVisibilityTestModule>();
            var expectedTestCount = version.CompareTo(new Version("2.2.5")) < 0 ? 14 : 16;

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
                                    tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(@event.Content.ToString()));
                                }
                                else if (@event.Type == SpanTypes.TestSuite)
                                {
                                    testSuites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(@event.Content.ToString()));
                                }
                                else if (@event.Type == SpanTypes.TestModule)
                                {
                                    testModules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(@event.Content.ToString()));
                                }
                            }
                        }
                    };

                    using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
                    {
                        // Check the tests, suites and modules count
                        Assert.Equal(expectedTestCount, tests.Count);
                        Assert.Single(testSuites);
                        Assert.Single(testModules);
                        var testSuite = testSuites[0];
                        var testModule = testModules[0];

                        // Check Suite
                        Assert.True(tests.All(t => t.TestSuiteId == testSuite.TestSuiteId));
                        Assert.True(testSuite.TestModuleId == testModule.TestModuleId);

                        // ITR tags inside the test suite
                        testSuite.Metrics.Should().Contain(IntelligentTestRunnerTags.SkippingCount, 1);

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

                            // check the name
                            Assert.Equal("mstestv2.test", targetTest.Name);

                            // check the CIEnvironmentValues decoration.
                            CheckCIEnvironmentValuesDecoration(targetTest);

                            // check the runtime values
                            CheckRuntimeValues(targetTest);

                            // check the bundle name
                            AssertTargetSpanEqual(targetTest, TestTags.Bundle, TestBundleName);
                            AssertTargetSpanEqual(targetTest, TestTags.Module, TestBundleName);

                            // check the suite name
                            AssertTargetSpanEqual(targetTest, TestTags.Suite, TestSuiteName);

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
                            AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.UnskippableTag, "false");
                            AssertTargetSpanEqual(targetTest, IntelligentTestRunnerTags.ForcedRunTag, "false");

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

        private static void WriteSpans(List<MockCIVisibilityTest> tests)
        {
            if (tests is null || tests.Count == 0)
            {
                return;
            }

            Console.WriteLine("***********************************");

            int i = 0;
            foreach (var test in tests)
            {
                Console.Write($" {i++}) ");
                Console.Write($"TraceId={test.TraceId}, ");
                Console.Write($"SpanId={test.SpanId}, ");
                Console.Write($"Service={test.Service}, ");
                Console.Write($"Name={test.Name}, ");
                Console.Write($"Resource={test.Resource}, ");
                Console.Write($"Type={test.Type}, ");
                Console.Write($"Error={test.Error}");
                Console.WriteLine();
                Console.WriteLine($"   Tags=");
                foreach (var kv in test.Meta)
                {
                    Console.WriteLine($"       => {kv.Key} = {kv.Value}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("***********************************");
        }

        private static void AssertTargetSpanAnyOf(MockCIVisibilityTest targetTest, string key, params string[] values)
        {
            string actualValue = targetTest.Meta[key];
            Assert.Contains(actualValue, values);
            targetTest.Meta.Remove(key);
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

        private static void CheckOriginTag(MockCIVisibilityTest targetTest)
        {
            // Check the test origin tag
            AssertTargetSpanEqual(targetTest, Tags.Origin, TestTags.CIAppTestOriginName);
        }

        private static void CheckSimpleTestSpan(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusPass);
        }

        private static void CheckSimpleSkipFromAttributeTest(MockCIVisibilityTest targetTest)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);

            // Check the Test skip reason
            AssertTargetSpanEqual(targetTest, TestTags.SkipReason, "Simple skip reason");
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
    }
}
