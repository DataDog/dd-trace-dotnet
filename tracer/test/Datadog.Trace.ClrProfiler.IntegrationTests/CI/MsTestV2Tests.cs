// <copyright file="MsTestV2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable SA1402

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public class MsTestV2Tests(ITestOutputHelper output) : MsTestV2TestsBase("MSTestTests", output, pre224TestCount: 20, post224TestCount: 22);

    public class MsTestV2Tests2(ITestOutputHelper output) : MsTestV2TestsBase("MSTestTests2", output, pre224TestCount: 19, post224TestCount: 21);

    [Collection("MsTestV2Tests")]
    [UsesVerify]
    public abstract class MsTestV2TestsBase : TestingFrameworkTest
    {
        private readonly GacFixture _gacFixture;

        public MsTestV2TestsBase(string sampleAppName, ITestOutputHelper output, int pre224TestCount, int post224TestCount)
            : base(sampleAppName, output)
        {
            TestBundleName = $"Samples.{sampleAppName}";
            TestSuiteName = "Samples.MSTestTests.TestSuite";
            ClassInitializationExceptionTestSuiteName = "Samples.MSTestTests.ClassInitializeExceptionTestSuite";
            Pre224TestCount = pre224TestCount;
            Post224TestCount = post224TestCount;
            SetServiceName("mstest-tests");
            SetServiceVersion("1.0.0");
            _gacFixture = new GacFixture();
            _gacFixture.AddAssembliesToGac();
        }

        protected virtual string TestSuiteName { get; }

        protected virtual string TestBundleName { get; }

        protected virtual string ClassInitializationExceptionTestSuiteName { get; }

        protected virtual int Pre224TestCount { get; }

        protected virtual int Post224TestCount { get; }

        public override void Dispose()
        {
            _gacFixture.RemoveAssembliesFromGac();
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MSTest), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "TestIntegrations")]
        public async Task SubmitTraces(string packageVersion)
        {
            var version = string.IsNullOrEmpty(packageVersion) ? new Version("2.2.3") : new Version(packageVersion);
            List<MockSpan> spans = null;
            var expectedSpanCount = version.CompareTo(new Version("2.2.3")) <= 0 ? Pre224TestCount : Post224TestCount;

            try
            {
                SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

                using (var agent = EnvironmentHelper.GetMockAgent())
                {
                    // We remove the evp_proxy endpoint to force the APM protocol compatibility
                    agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy/v2") && !e.Contains("evp_proxy/v4")).ToArray();
                    using (ProcessResult processResult = await RunDotnetTestSampleAndWaitForExit(agent, packageVersion: packageVersion))
                    {
                        spans = agent.WaitForSpans(expectedSpanCount)
                                     .Where(s => !(s.Tags.TryGetValue(Tags.InstrumentationName, out var sValue) && sValue == "HttpMessageHandler"))
                                     .ToList();

                        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings(expectedSpanCount == Pre224TestCount ? "pre_2_2_4" : "post_2_2_4");
                        settings.DisableRequireUniquePrefix();
                        await Verifier.Verify(spans.OrderBy(s => s.Resource).ThenBy(s => s.Tags.GetValueOrDefault(TestTags.Name)).ThenBy(s => s.Tags.GetValueOrDefault(TestTags.Parameters)), settings);

                        // Check the span count
                        Assert.Equal(expectedSpanCount, spans.Count);

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
                            Assert.Equal("mstestv2.test", targetSpan.Name);

                            // check the CIEnvironmentValues decoration.
                            CheckCIEnvironmentValuesDecoration(targetSpan);

                            // check the runtime values
                            CheckRuntimeValues(targetSpan);

                            // check the bundle name
                            AssertTargetSpanEqual(targetSpan, TestTags.Bundle, TestBundleName);
                            AssertTargetSpanEqual(targetSpan, TestTags.Module, TestBundleName);

                            // check the suite name
                            AssertTargetSpanAnyOf(targetSpan, TestTags.Suite, TestSuiteName, ClassInitializationExceptionTestSuiteName);

                            // check the test type
                            AssertTargetSpanEqual(targetSpan, TestTags.Type, TestTags.TypeTest);

                            // check the test framework
                            AssertTargetSpanContains(targetSpan, TestTags.Framework, "MSTestV2");
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
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"1\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"2\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"3\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "SimpleSkipParameterizedTest":
                                    CheckSimpleSkipFromAttributeTest(targetSpan);
                                    // On callsite the parameters tags are being sent with no parameters, this is not required due the whole test is skipped.
                                    // That behavior has changed in calltarget.
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"(default)\",\"yValue\":\"(default)\",\"expectedResult\":\"(default)\"}}");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.SkippedBy, "false");
                                    break;

                                case "SimpleErrorParameterizedTest":
                                    CheckSimpleErrorTest(targetSpan);
                                    AssertTargetSpanAnyOf(
                                        targetSpan,
                                        TestTags.Parameters,
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"1\",\"yValue\":\"0\",\"expectedResult\":\"2\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"2\",\"yValue\":\"0\",\"expectedResult\":\"4\"}}",
                                        "{\"metadata\":{},\"arguments\":{\"xValue\":\"3\",\"yValue\":\"0\",\"expectedResult\":\"6\"}}");
                                    break;

                                case "UnskippableTest":
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.UnskippableTag, "true");
                                    AssertTargetSpanEqual(targetSpan, IntelligentTestRunnerTags.ForcedRunTag, "false");
                                    CheckSimpleTestSpan(targetSpan);
                                    break;

                                case "ClassInitializeExceptionTestMethod":
                                    AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusFail);
                                    targetSpan.Error.Should().Be(1);
                                    AssertTargetSpanEqual(targetSpan, Tags.ErrorType, "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.TestFailedException");
                                    AssertTargetSpanContains(targetSpan, Tags.ErrorStack, "System.Exception: Class initialize exception");
                                    AssertTargetSpanContains(targetSpan, Tags.ErrorMsg, "Class initialize exception.");
                                    break;

                                case "My Custom: CustomTestMethodAttributeTest":
                                case "My Custom 2: CustomRenameTestMethodAttributeTest":
                                case "My Custom 3|1: CustomMultipleResultsTestMethodAttributeTest":
                                case "My Custom 3|2: CustomMultipleResultsTestMethodAttributeTest":
                                    AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusPass);
                                    break;
                            }

                            // check remaining tag (only the name)
                            Assert.Single(targetSpan.Tags);
                        }
                    }
                }
            }
            catch
            {
                WriteSpans(spans);
                throw;
            }
        }
    }

    [CollectionDefinition("MsTestV2Tests", DisableParallelization = true)]
    public class MsTestV2TestCollection
    {
    }
}
