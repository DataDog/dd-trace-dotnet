// <copyright file="NUnit5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if (NET8_0_OR_GREATER || NETFRAMEWORK) && !DEFAULT_SAMPLES

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
public class NUnit5Tests : TestingFrameworkTest
{
    public NUnit5Tests(ITestOutputHelper output)
        : base("NUnitTests", output)
    {
    }

    public static IEnumerable<object[]> GetVersions() => PackageVersions.NUnit.Where(v => ((string)v[0]).StartsWith("5.", StringComparison.Ordinal));

    public static IEnumerable<object[]> GetFilteredSkipData()
    {
        foreach (var version in GetVersions())
        {
            yield return [version[0], "FullyQualifiedName~Samples.NUnit5Tests.DependentFixture.SkippedFixtureCase", "SkippedFixtureCase", 2];
            yield return [version[0], "FullyQualifiedName=Samples.NUnit5Tests.DependentFixture.IgnoredFixtureCase", "IgnoredFixtureCase", 1];
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task ReportsDependencySkipsAndAggregatedResults(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: "/TestCaseFilter:FullyQualifiedName~Samples.NUnit5Tests -- NUnit.PreFilter=false", packageVersion: packageVersion, expectedExitCode: 1);
        var spans = (await agent.WaitForSpansAsync(18)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(18);
        spans.Should().OnlyContain(s => s.Tags[TestTags.FrameworkVersion] == packageVersion);
        spans.Count(s => s.Tags[TestTags.Status] == TestTags.StatusPass).Should().Be(9);
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
    [MemberData(nameof(GetVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task FilterIncludesPrerequisite(string packageVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        // The adapter's prefilter removes prerequisites before NUnit can expand its dependency graph.
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: "/TestCaseFilter:FullyQualifiedName=Samples.NUnit5Tests.DependenciesFixture.PassedDependent -- NUnit.PreFilter=false", packageVersion: packageVersion);
        var spans = (await agent.WaitForSpansAsync(2)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(2);
        spans.Select(s => s.Tags[TestTags.Name]).Should().BeEquivalentTo("PassedPrerequisite", "PassedDependent");
        spans.Should().OnlyContain(s => s.Tags[TestTags.Status] == TestTags.StatusPass && s.Error == 0);
    }

    [SkippableTheory]
    [MemberData(nameof(GetFilteredSkipData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task FilterExcludesUnselectedDependencySkippedCases(string packageVersion, string filter, string selectedName, int selectedCount)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration.Endpoints = agent.Configuration.Endpoints.Where(e => !e.Contains("evp_proxy")).ToArray();
        // The adapter receives no individual results from the blocked fixture and reports exit code zero.
        using var process = await RunDotnetTestSampleAndWaitForExit(agent, arguments: $"/TestCaseFilter:{filter} -- NUnit.PreFilter=false", packageVersion: packageVersion);
        var spans = (await agent.WaitForSpansAsync(selectedCount + 1)).Where(s => s.Name == "nunit.test").ToArray();

        spans.Should().HaveCount(selectedCount + 1);
        spans.Count(s => s.Tags[TestTags.Name] == "Failed" && s.Tags[TestTags.Status] == TestTags.StatusFail).Should().Be(1);
        spans.Count(s => s.Tags[TestTags.Name] == selectedName && s.Tags[TestTags.Status] == TestTags.StatusSkip).Should().Be(selectedCount);
    }
}

#endif
