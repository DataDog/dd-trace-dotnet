// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    internal static class TelemetryHelper
    {
        public static MockTelemetryAgent<TelemetryData> ConfigureTelemetry(this TestHelper helper)
        {
            int telemetryPort = TcpPortProvider.GetOpenPort();
            var telemetry = new MockTelemetryAgent<TelemetryData>(telemetryPort);

            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "true");
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            // removed, but necessary for some version conflict tests (e.g. TraceAnnotationsVersionMismatchNewerNuGetTests)
            helper.SetEnvironmentVariable("DD_TRACE_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            // API key is required when using the custom url
            helper.SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            return telemetry;
        }

        public static TelemetryData AssertIntegrationEnabled(this MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId)
        {
            return telemetry.AssertIntegration(integrationId, enabled: true, autoEnabled: true);
        }

        public static TelemetryData AssertIntegrationDisabled(this MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId)
        {
            return telemetry.AssertIntegration(integrationId, enabled: false, autoEnabled: true);
        }

        public static TelemetryData AssertIntegrationEnabled(this MockTracerAgent mockAgent, IntegrationId integrationId)
        {
            return mockAgent.AssertIntegration(integrationId, enabled: true, autoEnabled: true);
        }

        public static TelemetryData AssertIntegrationDisabled(this MockTracerAgent mockAgent, IntegrationId integrationId)
        {
            return mockAgent.AssertIntegration(integrationId, enabled: false, autoEnabled: true);
        }

        public static TelemetryData AssertIntegration(this MockTracerAgent mockAgent, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppClosing);

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            return AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static TelemetryData AssertIntegration(this MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            var allData = telemetry.Telemetry.ToArray();
            return AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static TelemetryData AssertConfiguration(this MockTracerAgent mockAgent, string key)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppStarted);

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            return AssertConfiguration(allData, key);
        }

        public static TelemetryData AssertConfiguration(this MockTelemetryAgent<TelemetryData> telemetry, string key, string value)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppStarted);

            var allData = telemetry.Telemetry.ToArray();
            return AssertConfiguration(allData, key, value);
        }

        public static TelemetryData AssertConfiguration(this MockTelemetryAgent<TelemetryData> telemetry, string key) => telemetry.AssertConfiguration(key, value: null);

        private static TelemetryData AssertConfiguration(TelemetryData[] allData, string key, string value = null)
        {
            var (latestConfigurationData, configurationPayload) =
                allData
                   .Where(x => x.RequestType == TelemetryRequestTypes.AppStarted)
                   .OrderByDescending(x => x.SeqId)
                   .Select(
                        data =>
                        {
                            var configuration = ((AppStartedPayload)data.Payload).Configuration;
                            return (data, configuration);
                        })
                   .FirstOrDefault(x => x.configuration is not null);

            latestConfigurationData.Should().NotBeNull();
            configurationPayload.Should().NotBeNull();

            var config = configurationPayload.Should().ContainSingle(telemetryValue => telemetryValue.Name == key).Subject;
            config.Should().NotBeNull();
            if (value is not null)
            {
                config.Value.Should().Be(value);
            }

            return latestConfigurationData;
        }

        private static TelemetryData AssertIntegration(TelemetryData[] allData, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            allData.Should().ContainSingle(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            var (latestIntegrationsData, integrationsPayload) =
                allData
                   .Where(
                        x => x.RequestType == TelemetryRequestTypes.AppStarted
                          || x.RequestType == TelemetryRequestTypes.AppIntegrationsChanged)
                   .OrderByDescending(x => x.SeqId)
                   .Select(
                        data =>
                        {
                            var integrations = data.Payload is AppStartedPayload payload
                                                   ? payload.Integrations
                                                   : ((AppIntegrationsChangedPayload)data.Payload).Integrations;
                            return (data, integrations);
                        })
                   .FirstOrDefault(x => x.integrations is not null);

            latestIntegrationsData.Should().NotBeNull();
            integrationsPayload.Should().NotBeNull();

            var integration = integrationsPayload
               .FirstOrDefault(x => x.Name == integrationId.ToString());

            integration.Should().NotBeNull();
            integration.Enabled.Should().Be(enabled, $"{integration.Name} should only be enabled if we generate a span");
            if (autoEnabled.HasValue)
            {
                integration.AutoEnabled.Should().Be(autoEnabled.Value, $"{integration.Name} should only be auto-enabled if available");
            }

            integration.Error.Should().BeNullOrEmpty();

            return latestIntegrationsData;
        }
    }
}
