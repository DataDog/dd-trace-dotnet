// <copyright file="NUnitRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
[Collection(nameof(TransportTestsCollection))]
public class NUnitRetriesTests : TestingFrameworkRetriesTests
{
    public NUnitRetriesTests(ITestOutputHelper output)
        : base("NUnitTestsRetries", output)
    {
        SetServiceName("nunit-retries");
    }

    protected override string AlwaysFails => "Samples.NUnitTestsRetries.TestSuite.AlwaysFails";

    protected override string AlwaysPasses => "Samples.NUnitTestsRetries.TestSuite.AlwaysPasses";

    protected override string TrueAtLastRetry => "Samples.NUnitTestsRetries.TestSuite.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => "Samples.NUnitTestsRetries.TestSuite.TrueAtThirdRetry";

    protected override string[] QuarantineTestNames => [..base.QuarantineTestNames, "AlwaysFailsWithAssertions"];

    public static IEnumerable<object[]> GetQuarantineRetryData() => GetQuarantineRetryData(PackageVersions.NUnitRetries);

    public static IEnumerable<object[]> GetAutomationExclusionData() => GetAutomationExclusionData(PackageVersions.NUnitRetries);

    [SkippableTheory]
    [MemberData(nameof(GetAutomationExclusionData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    [Trait("Category", "FlakyRetries")]
    public override Task AutomationExclusions(string packageVersion, string testName, int expectedExecutions, string expectedFinalStatus, int expectedExitCode, string expectedTag)
        => base.AutomationExclusions(packageVersion, testName, expectedExecutions, expectedFinalStatus, expectedExitCode, expectedTag);

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task ProtectedAttemptToFixFailure(string packageVersion)
        => base.ProtectedAttemptToFixFailure(packageVersion);

    [SkippableTheory]
    [MemberData(nameof(GetQuarantineRetryData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    [Trait("Category", "FlakyRetries")]
    public override Task QuarantineWithAutomaticRetries(string packageVersion, bool quarantined, bool retriesEnabled, bool quarantineAlwaysFails)
        => base.QuarantineWithAutomaticRetries(packageVersion, quarantined, retriesEnabled, quarantineAlwaysFails);

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task<List<MockCIVisibilityTest>> FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task<List<MockCIVisibilityTest>> DynamicFlakyRetries(string packageVersion)
    {
        return base.DynamicFlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    [Flaky("Under investigation", 5)]
    public override Task FlakyRetriesWithExceptionReplay(string packageVersion)
    {
        return base.FlakyRetriesWithExceptionReplay(packageVersion);
    }

    // The automation exclusion scenarios share this sample, so keep the retry scenarios on their own suite.
    protected override string GetTestRunnerArguments(string packageVersion, bool useDotnetExec)
        => GetTestFilterArguments("Samples.NUnitTestsRetries.TestSuite.");
}

#endif
