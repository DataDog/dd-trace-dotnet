// <copyright file="AgentlessFeatureFlagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

using Environment = Datadog.Trace.FeatureFlags.Rcm.Model.Environment;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.FeatureFlags;

#if NETFRAMEWORK
// The .NET Framework tests use NGEN which is a global thing, so make sure we don't parallelize
// Include these tests in the ManualInstrumentation batch
[Collection(nameof(ManualInstrumentationTests))]
#endif
public class AgentlessFeatureFlagsTests : TestHelper
{
    public AgentlessFeatureFlagsTests(ITestOutputHelper output)
        : base("OpenFeature", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ResolvesFlagsDeliveredByTheAgentlessEndpoint()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration());
        using var agent = EnvironmentHelper.GetMockAgent();

        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, FeatureFlagsSettings.AgentlessSourceName);
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, intake.Origin);

        var output = await RunSample(agent);

        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Eval (simple-string) : <OK: ", output);
        Assert.Contains("Eval (rule-based-flag) : <OK: ", output);
        Assert.Contains("Eval (numeric-rule-flag) : <OK: ", output);
        Assert.Contains("Eval (time-based-flag) : <OK: ", output);
        Assert.Contains("Eval (exposure-flag) : <OK: ", output);
        Assert.Contains("Exit. OK", output);

        // Provider initialization waits for the first configuration, so the sample never has to
        // poll for readiness of its own.
        Assert.DoesNotContain("Waiting for RC...", output);

        intake.Requests.Should().NotBeEmpty();

        // The base URL only carries an origin, so the tracer appends the canonical path.
        intake.Requests[0].PathAndQuery.Should().Be(AgentlessEndpoint.DefaultPath);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task DoesNotPollTheAgentlessEndpointWhenRemoteConfigurationIsSelected()
    {
        using var intake = new MockFeatureFlagsIntake(CreateConfiguration());
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.SetupRcm(
            Output,
            [
                ((object)new ServerConfiguration { Flags = FeatureFlagsHelpers.CreateAllFlags() },
                 RcmProducts.FfeFlags,
                 nameof(AgentlessFeatureFlagsTests))
            ]);

        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "0.5");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, FeatureFlagsSettings.RemoteConfigSourceName);

        // Configured but unused: selecting Remote Configuration must not start billed agentless polling.
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl, intake.Origin);

        var output = await RunSample(agent);

        Assert.Contains("<INSTRUMENTED>", output);
        Assert.Contains("Eval (simple-string) : <OK: ", output);
        Assert.Contains("Exit. OK", output);

        intake.Requests.Should().BeEmpty();
    }

    private static ServerConfiguration CreateConfiguration()
        => new()
        {
            Format = "SERVER",
            CreatedAt = "2025-01-01T00:00:00Z",
            Environment = new Environment { Name = "production" },
            Flags = FeatureFlagsHelpers.CreateAllFlags(),
        };

    private async Task<string> RunSample(MockTracerAgent agent)
    {
        using var telemetry = this.ConfigureTelemetry();
        using var assert = new AssertionScope();
        using var process = await RunSampleAndWaitForExit(agent);

        return process.StandardOutput.ToString();
    }
}
