// <copyright file="UdsXUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class UdsXUnitEvpTests(ITestOutputHelper output) : XUnitEvpTests(output)
{
    [SkippableTheory]
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
    public override Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, string settingsJson, string testsJson, int expectedSpans, string friendlyName)
    {
        EnvironmentHelper.EnableUnixDomainSockets();
        return base.EarlyFlakeDetection(packageVersion, evpVersionToRemove, expectedGzip, settingsJson, testsJson, expectedSpans, friendlyName);
    }
}
#endif
