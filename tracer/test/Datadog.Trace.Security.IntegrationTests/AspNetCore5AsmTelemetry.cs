// <copyright file="AspNetCore5AsmTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleEmail.Model;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Configuration.TracerSettings;
using Datadog.Trace.ClrProfiler.IntegrationTests;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5AsmTelemetry : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        public AspNetCore5AsmTelemetry(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5AsmTelemetry))
        {
            // telemetry metric events under test are sent only when using managed trace exporter
            SetEnvironmentVariable(ConfigurationKeys.TraceDataPipelineEnabled, "false");
        }

        [SkippableTheory]
        [Trait("RunOnWindows", "True")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestSecurityInitialization(bool enableSecurity)
        {
            using (var fixture = new AspNetCoreTestFixture())
            {
                fixture.SetOutput(Output);
                // telemetry metric events under test are sent only when using managed trace exporter
                SetEnvironmentVariable(ConfigurationKeys.TraceDataPipelineEnabled, "false");

                using var telemetry = new MockTelemetryAgent();
                Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
                EnableAgentlessTelemetry(telemetry.Port);

                await fixture.TryStartApp(this, enableSecurity: enableSecurity, sendHealthCheck: false, useTelemetry: true);
                SetHttpPort(fixture.HttpPort);

                var agent = fixture.Agent;
                await telemetry.AssertConfigurationAsync(ConfigurationKeys.AppSec.Enabled, enableSecurity, "env_var");
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityInitializationRC()
        {
            using var telemetry = new MockTelemetryAgent();
            using (var fixture = new AspNetCoreTestFixture())
            {
                fixture.SetOutput(Output);
                // telemetry metric events under test are sent only when using managed trace exporter
                SetEnvironmentVariable(ConfigurationKeys.TraceDataPipelineEnabled, "false");

                Output.WriteLine($"Assigned port {telemetry.Port} for the telemetry port.");
                EnableAgentlessTelemetry(telemetry.Port);

                await fixture.TryStartApp(this, enableSecurity: null, sendHealthCheck: false, useTelemetry: true);
                SetHttpPort(fixture.HttpPort);
                var configValues0 = (await telemetry.GetConfigurationValuesAsync(ConfigurationKeys.AppSec.Enabled)).ToList();
                configValues0.Should().HaveCount(1);
                configValues0[0].Value.Should().Be(false);
                configValues0[0].Origin.Should().Be("default");

                var agent = fixture.Agent;
                var request = await agent.SetupRcmAndWait(Output, [(new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, "ASM_FEATURES", "TestSecurityInitializationRC-1")]);

                await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClientConfigurationChanged));
            }

            var configValues1 = (await telemetry.GetConfigurationValuesAsync(ConfigurationKeys.AppSec.Enabled)).ToList();
            configValues1.Should().HaveCount(2);
            configValues1[1].Value.Should().Be(true);
            configValues1[1].Origin.Should().Be("remote_config");
        }

        private void EnableAgentlessTelemetry(int standaloneAgentPort)
        {
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "true");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED", "false");
            SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", $"http://localhost:{standaloneAgentPort}");
            // API key is required for agentless
            SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            // Disable by default
            SetEnvironmentVariable(ConfigurationKeys.Telemetry.TelemetryLogsEnabled, "0");
        }
    }
}
#endif
