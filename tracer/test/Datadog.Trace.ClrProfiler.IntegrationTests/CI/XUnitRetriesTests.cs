// <copyright file="XUnitRetriesTests.cs" company="Datadog">
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
public class XUnitRetriesTests : TestingFrameworkRetriesTests
{
    public XUnitRetriesTests(ITestOutputHelper output)
        : base("XUnitTestsRetries", output)
    {
        SetServiceName("xunit-retries");
    }

    protected override string AlwaysFails => "Samples.XUnitTestsRetries.TestSuite.AlwaysFails";

    protected override string AlwaysPasses => "Samples.XUnitTestsRetries.TestSuite.AlwaysPasses";

    protected override string TrueAtLastRetry => "Samples.XUnitTestsRetries.TestSuite.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => "Samples.XUnitTestsRetries.TestSuite.TrueAtThirdRetry";

    public static IEnumerable<object[]> GetQuarantineRetryData() => GetQuarantineRetryData(PackageVersions.XUnitRetries);

    public static IEnumerable<object[]> GetAutomationExclusionData() => GetAutomationExclusionData(PackageVersions.XUnitRetries);

    [SkippableTheory]
    [MemberData(nameof(GetAutomationExclusionData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    [Trait("Category", "FlakyRetries")]
    public override Task AutomationExclusions(string packageVersion, string testName, int expectedExecutions, string expectedFinalStatus, int expectedExitCode, string expectedTag)
        => base.AutomationExclusions(packageVersion, testName, expectedExecutions, expectedFinalStatus, expectedExitCode, expectedTag);

    [SkippableTheory]
    [MemberData(nameof(GetQuarantineRetryData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    [Trait("Category", "FlakyRetries")]
    public override Task QuarantineWithAutomaticRetries(string packageVersion, bool quarantined, bool retriesEnabled, bool quarantineAlwaysFails)
        => base.QuarantineWithAutomaticRetries(packageVersion, quarantined, retriesEnabled, quarantineAlwaysFails);

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task<List<MockCIVisibilityTest>> FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task<List<MockCIVisibilityTest>> DynamicFlakyRetries(string packageVersion)
    {
        return base.DynamicFlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnitRetries), MemberType = typeof(PackageVersions))]
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
        => GetTestFilterArguments("Samples.XUnitTestsRetries.TestSuite.");
}
#endif
