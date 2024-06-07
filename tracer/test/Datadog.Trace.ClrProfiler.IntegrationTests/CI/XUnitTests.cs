// <copyright file="XUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    [UsesVerify]
    public abstract class XUnitTests : TestingFrameworkTest
    {
        private const string TestBundleName = "Samples.XUnitTests";
        private const string TestSuiteName = "Samples.XUnitTests.TestSuite";
        private const string UnSkippableSuiteName = "Samples.XUnitTests.UnSkippableSuite";
        private const int ExpectedSpanCount = 16;

        public XUnitTests(ITestOutputHelper output)
            : base("XUnitTests", output)
        {
            SetServiceName("xunit-tests");
            SetServiceVersion("1.0.0");
        }

        public virtual async Task SubmitTraces(string packageVersion)
        {
            string[] messages = null;
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

            using var logsIntake = new MockLogsIntakeForCiVisibility();
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.XUnit), nameof(XUnitTests));
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

            using var agent = EnvironmentHelper.GetMockAgent();

            // We remove the evp_proxy endpoint to force the APM protocol compatibility
            agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy/v2") && !e.Contains("evp_proxy/v4")).ToArray();
            using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(ExpectedSpanCount)
                         .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                         .ToList();
            var spansCopy = JsonConvert.DeserializeObject<List<MockSpan>>(JsonConvert.SerializeObject(spans));

            // Check the span count
            Assert.Equal(ExpectedSpanCount, spans.Count);

            // ***************************************************************************

            try
            {
                foreach (var targetSpan in spans)
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
                    Assert.Equal("xunit.test", targetSpan.Name);

                    // check the CIEnvironmentValues decoration.
                    CheckCIEnvironmentValuesDecoration(targetSpan);

                    // check the runtime values
                    CheckRuntimeValues(targetSpan);

                    // check the bundle name
                    AssertTargetSpanEqual(targetSpan, TestTags.Bundle, TestBundleName);
                    AssertTargetSpanEqual(targetSpan, TestTags.Module, TestBundleName);

                    // check the suite name
                    AssertTargetSpanAnyOf(targetSpan, TestTags.Suite, TestSuiteName, UnSkippableSuiteName);

                    // check the test type
                    AssertTargetSpanEqual(targetSpan, TestTags.Type, TestTags.TypeTest);

                    // check the test framework
                    AssertTargetSpanContains(targetSpan, TestTags.Framework, "xUnit");
                    Assert.True(targetSpan.Tags.Remove(TestTags.FrameworkVersion));

                    // check the version
                    AssertTargetSpanEqual(targetSpan, "version", "1.0.0");

                    // checks the origin tag
                    CheckOriginTag(targetSpan);

                    // checks the runtime id tag
                    AssertTargetSpanExists(targetSpan, Tags.RuntimeId);

                    // checks the source tags
                    AssertTargetSpanExists(targetSpan, TestTags.SourceFile);

                    // checks code owners
                    AssertTargetSpanExists(targetSpan, TestTags.CodeOwners);

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
                            CheckSimpleTestSpan(targetSpan);
                            break;

                        case "SimpleSkipFromAttributeTest":
                            CheckSimpleSkipFromAttributeTest(targetSpan);
                            AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                            break;

                        case "SkipByITRSimulation":
                            AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusSkip);
                            AssertTargetSpanEqual(targetSpan, TestTags.SkipReason, IntelligentTestRunnerTags.SkippedByReason);
                            AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "true");
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
                            AssertTargetSpanAnyOf(
                                targetSpan,
                                TestTags.Parameters,
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 1, yValue: 1, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 2, yValue: 2, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleParameterizedTest(xValue: 3, yValue: 3, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 1, yValue: 1, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 2, yValue: 2, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleParameterizedTest(xValue: 3, yValue: 3, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                            break;

                        case "SimpleSkipParameterizedTest":
                            CheckSimpleSkipFromAttributeTest(targetSpan);
                            AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                            break;

                        case "SimpleErrorParameterizedTest":
                            CheckSimpleErrorTest(targetSpan);
                            AssertTargetSpanAnyOf(
                                targetSpan,
                                TestTags.Parameters,
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 1, yValue: 0, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 2, yValue: 0, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                "{\"metadata\":{\"test_name\":\"Samples.XUnitTests.TestSuite.SimpleErrorParameterizedTest(xValue: 3, yValue: 0, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 1, yValue: 0, expectedResult: 2)\"},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 2, yValue: 0, expectedResult: 4)\"},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                "{\"metadata\":{\"test_name\":\"SimpleErrorParameterizedTest(xValue: 3, yValue: 0, expectedResult: 6)\"},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                            break;

                        case "UnskippableTest":
                            AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.UnskippableTag, "true");
                            AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.ForcedRunTag, "false");
                            CheckSimpleTestSpan(targetSpan);
                            break;
                    }

                    // check remaining tag (only the name)
                    Assert.Single(targetSpan.Tags);
                }
            }
            catch
            {
                WriteSpans(spans);
                throw;
            }

            // Snapshot testing
            var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings("all");
            settings.DisableRequireUniquePrefix();
            settings.UseTypeName(nameof(XUnitTests));
            await Verifier.Verify(spansCopy.OrderBy(s => s.Resource).ThenBy(s => s.Tags.GetValueOrDefault(TestTags.Parameters)), settings);

            // ***************************************************************************
            // Check logs
            messages = logsIntake.Logs.Select(i => i.Message).Where(m => m.StartsWith("Test:")).ToArray();

            Assert.Contains(messages, m => m.StartsWith("Test:SimplePassTest"));
            Assert.Contains(messages, m => m.StartsWith("Test:SimpleErrorTest"));
            Assert.Contains(messages, m => m.StartsWith("Test:TraitPassTest"));
            Assert.Contains(messages, m => m.StartsWith("Test:TraitErrorTest"));
            Assert.Contains(messages, m => m.StartsWith("Test:SimpleParameterizedTest"));
            Assert.Contains(messages, m => m.StartsWith("Test:SimpleErrorParameterizedTest"));
        }

        private class MockLogsIntakeForCiVisibility : MockLogsIntake<MockLogsIntakeForCiVisibility.Log>
        {
            public class Log
            {
                [JsonProperty("ddsource")]
                public string Source { get; set; }

                [JsonProperty("hostname")]
                public string Hostname { get; set; }

                [JsonProperty("timestamp")]
                public long Timestamp { get; set; }

                [JsonProperty("message")]
                public string Message { get; set; }

                [JsonProperty("status")]
                public string Status { get; set; }

                [JsonProperty("service")]
                public string Service { get; set; }

                [JsonProperty("dd.trace_id")]
                public string TraceId { get; set; }

                [JsonProperty(TestTags.Suite)]
                public string TestSuite { get; set; }

                [JsonProperty(TestTags.Name)]
                public string TestName { get; set; }

                [JsonProperty(TestTags.Bundle)]
                public string TestBundle { get; set; }

                [JsonProperty("ddtags")]
                public string Tags { get; set; }
            }
        }
    }
}
