// <copyright file="TcpXUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class TcpXUnitEvpTests(ITestOutputHelper output) : XUnitEvpTests(output)
{
    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public override Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.SubmitTraces(packageVersion, evpVersionToRemove, expectedGzip);
    }

    /// <summary>
    /// Runs the TCP transport variant for the ITR coverage-backfill integration smoke test.
    /// </summary>
    /// <remarks>
    /// This smoke test depends on Coverlet's in-process data-collector hook emitting a coverage message; the runner tests cover the post-command XML fallback used when that callback is unavailable.
    /// </remarks>
    /// <param name="packageVersion">XUnit package version under test.</param>
    /// <param name="evpVersionToRemove">EVP endpoint version removed from the mock agent to force the target path.</param>
    /// <param name="expectedGzip">Whether the target EVP path should use gzip.</param>
    /// <returns>A task that completes when the sample process and assertions finish.</returns>
    [SkippableTheory]
    [MemberData(nameof(GetDataForCoverageBackfill))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "ArmUnsupported")]
    public override Task ItrCoverageBackfillSendsBackfilledCoverletCoverage(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.ItrCoverageBackfillSendsBackfilledCoverletCoverage(packageVersion, evpVersionToRemove, expectedGzip);
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForEarlyFlakeDetection))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "EarlyFlakeDetection")]
    public override Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.EarlyFlakeDetection(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForQuarantinedTests))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "QuarantinedTests")]
    public override Task QuarantinedTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.QuarantinedTests(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForDisabledTests))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "DisabledTests")]
    public override Task DisabledTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.DisabledTests(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForAttemptToFixTests))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "AttemptToFixTests")]
    public override Task AttemptToFixTests(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableDefaultTransport();
        return base.AttemptToFixTests(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }
}
#endif
