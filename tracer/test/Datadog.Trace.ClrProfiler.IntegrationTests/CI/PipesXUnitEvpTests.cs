// <copyright file="PipesXUnitEvpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class PipesXUnitEvpTests(ITestOutputHelper output) : XUnitEvpTests(output)
{
    [SkippableTheory(Skip = "These are currently very flaky - revisit once we move to aspnetcore-based mock agent")]
    [MemberData(nameof(GetData))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Flaky("Named pipes is flaky", maxRetries: 5)]
    public override async Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        EnvironmentHelper.EnableWindowsNamedPipes();

        await base.SubmitTraces(packageVersion, evpVersionToRemove, expectedGzip);
    }

    [SkippableTheory(Skip = "These are currently very flaky - revisit once we move to aspnetcore-based mock agent")]
    [MemberData(nameof(GetDataForEarlyFlakeDetection))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "EarlyFlakeDetection")]
    [Flaky("Named pipes is flaky", maxRetries: 5)]
    public override async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, MockData mockData, int expectedExitCode, int expectedSpans, string friendlyName)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        EnvironmentHelper.EnableWindowsNamedPipes();

        await base.EarlyFlakeDetection(packageVersion, evpVersionToRemove, expectedGzip, mockData, expectedExitCode, expectedSpans, friendlyName);
    }
}
#endif
