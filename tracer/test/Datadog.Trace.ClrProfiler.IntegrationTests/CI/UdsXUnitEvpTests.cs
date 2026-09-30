// <copyright file="UdsXUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
[Collection(nameof(TransportTestsCollection))]
public class UdsXUnitEvpTests(ITestOutputHelper output) : XUnitEvpTests(output)
{
    // TODO: Coverlet still writes its cobertura attachment under the .NET 11 SDK, but the tracer
    // never sends the SessionCodeCoverage IPC message, so the coverage assertion in this test fails
    // on every leg except net11.0. Delete the whole #if block once that's fixed.
#if NET11_0_OR_GREATER
    [SkippableTheory]
#else
    [SkippableTheory(Skip = "CI Visibility code coverage IPC message is not received under the .NET 11 SDK. Only the net11.0 legs are unaffected. Pending investigation.")]
#endif
    [MemberData(nameof(GetData))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public override Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        EnvironmentHelper.EnableUnixDomainSockets();
        return base.SubmitTraces(packageVersion, evpVersionToRemove, expectedGzip);
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForEarlyFlakeDetection))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "EarlyFlakeDetection")]
    public override Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableUnixDomainSockets();
        return base.EarlyFlakeDetection(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }
}
#endif
