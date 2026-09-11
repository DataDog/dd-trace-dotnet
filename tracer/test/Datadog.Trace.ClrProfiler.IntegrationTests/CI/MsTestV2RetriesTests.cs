// <copyright file="MsTestV2RetriesTests.cs" company="Datadog">
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
public class MsTestV2RetriesTests : TestingFrameworkRetriesTests
{
    public MsTestV2RetriesTests(ITestOutputHelper output)
        : base("MSTestTestsRetries", output)
    {
        SetServiceName("mstestv2-retries");
    }

    protected override string AlwaysFails => "Samples.MSTestTestsRetries.TestSuite.AlwaysFails";

    protected override string AlwaysPasses => "Samples.MSTestTestsRetries.TestSuite.AlwaysPasses";

    protected override string TrueAtLastRetry => "Samples.MSTestTestsRetries.TestSuite.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => "Samples.MSTestTestsRetries.TestSuite.TrueAtThirdRetry";

    public static IEnumerable<object[]> GetQuarantineRetryData() => GetQuarantineRetryData(PackageVersions.MSTest2Retries);

    [SkippableTheory]
    [MemberData(nameof(GetQuarantineRetryData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    [Trait("Category", "FlakyRetries")]
    public override Task QuarantineWithAutomaticRetries(string packageVersion, bool quarantined, bool retriesEnabled, bool quarantineAlwaysFails)
        => base.QuarantineWithAutomaticRetries(packageVersion, quarantined, retriesEnabled, quarantineAlwaysFails);

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.MSTest2Retries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task<List<MockCIVisibilityTest>> FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.MSTest2Retries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    [Flaky("Under investigation", 5)]
    public override Task FlakyRetriesWithExceptionReplay(string packageVersion)
    {
        return base.FlakyRetriesWithExceptionReplay(packageVersion);
    }

    // Quarantine must not depend on the runner treating Inconclusive as a non-blocking outcome.
    protected override string GetTestRunnerArguments(string packageVersion, bool useDotnetExec)
        => "-- MSTest.MapInconclusiveToFailed=true";
}

#endif
