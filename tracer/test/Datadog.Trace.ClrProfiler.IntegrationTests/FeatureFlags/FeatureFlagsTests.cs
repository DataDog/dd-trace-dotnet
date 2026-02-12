// <copyright file="FeatureFlagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.ClrProfiler.IntegrationTests.FeatureFlags;

#if !NETCOREAPP2_1

#if NETFRAMEWORK
// The .NET Framework tests use NGEN which is a global thing, so make sure we don't parallelize
// Include these tests in the ManualInstrumentation batch
[Collection(nameof(ManualInstrumentationTests))]
#endif
public class FeatureFlagsTests : FeatureFlagsTestsBase
{
    public FeatureFlagsTests(ITestOutputHelper output)
        : base("FeatureFlags", output)
    {
    }
}

#if NETFRAMEWORK
[Collection(nameof(ManualInstrumentationTests))]
#endif
public class OpenFeatureFeatureFlagsTests : FeatureFlagsTestsBase
{
    public OpenFeatureFeatureFlagsTests(ITestOutputHelper output)
        : base("OpenFeature", output)
    {
    }
}

public abstract class FeatureFlagsTestsBase : TestHelper
{
    public FeatureFlagsTestsBase(string sampleName, ITestOutputHelper output)
        : base(sampleName, output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task FfeEnabled()
    {
        int eventsReceived = 0;
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

        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/exposures"))
            {
                Interlocked.Increment(ref eventsReceived);
                e.Value.Headers["Content-Encoding"].Should().Be("gzip");
                var payload = JsonConvert.DeserializeObject(e.Value.BodyInJson);
                return;
            }
        };

        var output = await RunTest(agent, enabled: true);

        Assert.NotNull(output);
        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Eval (nonexistent) : ", output);
        Assert.Contains("Eval (simple-string) : <OK: ", output);
        Assert.Contains("Eval (rule-based-flag) : <OK: ", output);
        Assert.Contains("Eval (numeric-rule-flag) : <OK: ", output);
        Assert.Contains("Eval (time-based-flag) : <OK: ", output);
        Assert.Contains("Eval (exposure-flag) : <OK: ", output);
        Assert.Contains("Exit. OK", output);
        Assert.True(eventsReceived > 0);
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
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "0.5");
        SetEnvironmentVariable("DD_EXPERIMENTAL_FLAGGING_PROVIDER_ENABLED", enabled ? "1" : "0");
        using var telemetry = this.ConfigureTelemetry();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent, usePublishWithRID: usePublishWithRID);

        return process.StandardOutput.ToString();
    }
}

#pragma warning restore SA1402 // File may only contain a single type

#endif
