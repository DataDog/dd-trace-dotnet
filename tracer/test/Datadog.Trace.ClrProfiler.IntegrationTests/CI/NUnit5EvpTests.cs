// <copyright file="NUnit5EvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if (NET8_0_OR_GREATER || NETFRAMEWORK) && !DEFAULT_SAMPLES

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
public class NUnit5EvpTests : TestingFrameworkEvpTest
{
    public NUnit5EvpTests(ITestOutputHelper output)
        : base("NUnitTests", output)
    {
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task ReportsSkippedFixtureAndPreservesPrerequisites(
        [PackageVersionData(nameof(PackageVersions.NUnit), minInclusive: "5.0.0")] string packageVersion,
        [CombinatorialValues("evp_proxy/v2", "evp_proxy/v4")] string evpVersionToRemove)
    {
        const string SkippableTestsJson = """
                                          {"data":[
                                            {"type":"test","attributes":{"name":"PassedPrerequisite","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                                            {"type":"test","attributes":{"name":"PassedDependent","suite":"Samples.NUnit5Tests.DependenciesFixture"}},
                                            {"type":"test","attributes":{"name":"Independent","suite":"Samples.NUnit5Tests.DependenciesFixture"}}
                                          ],"meta":{"correlation_id":"nunit5-dependencies"}}
                                          """;

        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                ExpectsGzip(evpVersionToRemove),
                new TestScenario(
                    nameof(NUnit5EvpTests),
                    "dependencies_with_itr",
                    new MockData(GetSettingsJson("false", "true", "false", "0"), string.Empty, string.Empty, SkippableTestsJson),
                    expectedExitCode: 1,
                    expectedSpans: 19,
                    useSnapshot: false,
                    (in ExecutionData data) =>
                    {
                        var tests = data.Tests;
                        data.TestSuites.Should().HaveCount(4);
                        data.TestModules.Should().ContainSingle();
                        tests.Select(t => t.SpanId).Should().OnlyHaveUniqueItems();
                        var skippedSuite = data.TestSuites.Single(s => s.Meta[TestTags.Suite] == "Samples.NUnit5Tests.DependentFixture");
                        skippedSuite.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
                        var skippedCases = tests.Where(t => t.Meta[TestTags.Suite] == "Samples.NUnit5Tests.DependentFixture").ToArray();
                        skippedCases.Should().HaveCount(3);
                        skippedCases.Should().OnlyContain(t => t.TestSuiteId == skippedSuite.TestSuiteId && t.Meta[TestTags.Status] == TestTags.StatusSkip);
                        skippedCases.Should().OnlyContain(t => t.Meta[TestTags.SkipReason].StartsWith("Dependency on "));

                        foreach (var name in new[] { "PassedPrerequisite", "PassedDependent" })
                        {
                            tests.Single(t => t.Meta[TestTags.Name] == name).Meta[TestTags.Status].Should().Be(TestTags.StatusPass);
                        }

                        var independent = tests.Single(t => t.Meta[TestTags.Name] == "Independent");
                        independent.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
                        independent.Meta[IntelligentTestRunnerTags.SkippedBy].Should().Be("true");
                        data.TestModules.Single().Metrics[IntelligentTestRunnerTags.SkippingCount].Should().Be(1);
                    },
                    useDotnetExec: false,
                    arguments: $"/TestCaseFilter:FullyQualifiedName~Samples.NUnit5Tests {NUnit5Tests.RunFixturesSequentially}"))
           .ConfigureAwait(false);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    public async Task QuarantineHidesOnlyFailuresFromNUnitDependencies(
        [PackageVersionData(nameof(PackageVersions.NUnit), minInclusive: "5.0.0")] string packageVersion,
        [CombinatorialValues("evp_proxy/v2", "evp_proxy/v4")] string evpVersionToRemove,
        bool attemptToFix)
    {
        const string TestManagementJson = """
                                          {"data":{"id":"nunit5-quarantine","type":"ci_app_libraries_tests","attributes":{"modules":{"Samples.NUnitTests":{"suites":{
                                            "Samples.NUnit5Tests.DependenciesFixture":{"tests":{"PassedPrerequisite":{"properties":PROPERTIES}}},
                                            "Samples.NUnit5Tests.FailingFixture":{"tests":{"WarningBeforeFailure":{"properties":PROPERTIES},"Failed":{"properties":PROPERTIES}}}
                                          }}}}}}
                                          """;
        var properties = attemptToFix ? """{"quarantined":true,"attempt_to_fix":true}""" : """{"quarantined":true}""";
        const string Filter = "FullyQualifiedName=Samples.NUnit5Tests.DependenciesFixture.PassedDependent|FullyQualifiedName~Samples.NUnit5Tests.DependentFixture.SkippedFixtureCase";

        // Attempt to fix runs each of the three test management tests twice.
        var executions = attemptToFix ? 2 : 1;

        // The adapter receives no individual results from the blocked fixture or from quarantined prerequisites, so it reports exit code zero.
        await ExecuteTestAsync(
                packageVersion,
                evpVersionToRemove,
                ExpectsGzip(evpVersionToRemove),
                new TestScenario(
                    nameof(NUnit5EvpTests),
                    attemptToFix ? "dependencies_with_attempt_to_fix" : "dependencies_with_quarantine",
                    new MockData(GetSettingsJson("false", "false", "true", attemptToFix ? "2" : "0"), string.Empty, TestManagementJson.Replace("PROPERTIES", properties)),
                    expectedExitCode: 0,
                    expectedSpans: 3 + (3 * executions),
                    useSnapshot: false,
                    (in ExecutionData data) =>
                    {
                        var tests = data.Tests;
                        foreach (var name in new[] { "PassedPrerequisite", "WarningBeforeFailure", "Failed" })
                        {
                            var testExecutions = tests.Where(t => t.Meta[TestTags.Name] == name).ToArray();
                            testExecutions.Should().HaveCount(executions);
                            testExecutions.Should().OnlyContain(t => t.Meta[TestTags.TestIsQuarantined] == "true");
                            testExecutions.Should().OnlyContain(t => !attemptToFix || t.Meta[TestTags.TestIsAttemptToFix] == "true");
                        }

                        tests.Where(t => t.Meta[TestTags.Name] == "PassedPrerequisite").Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
                        tests.Single(t => t.Meta[TestTags.Name] == "PassedDependent").Meta[TestTags.Status].Should().Be(TestTags.StatusPass);

                        // NUnit keeps the warning, but the hidden failure after it must still block the dependents of its fixture.
                        tests.Where(t => t.Meta[TestTags.Name] == "WarningBeforeFailure").Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusPass);
                        tests.Where(t => t.Meta[TestTags.Name] == "Failed").Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusFail);
                        var skippedCases = tests.Where(t => t.Meta[TestTags.Name] == "SkippedFixtureCase").ToArray();
                        skippedCases.Should().HaveCount(2);
                        skippedCases.Should().OnlyContain(t => t.Meta[TestTags.Status] == TestTags.StatusSkip && t.Meta[TestTags.SkipReason].StartsWith("Dependency on "));
                    },
                    useDotnetExec: false,
                    arguments: $"/TestCaseFilter:{Filter} {NUnit5Tests.RunFixturesSequentially}"))
           .ConfigureAwait(false);
    }

    // Removing evp_proxy/v2 leaves evp_proxy/v4, which compresses payloads.
    private static bool ExpectsGzip(string evpVersionToRemove) => evpVersionToRemove == "evp_proxy/v2";
}

#endif
