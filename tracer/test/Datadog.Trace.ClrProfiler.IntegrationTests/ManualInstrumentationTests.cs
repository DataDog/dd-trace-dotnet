// <copyright file="ManualInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

#if NETFRAMEWORK
// The .NET Framework tests use NGEN which is a global thing, so make sure we don't parallelize
[CollectionDefinition(nameof(ManualInstrumentationTests), DisableParallelization = true)]
[Collection(nameof(ManualInstrumentationTests))]
#endif
[UsesVerify]
public class ManualInstrumentationTests : TestHelper
{
    public ManualInstrumentationTests(ITestOutputHelper output)
        : base("ManualInstrumentation", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualAndAutomatic() => await RunTest(usePublishWithRID: false);

#if NETFRAMEWORK
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NGenRunManualAndAutomatic()
    {
        SetEnvironmentVariable("READY2RUN_ENABLED", "1");
        var sampleAppPath = EnvironmentHelper.GetSampleApplicationPath();
        NgenHelper.InstallToNativeImageCache(Output, sampleAppPath);
        try
        {
            await RunTest(usePublishWithRID: false);
        }
        finally
        {
            NgenHelper.UninstallFromNativeImageCache(Output, sampleAppPath);
        }
    }
#else
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ReadyToRunManualAndAutomatic()
    {
        // FIXME: .NET 9 RC2 doesn't seem to behave well with ReadyToRun in our CI
        // I'm 99.9% sure it's an issue in our build but it's more hassle than it's worth to fix right now
        // I'm hoping that we can just remove this skip for the GA of .NET 9
#if NET9_0
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);
#endif
#if !NET6_0_OR_GREATER
        // osx-arm64 doesn't work with Ready2Run
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.MacOs, SkipOn.ArchitectureValue.ARM64);
#endif
        SetEnvironmentVariable("READY2RUN_ENABLED", "1");
        await RunTest(usePublishWithRID: true);
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ManualOnly()
    {
        SetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED", "0");
        EnvironmentHelper.SetAutomaticInstrumentation(false);
        // with automatic instrumentation disabled, we don't expect _any_ spans
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(0, timeoutInMilliseconds: 500);
        spans.Should().BeEmpty();
    }

    private async Task RunTest(bool usePublishWithRID = false)
    {
        SetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED", "1");
        const int expectedSpans = 37;
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent, usePublishWithRID: usePublishWithRID);

        var spans = agent.WaitForSpans(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseMethodName(nameof(ManualAndAutomatic)); // they should be identical, so share
        settings.DisableRequireUniquePrefix();

        await VerifyHelper.VerifySpans(spans, settings);
    }
}
