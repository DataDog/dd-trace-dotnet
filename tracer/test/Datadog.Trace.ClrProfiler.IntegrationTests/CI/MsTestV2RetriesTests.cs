// <copyright file="MsTestV2RetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

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

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.MSTest2Retries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }
}

#endif
