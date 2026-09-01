// <copyright file="XUnitV4ParallelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.TestHelpers.Ci;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class XUnitV4ParallelTests : TestingFrameworkRetriesTests
{
    private const string RequireCaseParallelismEnvironmentVariable = "XUNIT_V4_REQUIRE_CASE_PARALLELISM";
    private const string TestSuite = "Samples.XUnitTestsV4Parallel.TestSuite";

    public XUnitV4ParallelTests(ITestOutputHelper output)
        : base("XUnitTestsV4Parallel", output)
    {
        SetServiceName("xunit-v4-parallel");
    }

    public static IEnumerable<object[]> Repetitions => Enumerable.Range(1, 20).Select(index => new object[] { index, index % 2 == 0 ? "conservative" : "aggressive" });

    protected override string AlwaysFails => $"{TestSuite}.AlwaysFails";

    protected override string AlwaysPasses => $"{TestSuite}.AlwaysPasses";

    protected override string TrueAtLastRetry => $"{TestSuite}.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => $"{TestSuite}.TrueAtThirdRetry";

    protected override int ExpectedTestSuiteCount => 5;

    protected override bool UseDotnetExec => true;

    [SkippableTheory]
    [MemberData(nameof(Repetitions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public async Task FullParallelRetriesKeepTheoryRowsIsolated(int repetition, string parallelAlgorithm)
    {
        Output.WriteLine("Parallel retry repetition: {0}; algorithm: {1}", repetition, parallelAlgorithm);
        SetEnvironmentVariable(RequireCaseParallelismEnvironmentVariable, "1");
        var tests = await FlakyRetriesWithArguments("4.0.0", $"-parallelMode all -parallelAlgorithm {parallelAlgorithm} -maxThreads unlimited");
        AssertRetryIsolation(tests);
    }

    [SkippableTheory]
    [InlineData("none")]
    [InlineData("collections")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public async Task RunnerParallelModesPreserveRetrySemantics(string parallelMode)
    {
        SetEnvironmentVariable(RequireCaseParallelismEnvironmentVariable, "0");
        var tests = await FlakyRetriesWithArguments("4.0.0", $"-parallelMode {parallelMode} -parallelAlgorithm conservative");
        AssertRetryIsolation(tests);
    }

    private static void AssertRetryIsolation(List<MockCIVisibilityTest> tests)
    {
        var theoryRows = tests.Where(test => test.Resource == $"{TestSuite}.ConcurrentTheoryRow").ToList();
        var auxiliaryTests = tests.Where(test => test.Resource.StartsWith("Samples.XUnitTestsV4Parallel.Collection", System.StringComparison.Ordinal)).ToList();
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
        }
    }
}
#endif
