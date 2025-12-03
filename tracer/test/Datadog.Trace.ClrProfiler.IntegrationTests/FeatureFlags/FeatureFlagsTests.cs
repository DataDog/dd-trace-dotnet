// <copyright file="FeatureFlagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
// Include these tests in the ManualInstrumentation batch
[Collection(nameof(ManualInstrumentationTests))]
#endif
[UsesVerify]
public class FeatureFlagsTests : TestHelper
{
    public FeatureFlagsTests(ITestOutputHelper output)
        : base("FeatureFlags", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task EnabledNoConfig()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        EnableDebugMode();
#pragma warning restore CS0618 // Type or member is obsolete
        var output = await RunTest();
        Assert.NotNull(output);
        Assert.Contains("<INSTRUMENTED>", output);
    }

    /*
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
    #if NET9_0_OR_GREATER
            // OK, I know, this is weird, but AFAICT they changed the host FX lookup logic in .NET 9,
            // and for some reason it doesn't seem to work properly in this _one_specific case...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
             && Environment.GetEnvironmentVariable("DOTNET_EXE_32") is { } dotnet32BitExe)
            {
                var root = Path.GetDirectoryName(dotnet32BitExe);
                SetEnvironmentVariable("DOTNET_ROOT(x86)", root);
            }
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

            var spans = await agent.WaitForSpansAsync(0, timeoutInMilliseconds: 500);
            spans.Should().BeEmpty();
        }
    */

    private async Task<string> RunTest(bool usePublishWithRID = false)
    {
        SetEnvironmentVariable("DD_EXPERIMENTAL_FLAGGING_PROVIDER_ENABLED", "1");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent, usePublishWithRID: usePublishWithRID);

        return process.StandardOutput.ToString();

/*
        const int expectedSpans = 47;
        var spans = await agent.WaitForSpansAsync(expectedSpans);
        spans.Should().HaveCount(expectedSpans);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.UseMethodName(nameof(ManualAndAutomatic)); // they should be identical, so share
        settings.DisableRequireUniquePrefix();

        await VerifyHelper.VerifySpans(spans, settings);

        // telemetry should contain "code" config when it's set manually in code
        // this just verifies we have values for all the settings we call,
        // without being too rigid about the exact values to avoid fragility
        var allConfig = TelemetryHelper.GetAllConfigurationPayloads(telemetry.Telemetry)
                                       .SelectMany(x => x)
                                       .Where(x => x.Origin == "code")
                                       .Select(x => x.Name);

        allConfig.Should().Contain([
            ConfigurationKeys.DebugEnabled,
            ConfigurationKeys.ServiceName,
            ConfigurationKeys.Environment,
            ConfigurationKeys.GlobalTags,
        ]);
*/
    }
}
