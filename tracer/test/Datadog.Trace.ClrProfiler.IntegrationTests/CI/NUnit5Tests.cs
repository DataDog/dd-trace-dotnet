// <copyright file="NUnit5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if (NET8_0_OR_GREATER || NETFRAMEWORK) && !DEFAULT_SAMPLES

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
public class NUnit5Tests : TestingFrameworkTest
{
    // With parallel workers NUnit 5 queues fixtures and evaluates a fixture dependency before the prerequisite fixture
    // completes, so dependent fixtures are skipped whatever the prerequisite outcome. Without workers NUnit decides
    // from the completed prerequisite fixture.
    internal const string RunFixturesSequentially = "-- NUnit.NumberOfTestWorkers=0";

    public NUnit5Tests(ITestOutputHelper output)
        : base("NUnitTests", output)
    {
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task ReportsDependencySkipsAndAggregatedResults([PackageVersionData(nameof(PackageVersions.NUnit), minInclusive: "5.0.0")] string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: $"/TestCaseFilter:FullyQualifiedName~Samples.NUnit5Tests {RunFixturesSequentially}", packageVersion: packageVersion, expectedExitCode: 1);
        var spans = (await agent.WaitForSpansAsync(19)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(19);
        spans.Should().OnlyContain(s => s.Tags[TestTags.FrameworkVersion] == packageVersion);
        spans.Count(s => s.Tags[TestTags.Status] == TestTags.StatusPass).Should().Be(10);
        spans.Count(s => s.Tags[TestTags.Status] == TestTags.StatusFail).Should().Be(3);
        spans.Count(s => s.Tags[TestTags.Status] == TestTags.StatusSkip).Should().Be(6);
        spans.Should().OnlyContain(s => s.Tags[TestTags.Status] != TestTags.StatusPass || s.Error == 0);

        foreach (var name in new[] { "SkippedAfterFailure", "SkippedAfterIgnored", "SkippedFixtureCase", "IgnoredFixtureCase" })
        {
            var skipped = spans.Where(s => s.Tags[TestTags.Name] == name).ToArray();
            skipped.Should().HaveCount(name == "SkippedFixtureCase" ? 2 : 1);
            skipped.Should().OnlyContain(s => s.Tags[TestTags.Status] == TestTags.StatusSkip && s.Tags[TestTags.SkipReason].StartsWith("Dependency on "));
        }

        spans.Count(s => s.Tags[TestTags.Name] == "NativeRepeatWithThreshold").Should().Be(1);
        spans.Count(s => s.Tags[TestTags.Name] == "GenericData").Should().Be(2);
        spans.Where(s => s.Tags[TestTags.Name] == "GenericData").Should().OnlyContain(s => s.Tags.ContainsKey(TestTags.Parameters));
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task FilterIncludesPrerequisite([PackageVersionData(nameof(PackageVersions.NUnit), minInclusive: "5.0.0")] string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        // NUnit adds the prerequisite of the selected dependent to the run.
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: "/TestCaseFilter:FullyQualifiedName=Samples.NUnit5Tests.DependenciesFixture.PassedDependent", packageVersion: packageVersion);
        var spans = (await agent.WaitForSpansAsync(2)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(2);
        spans.Select(s => s.Tags[TestTags.Name]).Should().BeEquivalentTo("PassedPrerequisite", "PassedDependent");
        spans.Should().OnlyContain(s => s.Tags[TestTags.Status] == TestTags.StatusPass && s.Error == 0);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task FilterExcludesUnselectedDependencySkippedCases(
        [PackageVersionData(nameof(PackageVersions.NUnit), minInclusive: "5.0.0")] string packageVersion,
        [CombinatorialValues("SkippedFixtureCase", "IgnoredFixtureCase")] string selectedName)
    {
        var filter = $"FullyQualifiedName~Samples.NUnit5Tests.DependentFixture.{selectedName}";
        var selectedCount = selectedName == "SkippedFixtureCase" ? 2 : 1;
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        // The adapter receives no individual results from the blocked fixture and reports exit code zero.
        // NUnit adds the whole prerequisite fixture to the run: WarningBeforeFailure and Failed.
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: $"/TestCaseFilter:{filter} {RunFixturesSequentially}", packageVersion: packageVersion);
        var spans = (await agent.WaitForSpansAsync(selectedCount + 2)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(selectedCount + 2);
        spans.Count(s => s.Tags[TestTags.Name] == "Failed" && s.Tags[TestTags.Status] == TestTags.StatusFail).Should().Be(1);
        spans.Count(s => s.Tags[TestTags.Name] == selectedName && s.Tags[TestTags.Status] == TestTags.StatusSkip).Should().Be(selectedCount);
    }
}

#endif
