// <copyright file="FeatureFlagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
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
#pragma warning disable CS0618 // Type or member is obsolete
        EnableDebugMode();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task FfeEnabled()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        var request1 = agent.SetupRcm(
            Output,
            [
                ((object)new ServerConfiguration
                {
                    Flags = FeatureFlagsHelpers.CreateAllFlags(),
                },
                RcmProducts.FfeFlags,
                nameof(FeatureFlagsTests))
            ]);

        var output = await RunTest(agent, enabled: true);

        Assert.NotNull(output);
        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Eval (nonexistent) : <ERROR: No config loaded>", output);
        Assert.Contains("Eval (simple-string) : <OK: >", output);
        Assert.Contains("Eval (rule-based-flag) : <OK: >", output);
        Assert.Contains("Eval (numeric-rule-flag) : <OK: >", output);
        Assert.Contains("Eval (time-based-flag) : <OK: >", output);
        Assert.Contains("Eval (exposure-flag) : <OK: >", output);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task FfeDisabled()
    {
        using var agent = EnvironmentHelper.GetMockAgent();
        var output = await RunTest(agent, enabled: false);
        Assert.NotNull(output);
        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("FeatureFlagsSdk is disabled", output);
    }

    private async Task<string> RunTest(MockTracerAgent agent, bool enabled = true, bool usePublishWithRID = false)
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "1");
        SetEnvironmentVariable("DD_EXPERIMENTAL_FLAGGING_PROVIDER_ENABLED", enabled ? "1" : "0");
        using var telemetry = this.ConfigureTelemetry();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent, usePublishWithRID: usePublishWithRID);

        return process.StandardOutput.ToString();
    }
}
