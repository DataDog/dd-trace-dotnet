// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
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
            var telemetryUrl = $"http://127.0.0.1:{telemetry.Port}";
            helper.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_URL", telemetryUrl);
            // removed, but necessary for some version conflict tests (e.g. TraceAnnotationsVersionMismatchNewerNuGetTests)
            helper.SetEnvironmentVariable("DD_TRACE_TELEMETRY_URL", telemetryUrl);
            // API key is required when using the custom url
            helper.SetEnvironmentVariable("DD_API_KEY", "INVALID_KEY_FOR_TESTS");
            // For legacy versions that don't use V2 by default
            helper.SetEnvironmentVariable("DD_INTERNAL_TELEMETRY_V2_ENABLED", "1");

            return telemetry;
        }

        public static ValueTask AssertIntegrationEnabledAsync(this MockTelemetryAgent telemetry, IntegrationId integrationId)
        {
            return telemetry.AssertIntegrationAsync(integrationId, enabled: true, autoEnabled: true);
        }

        public static ValueTask AssertIntegrationDisabledAsync(this MockTelemetryAgent telemetry, IntegrationId integrationId)
        {
            return telemetry.AssertIntegrationAsync(integrationId, enabled: false, autoEnabled: true);
        }

        public static ValueTask AssertIntegrationEnabledAsync(this MockTracerAgent mockAgent, IntegrationId integrationId)
            => mockAgent.AssertIntegrationAsync(integrationId, enabled: true, autoEnabled: true);

        public static ValueTask AssertIntegrationDisabledAsync(this MockTracerAgent mockAgent, IntegrationId integrationId)
            => mockAgent.AssertIntegrationAsync(integrationId, enabled: false, autoEnabled: true);

        public static async ValueTask AssertIntegrationAsync(this MockTracerAgent mockAgent, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            await mockAgent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static async ValueTask AssertIntegrationAsync(this MockTelemetryAgent telemetry, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.Cast<TelemetryData>().ToArray();
            AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static async ValueTask AssertConfigurationAsync(this MockTracerAgent mockAgent, string key, object value = null)
        {
            await mockAgent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = mockAgent.Telemetry.Cast<TelemetryData>().ToArray();
            AssertConfiguration(allData, key, value);
        }

        public static async ValueTask AssertConfigurationAsync(this MockTelemetryAgent telemetry, string key, object value)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.Cast<TelemetryData>().ToArray();
            AssertConfiguration(allData, key, value);
        }

        public static ValueTask AssertConfigurationAsync(this MockTelemetryAgent telemetry, string key)
            => telemetry.AssertConfigurationAsync(key, value: null);

        internal static async ValueTask<IEnumerable<(string[] Tags, int Value, long Timestamp)>> GetMetricDataPointsAsync(this MockTelemetryAgent telemetry, string metric, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.Cast<TelemetryData>().ToArray();
            return GetMetricData(allData, metric, tag1, tag2, tag3);
        }

        internal static async ValueTask<IEnumerable<DistributionMetricData>> GetDistributionsAsync(this MockTelemetryAgent telemetry, string distribution, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            await telemetry.WaitForLatestTelemetryAsync(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.Cast<TelemetryData>().ToArray();
            return GetDistributions(allData, distribution, tag1, tag2, tag3);
        }

        internal static void AssertConfiguration(ICollection<TelemetryData> allData, string key, object value = null)
        {
            var payloads = GetAllConfigurationPayloads(allData);

            payloads.Should().NotBeEmpty();
            var config = payloads
                        .SelectMany(x => x)
                        .GroupBy(x => x.Name)
                        .Where(x => x.Key == key)
                        .SelectMany(x => x)
                        .OrderByDescending(x => x.SeqId)
                        .FirstOrDefault();
            config.Should().NotBeNull();
            if (value is not null)
            {
                config.Value.Should().Be(value);
            }
        }

        internal static List<ICollection<ConfigurationKeyValue>> GetAllConfigurationPayloads(IEnumerable<TelemetryData> allData)
        {
            var payloads =
                allData
                   .OrderByDescending(x => x.SeqId)
                   .Select(
                        data => data switch
                        {
                            _ when data.TryGetPayload<AppStartedPayload>(TelemetryRequestTypes.AppStarted) is { } p => p.Configuration,
                            _ when data.TryGetPayload<AppClientConfigurationChangedPayload>(TelemetryRequestTypes.AppClientConfigurationChanged) is { } p => p.Configuration,
                            _ => null,
                        })
                   .Where(x => x is not null)
                   .ToList();
            return payloads;
        }

        internal static void AssertIntegration(ICollection<TelemetryData> allData, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            allData.Should().ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            // as integrations only include the diff, we need to reconstruct the "latest" data ourselves
            var latestIntegrations = new ConcurrentDictionary<string, IntegrationTelemetryData>();
            var integrationsPayloads =
                allData
                   .OrderByDescending(x => x.SeqId)
                   .Select(
                        data => data switch
                        {
                            _ when data.TryGetPayload<AppIntegrationsChangedPayload>(TelemetryRequestTypes.AppIntegrationsChanged) is { } p => p.Integrations,
                            _ => null,
                        })
                   .Where(x => x is not null);

            // only keep the latest version of integration data
            foreach (var integrationsPayload in integrationsPayloads)
            {
                foreach (var integrationEntry in integrationsPayload)
                {
                    latestIntegrations.TryAdd(integrationEntry.Name, integrationEntry);
                }
            }

            var spansCreatedByIntegration = new ConcurrentDictionary<string, MetricData>();
            var metricsPayloads =
                allData
                    .Select(
                        data => data switch
                        {
                            _ when data.TryGetPayload<GenerateMetricsPayload>(TelemetryRequestTypes.GenerateMetrics) is { } p => p.Series.Where(s => s.Metric == "spans_created"),
                            _ => null,
                        })
                    .Where(x => x is not null);

            // Flatten the spans_created metrics
            foreach (var metricPayload in metricsPayloads)
            {
                foreach (var metricEntry in metricPayload)
                {
                    spansCreatedByIntegration.TryAdd(metricEntry.Tags.First(s => s.StartsWith("integration_name:")), metricEntry);
                }
            }

            var integrationName = integrationId.ToString();
            var integrationNameTagValue = integrationName switch
            {
                "OpenTelemetry" => "otel",
                _ => integrationName.ToLowerInvariant(),
            };

            if (enabled)
            {
                spansCreatedByIntegration.Should().NotBeEmpty();

                var spansCreated = spansCreatedByIntegration.Should().ContainKey($"integration_name:{integrationNameTagValue}").WhoseValue;
                spansCreated.Points.Should().NotBeEmpty();
                spansCreated.Points.Sum(p => p.Value).Should().BeGreaterThanOrEqualTo(1);
            }

            latestIntegrations.Should().NotBeEmpty();

            var integration = latestIntegrations.Should().ContainKey(integrationName).WhoseValue;

            integration.Enabled.Should().Be(enabled, $"{integration.Name} should only be enabled if we generate a span");
            if (autoEnabled.HasValue)
            {
                integration.AutoEnabled.Should().Be(autoEnabled.Value, $"{integration.Name} should only be auto-enabled if available");
            }

            integration.Error.Should().BeNullOrEmpty();
        }

        internal static IEnumerable<(string[] Tags, int Value, long Timestamp)> GetMetricData(ICollection<TelemetryData> allData, string metricName, string tag1 = null, string tag2 = null, string tag3 = null, bool singleAppClosing = true)
        {
            if (singleAppClosing)
            {
                allData.Should().ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));
            }
            else
            {
                allData.Should().Contain(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));
            }

            var metricPayloads = allData
                                .OrderByDescending(x => x.SeqId)
                                .Select(
                                     data => data switch
                                     {
                                         _ when data.TryGetPayload<GenerateMetricsPayload>(TelemetryRequestTypes.GenerateMetrics) is { } p => p.Series,
                                         _ => null
                                     })
                                .Where(x => x is not null)
                                .SelectMany(x => x)
                                .Where(x => IsRequiredMetric(x, metricName, tag1, tag2, tag3))
                                .SelectMany(x => x.Points.Select(pt => (x.Tags, pt.Value, pt.Timestamp)));

            return metricPayloads;

            static bool IsRequiredMetric(MetricData datum, string metricName, string tag1 = null, string tag2 = null, string tag3 = null)
            {
                return datum.Metric == metricName
                    && (tag1 is null || (datum.Tags?.Contains(tag1) ?? false))
                    && (tag2 is null || (datum.Tags?.Contains(tag2) ?? false))
                    && (tag3 is null || (datum.Tags?.Contains(tag3) ?? false));
            }
        }

        internal static IEnumerable<DistributionMetricData> GetDistributions(ICollection<TelemetryData> allData, string metricName, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            allData.Should().ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var distributions = allData
                                .OrderByDescending(x => x.SeqId)
                                .Select(
                                     data => data switch
                                     {
                                         _ when data.TryGetPayload<DistributionsPayload>(TelemetryRequestTypes.Distributions) is { } p => p.Series,
                                         _ => null
                                     })
                                .Where(x => x is not null)
                                .SelectMany(x => x)
                                .Where(x => IsRequiredDistribution(x, metricName, tag1, tag2, tag3));

            return distributions;

            static bool IsRequiredDistribution(DistributionMetricData datum, string metricName, string tag1 = null, string tag2 = null, string tag3 = null)
            {
                return datum.Metric == metricName
                    && (tag1 is null || (datum.Tags?.Contains(tag1) ?? false))
                    && (tag2 is null || (datum.Tags?.Contains(tag2) ?? false))
                    && (tag3 is null || (datum.Tags?.Contains(tag3) ?? false));
            }
        }
    }
}
