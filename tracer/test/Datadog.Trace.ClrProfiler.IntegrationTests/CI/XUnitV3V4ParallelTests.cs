// <copyright file="XUnitV3V4ParallelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER && !DEFAULT_SAMPLES

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers.Ci;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class XUnitV3V4ParallelTests : TestingFrameworkRetriesTests
{
    private const string RequireCaseParallelismEnvironmentVariable = "XUNIT_V3_V4_REQUIRE_CASE_PARALLELISM";
    private const string TestNamespace = "Samples.XUnitTestsV3V4Parallel";
    private const string TestSuite = "Samples.XUnitTestsV3V4Parallel.TestSuite";

    public XUnitV3V4ParallelTests(ITestOutputHelper output)
        : base("XUnitTestsV3", output)
    {
        SetServiceName("xunit-v3-v4-parallel");
    }

    public enum ParallelAlgorithm
    {
        Conservative,
        Aggressive,
    }

    public enum ParallelMode
    {
        None,
        Collections,
    }

    protected override string AlwaysFails => $"{TestSuite}.AlwaysFails";

    protected override string AlwaysPasses => $"{TestSuite}.AlwaysPasses";

    protected override string TrueAtLastRetry => $"{TestSuite}.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => $"{TestSuite}.TrueAtThirdRetry";

    protected override int ExpectedTestSuiteCount => 5;

    protected override bool UseDotnetExec => true;

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public async Task FullParallelRetriesKeepTheoryRowsIsolated(
        [PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion,
        [CombinatorialValues(ParallelAlgorithm.Conservative, ParallelAlgorithm.Aggressive)] ParallelAlgorithm parallelAlgorithm)
    {
        SetEnvironmentVariable(RequireCaseParallelismEnvironmentVariable, "1");
        var tests = await FlakyRetriesWithArguments(packageVersion, $"-namespace {TestNamespace} -parallelMode all -parallelAlgorithm {GetArgument(parallelAlgorithm)} -maxThreads unlimited");
        AssertRetryIsolation(tests);
    }

    [SkippableTheory]
    [CombinatorialOrPairwiseData]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public async Task RunnerParallelModesPreserveRetrySemantics(
        [PackageVersionData(nameof(PackageVersions.XUnitV3), minInclusive: "4.0.0")] string packageVersion,
        [CombinatorialValues(ParallelMode.None, ParallelMode.Collections)] ParallelMode parallelMode)
    {
        SetEnvironmentVariable(RequireCaseParallelismEnvironmentVariable, "0");
        var tests = await FlakyRetriesWithArguments(packageVersion, $"-namespace {TestNamespace} -parallelMode {GetArgument(parallelMode)} -parallelAlgorithm conservative");
        AssertRetryIsolation(tests);
    }

    private static string GetArgument(ParallelAlgorithm parallelAlgorithm) =>
        parallelAlgorithm switch
        {
            ParallelAlgorithm.Conservative => "conservative",
            ParallelAlgorithm.Aggressive => "aggressive",
            _ => throw new ArgumentOutOfRangeException(nameof(parallelAlgorithm), parallelAlgorithm, null),
        };

    private static string GetArgument(ParallelMode parallelMode) =>
        parallelMode switch
        {
            ParallelMode.None => "none",
            ParallelMode.Collections => "collections",
            _ => throw new ArgumentOutOfRangeException(nameof(parallelMode), parallelMode, null),
        };

    private static void AssertRetryIsolation(List<MockCIVisibilityTest> tests)
    {
        var theoryRows = tests.Where(test => test.Resource == $"{TestSuite}.ConcurrentTheoryRow").ToList();
        var auxiliaryTests = tests.Where(test => test.Resource.StartsWith("Samples.XUnitTestsV3V4Parallel.Collection", StringComparison.Ordinal)).ToList();
        var dynamicSkip = tests.Where(test => test.Resource == $"{TestSuite}.DynamicSkip").ToList();
        var cancellationContextTests = tests.Where(test => test.Resource == $"{TestSuite}.CancellationContextIsAvailableOnRetry").ToList();

        tests.Should().HaveCount(32);

        auxiliaryTests.Should().HaveCount(4);
        auxiliaryTests.Should().OnlyContain(test => test.Meta[TestTags.Status] == TestTags.StatusPass);
        auxiliaryTests.Select(test => test.Resource).Should().OnlyHaveUniqueItems();

        dynamicSkip.Should().ContainSingle().Which.Meta[TestTags.Status].Should().Be(TestTags.StatusSkip);
        cancellationContextTests.Should().HaveCount(2);
        cancellationContextTests.Should().ContainSingle(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
        cancellationContextTests.Should().ContainSingle(test => test.Meta[TestTags.Status] == TestTags.StatusPass);
        cancellationContextTests.Should().ContainSingle(test => !test.Meta.ContainsKey(TestTags.TestIsRetry));
        cancellationContextTests.Should().ContainSingle(test => test.Meta.GetValueOrDefault(TestTags.TestIsRetry) == "true");

        theoryRows.Should().HaveCount(8);
        var executionsByParameters = theoryRows.GroupBy(test => test.Meta[TestTags.Parameters]).ToList();
        executionsByParameters.Should().HaveCount(4);

        foreach (var executions in executionsByParameters)
        {
            executions.Should().HaveCount(2);
            executions.Should().ContainSingle(test => test.Meta[TestTags.Status] == TestTags.StatusFail);
            executions.Should().ContainSingle(test => test.Meta[TestTags.Status] == TestTags.StatusPass);
            executions.Should().ContainSingle(test => !test.Meta.ContainsKey(TestTags.TestIsRetry));
            executions.Should().ContainSingle(test => test.Meta.GetValueOrDefault(TestTags.TestIsRetry) == "true");
            executions.Should().ContainSingle(test => test.Meta.GetValueOrDefault(TestTags.TestFinalStatus) == TestTags.StatusPass);
        }
    }
}
#endif
