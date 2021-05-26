// <copyright file="NUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.Ci;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class NUnitTests : TestHelper
    {
        private const string TestSuiteName = "Samples.NUnitTests.TestSuite";
        private const int ExpectedSpanCount = 15;

        public NUnitTests(ITestOutputHelper output)
            : base("NUnitTests", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetData()
        {
            foreach (object[] item in PackageVersions.NUnit)
            {
                yield return item.Concat(false);
                yield return item.Concat(true);
            }
        }

        [Theory]
        [MemberData(nameof(GetData))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public void SubmitTraces(string packageVersion, bool enableCallTarget)
        {
            if (new Version(FrameworkDescription.Instance.ProductVersion).Major >= 5)
            {
                if (new Version(packageVersion) < new Version("3.13"))
                {
                    // Ignore due https://github.com/nunit/nunit/issues/3565#issuecomment-726835235
                    return;
                }
            }

            List<MockTracerAgent.Span> spans = null;
            try
            {
                SetCallTargetSettings(enableCallTarget);
                SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
                SetEnvironmentVariable("DD_DUMP_ILREWRITE_ENABLED", "1");

                int agentPort = TcpPortProvider.GetOpenPort();

                using (var agent = new MockTracerAgent(agentPort))
                using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
                {
                    spans = agent.WaitForSpans(ExpectedSpanCount)
                        .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                        .ToList();

                    // Check the span count
                    Assert.Equal(ExpectedSpanCount, spans.Count);

                    foreach (var targetSpan in spans)
                    {
                        // check the name
                        Assert.Equal("nunit.test", targetSpan.Name);

                        // check the CIEnvironmentValues decoration.
                        CheckCIEnvironmentValuesDecoration(targetSpan);

                        // check the runtime values
                        CheckRuntimeValues(targetSpan);

                        // check the suite name
                        AssertTargetSpanEqual(targetSpan, TestTags.Suite, TestSuiteName);

                        // check the test type
                        AssertTargetSpanEqual(targetSpan, TestTags.Type, TestTags.TypeTest);

                        // check the test framework
                        AssertTargetSpanContains(targetSpan, TestTags.Framework, "NUnit");

                        // check the version
                        AssertTargetSpanEqual(targetSpan, "version", "1.0.0");

                        // checks the origin tag
                        CheckOriginTag(targetSpan);

                        // Check the Environment
                        AssertTargetSpanEqual(targetSpan, Tags.Env, "integration_tests");

                        // check specific test span
                        switch (targetSpan.Tags[TestTags.Name])
                        {
                            case "SimplePassTest":
                                CheckSimpleTestSpan(targetSpan);
                                break;

                            case "SimpleSkipFromAttributeTest":
                                CheckSimpleSkipFromAttributeTest(targetSpan);
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
                                break;

                            case "TraitErrorTest":
                                CheckSimpleErrorTest(targetSpan);
                                CheckTraitsValues(targetSpan);
                                break;

                            case "SimpleParameterizedTest":
                                CheckSimpleTestSpan(targetSpan);
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
                        }

                        // check remaining tag (only the name)
                        Assert.Single(targetSpan.Tags);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Framework Version: " + new Version(FrameworkDescription.Instance.ProductVersion));
                Console.WriteLine("Package Version: " + new Version(packageVersion));
                WriteSpans(spans);
                throw;
            }
        }

        private static void WriteSpans(List<MockTracerAgent.Span> spans)
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

        private static void AssertTargetSpanAnyOf(MockTracerAgent.Span targetSpan, string key, params string[] values)
        {
            string actualValue = targetSpan.Tags[key];
            Assert.Contains(actualValue, values);
            targetSpan.Tags.Remove(key);
        }

        private static void AssertTargetSpanEqual(MockTracerAgent.Span targetSpan, string key, string value)
        {
            Assert.Equal(value, targetSpan.Tags[key]);
            targetSpan.Tags.Remove(key);
        }

        private static void AssertTargetSpanContains(MockTracerAgent.Span targetSpan, string key, string value)
        {
            Assert.Contains(value, targetSpan.Tags[key]);
            targetSpan.Tags.Remove(key);
        }

        private static void CheckCIEnvironmentValuesDecoration(MockTracerAgent.Span targetSpan)
        {
            var context = new SpanContext(null, null, null, null);
            var span = new Span(context, DateTimeOffset.UtcNow);
            CIEnvironmentValues.DecorateSpan(span);

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

        private static void CheckRuntimeValues(MockTracerAgent.Span targetSpan)
        {
            FrameworkDescription framework = FrameworkDescription.Instance;

            AssertTargetSpanEqual(targetSpan, CommonTags.RuntimeName, framework.Name);
            AssertTargetSpanEqual(targetSpan, CommonTags.RuntimeVersion, framework.ProductVersion);
            AssertTargetSpanEqual(targetSpan, CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            AssertTargetSpanEqual(targetSpan, CommonTags.OSArchitecture, framework.OSArchitecture);
            AssertTargetSpanEqual(targetSpan, CommonTags.OSPlatform, framework.OSPlatform);
            AssertTargetSpanEqual(targetSpan, CommonTags.OSVersion, Environment.OSVersion.VersionString);
        }

        private static void CheckTraitsValues(MockTracerAgent.Span targetSpan)
        {
            // Check the traits tag value
            AssertTargetSpanEqual(targetSpan, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
        }

        private static void CheckOriginTag(MockTracerAgent.Span targetSpan)
        {
            // Check the test origin tag
            AssertTargetSpanEqual(targetSpan, Tags.Origin, TestTags.CIAppTestOriginName);
        }

        private static void CheckSimpleTestSpan(MockTracerAgent.Span targetSpan)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusPass);
        }

        private static void CheckSimpleSkipFromAttributeTest(MockTracerAgent.Span targetSpan)
        {
            // Check the Test Status
            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusSkip);

            // Check the Test skip reason
            AssertTargetSpanEqual(targetSpan, TestTags.SkipReason, "Simple skip reason");
        }

        private static void CheckSimpleErrorTest(MockTracerAgent.Span targetSpan)
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
    }
}
