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
    [SkippableTheory]
    [MemberData(nameof(GetData))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public override async Task SubmitTraces(string packageVersion, string evpVersionToRemove, bool expectedGzip)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        EnvironmentHelper.EnableWindowsNamedPipes();

        // The server implementation of named pipes is flaky so have 5 attempts
        var attemptsRemaining = 5;
        while (true)
        {
            try
            {
                attemptsRemaining--;
                await base.SubmitTraces(packageVersion, evpVersionToRemove, expectedGzip);
                return;
            }
            catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
            {
                await ReportRetry(Output, attemptsRemaining, ex);
            }
        }
    }

    [SkippableTheory]
    [MemberData(nameof(GetDataForEarlyFlakeDetection))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "EarlyFlakeDetection")]
    public override async Task EarlyFlakeDetection(string packageVersion, string evpVersionToRemove, bool expectedGzip, string settingsJson, string testsJson, int expectedSpans, string friendlyName)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        EnvironmentHelper.EnableWindowsNamedPipes();

        // The server implementation of named pipes is flaky so have 5 attempts
        var attemptsRemaining = 5;
        while (true)
        {
            try
            {
                attemptsRemaining--;
                await base.EarlyFlakeDetection(packageVersion, evpVersionToRemove, expectedGzip, settingsJson, testsJson, expectedSpans, friendlyName);
                return;
            }
            catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
            {
                await ReportRetry(Output, attemptsRemaining, ex);
            }
        }
    }
}
#endif
