// <copyright file="XUnitRetriesTestsV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class XUnitRetriesTestsV3 : TestingFrameworkRetriesTests
{
    public XUnitRetriesTestsV3(ITestOutputHelper output)
        : base("XUnitTestsRetriesV3", output)
    {
        SetServiceName("xunit-retries");
    }

    protected override string AlwaysFails => "Samples.XUnitTestsRetriesV3.TestSuite.AlwaysFails";

    protected override string AlwaysPasses => "Samples.XUnitTestsRetriesV3.TestSuite.AlwaysPasses";

    protected override string TrueAtLastRetry => "Samples.XUnitTestsRetriesV3.TestSuite.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => "Samples.XUnitTestsRetriesV3.TestSuite.TrueAtThirdRetry";

    protected override bool UseDotnetExec => true;

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnitRetriesV3), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }
}
#endif
