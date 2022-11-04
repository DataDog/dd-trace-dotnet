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
        private const int ExpectedTestCount = 20;
        private const int ExpectedTestSuiteCount = 4;

        private const string TestBundleName = "Samples.NUnitTests";
        private static string[] _testSuiteNames = new string[]
        {
            "Samples.NUnitTests.TestSuite",
            "Samples.NUnitTests.TestFixtureTest(\"Test01\")",
            "Samples.NUnitTests.TestFixtureTest(\"Test02\")",
            "Samples.NUnitTests.TestString",
        };

        public NUnitEvpTests(ITestOutputHelper output)
            : base("NUnitTests", output)
        {
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
            var sessionId = SpanIdGenerator.CreateNew();
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
                        Assert.Equal(ExpectedTestCount, tests.Count);
                        Assert.Equal(ExpectedTestSuiteCount, testSuites.Count);
                        Assert.Single(testModules);

                        // Check suites
                        Assert.True(tests.All(t => testSuites.Find(s => s.TestSuiteId == t.TestSuiteId) != null));
                        Assert.True(tests.All(t => t.TestModuleId == testModules[0].TestModuleId));

                        // Check Module
                        Assert.True(tests.All(t => t.TestModuleId == testSuites[0].TestModuleId));

                        // Check Session
                        tests.Should().OnlyContain(t => t.TestSessionId == testSuites[0].TestSessionId);
                        testSuites[0].TestSessionId.Should().Be(testModules[0].TestSessionId);
                        testModules[0].TestSessionId.Should().Be(sessionId);

                        foreach (var targetTest in tests)
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
                            AssertTargetSpanAnyOf(targetTest, TestTags.Suite, _testSuiteNames);

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
                                case "Test":
                                case "IsNull":
                                    CheckSimpleTestSpan(targetTest);
                                    break;

                                case "SimpleSkipFromAttributeTest":
                                    CheckSimpleSkipFromAttributeTest(targetTest);
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
                Console.WriteLine("Framework Version: " + new Version(FrameworkDescription.Instance.ProductVersion));
                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    Console.WriteLine("Package Version: " + new Version(packageVersion));
                }

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
            var context = new SpanContext(null, null, null, null);
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
            FrameworkDescription framework = FrameworkDescription.Instance;

            AssertTargetSpanEqual(targetTest, CommonTags.RuntimeName, framework.Name);
            AssertTargetSpanEqual(targetTest, CommonTags.RuntimeVersion, framework.ProductVersion);
            AssertTargetSpanEqual(targetTest, CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            AssertTargetSpanEqual(targetTest, CommonTags.OSArchitecture, framework.OSArchitecture);
            AssertTargetSpanEqual(targetTest, CommonTags.OSPlatform, framework.OSPlatform);
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
    }
}
