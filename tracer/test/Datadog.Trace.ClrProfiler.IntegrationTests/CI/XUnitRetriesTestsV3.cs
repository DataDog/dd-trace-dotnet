// <copyright file="XUnitRetriesTestsV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET8_0_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.Ci;
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

    public static IEnumerable<object[]> V4PackageVersions => PackageVersions.XUnitRetriesV3.Where(row => ((string)row[0]).StartsWith("4."));

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
    public override Task<List<MockCIVisibilityTest>> FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(V4PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task FlakyRetriesWithExceptionReplay(string packageVersion)
    {
        return base.FlakyRetriesWithExceptionReplay(packageVersion);
    }
}
#endif
