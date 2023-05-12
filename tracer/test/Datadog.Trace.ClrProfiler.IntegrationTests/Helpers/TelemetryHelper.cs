// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    internal static class TelemetryHelper
    {
        public static MockTelemetryAgent ConfigureTelemetry(this TestHelper helper)
        {
            int telemetryPort = TcpPortProvider.GetOpenPort();
            var telemetry = new MockTelemetryAgent(telemetryPort);

            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENTLESS_ENABLED", "true");
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_AGENT_PROXY_ENABLED", "false");
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            // removed, but necessary for some version conflict tests (e.g. TraceAnnotationsVersionMismatchNewerNuGetTests)
            helper.SetEnvironmentVariable("DD_TRACE_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            // API key is required when using the custom url
            helper.SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            return telemetry;
        }

        public static void AssertIntegrationEnabled(this MockTelemetryAgent telemetry, IntegrationId integrationId)
        {
            telemetry.AssertIntegration(integrationId, enabled: true, autoEnabled: true);
        }

        public static void AssertIntegrationDisabled(this MockTelemetryAgent telemetry, IntegrationId integrationId)
        {
            telemetry.AssertIntegration(integrationId, enabled: false, autoEnabled: true);
        }

        public static void AssertIntegrationEnabled(this MockTracerAgent mockAgent, IntegrationId integrationId)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppClosing);

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            AssertIntegration(allData, integrationId, true, true);
        }

        public static void AssertIntegration(this MockTelemetryAgent telemetry, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            var allData = telemetry.Telemetry.ToArray();
            AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static TelemetryData AssertConfiguration(this MockTracerAgent mockAgent, string key, object value = null)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryData)x).RequestType == TelemetryRequestTypes.AppStarted);

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            return AssertConfiguration(allData, key, value);
        }

        public static TelemetryData AssertConfiguration(this MockTelemetryAgent telemetry, string key, object value)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppStarted);

            var allData = telemetry.Telemetry.ToArray();
            return AssertConfiguration(allData, key, value);
        }

        public static TelemetryData AssertConfiguration(this MockTelemetryAgent telemetry, string key) => telemetry.AssertConfiguration(key, value: null);

        internal static TelemetryData AssertConfiguration(ICollection<TelemetryData> allData, string key, object value = null)
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

        internal static void AssertIntegration(ICollection<TelemetryData> allData, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            allData.Should().ContainSingle(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            // as integrations only include the diff, we need to reconstruct the "latest" data ourselves
            var latestIntegrations = new ConcurrentDictionary<string, IntegrationTelemetryData>();
            var integrationsPayloads =
                allData
                   .Where(
                        x => x.RequestType == TelemetryRequestTypes.AppStarted
                          || x.RequestType == TelemetryRequestTypes.AppIntegrationsChanged)
                   .OrderByDescending(x => x.SeqId)
                   .Select(
                        data => data.Payload is AppStartedPayload payload
                                    ? payload.Integrations
                                    : ((AppIntegrationsChangedPayload)data.Payload).Integrations);

            // only keep the latest version of integration data
            foreach (var integrationsPayload in integrationsPayloads)
            {
                foreach (var integrationEntry in integrationsPayload)
                {
                    latestIntegrations.TryAdd(integrationEntry.Name, integrationEntry);
                }
            }

            latestIntegrations.Should().NotBeEmpty();

            var integration = latestIntegrations.Should().ContainKey(integrationId.ToString()).WhoseValue;

            integration.Enabled.Should().Be(enabled, $"{integration.Name} should only be enabled if we generate a span");
            if (autoEnabled.HasValue)
            {
                integration.AutoEnabled.Should().Be(autoEnabled.Value, $"{integration.Name} should only be auto-enabled if available");
            }

            integration.Error.Should().BeNullOrEmpty();
        }
    }
}
