// <copyright file="NUnitRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

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

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }
}

#endif
