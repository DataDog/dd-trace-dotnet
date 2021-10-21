// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        public static MockTelemetryAgent<TelemetryData> ConfigureTelemetry(this TestHelper helper)
        {
            int telemetryPort = TcpPortProvider.GetOpenPort();
            var telemetry = new MockTelemetryAgent<TelemetryData>(telemetryPort);

            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "true");
            helper.SetEnvironmentVariable("DD_TRACE_TELEMETRY_URL", $"http://localhost:{telemetry.Port}");
            return telemetry;
        }

        public static TelemetryData AssertIntegrationEnabled(this MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId)
        {
            return AssertIntegration(telemetry, integrationId, enabled: true);
        }

        public static TelemetryData AssertIntegrationDisabled(this MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId)
        {
            return AssertIntegration(telemetry, integrationId, enabled: false);
        }

        private static TelemetryData AssertIntegration(MockTelemetryAgent<TelemetryData> telemetry, IntegrationId integrationId, bool enabled)
        {
            telemetry.WaitForLatestTelemetry(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            var allData = telemetry.Telemetry.ToArray();

            allData.Should().ContainSingle(x => x.RequestType == TelemetryRequestTypes.AppClosing);

            var latestIntegrationsData = allData
                                        .Where(
                                             x => x.RequestType == TelemetryRequestTypes.AppStarted
                                               || x.RequestType == TelemetryRequestTypes.AppIntegrationsChanged)
                                        .OrderByDescending(x => x.SeqId)
                                        .FirstOrDefault();

            latestIntegrationsData.Should().NotBeNull();

            var integrationsPayload = latestIntegrationsData.Payload is AppStartedPayload payload
                                          ? payload.Integrations
                                          : ((AppIntegrationsChangedPayload)latestIntegrationsData.Payload).Integrations;

            var integration = integrationsPayload
               .FirstOrDefault(x => x.Name == integrationId.ToString());

            integration.Should().NotBeNull();
            integration.Enabled.Should().Be(enabled); // this requires that we've generated a span
            integration.AutoEnabled.Should().BeTrue(); // we always instrument, so this will always be true
            integration.Error.Should().BeNullOrEmpty();

            return latestIntegrationsData;
        }
    }
}
