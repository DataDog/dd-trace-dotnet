﻿// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    internal static class TelemetryHelper
    {
        public static MockTelemetryAgent ConfigureTelemetry(this TestHelper helper, bool? enableV2 = null)
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

            switch (enableV2)
            {
                case true:
                    helper.SetEnvironmentVariable("DD_INTERNAL_TELEMETRY_V2_ENABLED", "1");
                    break;
                case false:
                    helper.SetEnvironmentVariable("DD_INTERNAL_TELEMETRY_V2_ENABLED", "0");
                    break;
            }

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
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryWrapper)x).IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = mockAgent.Telemetry.Cast<TelemetryWrapper>().ToArray();
            AssertIntegration(allData, integrationId, true, true);
        }

        public static void AssertIntegration(this MockTelemetryAgent telemetry, IntegrationId integrationId, bool enabled, bool? autoEnabled)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.ToArray();
            AssertIntegration(allData, integrationId, enabled, autoEnabled);
        }

        public static void AssertConfiguration(this MockTracerAgent mockAgent, string key, object value = null)
        {
            mockAgent.WaitForLatestTelemetry(x => ((TelemetryWrapper)x).IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = mockAgent.Telemetry.Cast<TelemetryWrapper>().ToArray();
            AssertConfiguration(allData, key, value);
        }

        public static void AssertConfiguration(this MockTelemetryAgent telemetry, string key, object value)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.ToArray();
            AssertConfiguration(allData, key, value);
        }

        public static void AssertConfiguration(this MockTelemetryAgent telemetry, string key) => telemetry.AssertConfiguration(key, value: null);

        internal static IEnumerable<(string[] Tags, int Value, long Timestamp)> GetMetricDataPoints(this MockTelemetryAgent telemetry, Count metric, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.ToArray();
            return GetMetricData(allData, metric.GetName(), tag1, tag2, tag3);
        }

        internal static IEnumerable<(string[] Tags, int Value, long Timestamp)> GetMetricDataPoints(this MockTelemetryAgent telemetry, Gauge metric, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.ToArray();
            return GetMetricData(allData, metric.GetName(), tag1, tag2, tag3);
        }

        internal static IEnumerable<DistributionMetricData> GetDistributions(this MockTelemetryAgent telemetry, Distribution distribution, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            telemetry.WaitForLatestTelemetry(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

            var allData = telemetry.Telemetry.ToArray();
            return GetDistributions(allData, distribution.GetName(), tag1, tag2, tag3);
        }

        internal static void AssertConfiguration(ICollection<TelemetryWrapper> allData, string key, object value = null)
        {
            // assume we're not mixing v1 and v2 telemetry in the same app
            var payloads =
                allData
                   .OrderByDescending(x => x.SeqId)
                   .Select<TelemetryWrapper, (ICollection<TelemetryValue>, ICollection<ConfigurationKeyValue>)>(
                        data => data switch
                        {
                            _ when data.TryGetPayload<AppStartedPayload>(TelemetryRequestTypes.AppStarted) is { } p => (p.Configuration, null),
                            _ when data.TryGetPayload<AppStartedPayloadV2>(TelemetryRequestTypes.AppStarted) is { } p => (null, p.Configuration),
                            _ when data.TryGetPayload<AppClientConfigurationChangedPayloadV2>(TelemetryRequestTypes.AppClientConfigurationChanged) is { } p => (null, p.Configuration),
                            _ => (null, null),
                        })
                   .Where(x => x.Item1 is not null || x.Item2 is not null)
                   .ToList();

            var v1Payloads = payloads
                            .Where(x => x.Item1 is not null)
                            .Select(x => x.Item1)
                            .ToList();

            var v2Payloads = payloads
                            .Where(x => x.Item2 is not null)
                            .Select(x => x.Item2)
                            .ToList();

            if (v1Payloads.Any())
            {
                v2Payloads.Should().BeEmpty("Should not have v2 and v1 payloads in the same run");

                var configPayload = v1Payloads.First();

                var config = configPayload.Should().ContainSingle(telemetryValue => telemetryValue.Name == key).Subject;
                config.Should().NotBeNull();
                if (value is not null)
                {
                    config.Value.Should().Be(value);
                }
            }
            else
            {
                v2Payloads.Should().NotBeEmpty();
                var config = v2Payloads
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
        }

        internal static void AssertIntegration(ICollection<TelemetryWrapper> allData, IntegrationId integrationId, bool enabled, bool? autoEnabled)
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
                            _ when data.TryGetPayload<AppStartedPayload>(TelemetryRequestTypes.AppStarted) is { } p => p.Integrations,
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

            latestIntegrations.Should().NotBeEmpty();

            var integration = latestIntegrations.Should().ContainKey(integrationId.ToString()).WhoseValue;

            integration.Enabled.Should().Be(enabled, $"{integration.Name} should only be enabled if we generate a span");
            if (autoEnabled.HasValue)
            {
                integration.AutoEnabled.Should().Be(autoEnabled.Value, $"{integration.Name} should only be auto-enabled if available");
            }

            integration.Error.Should().BeNullOrEmpty();
        }

        internal static IEnumerable<(string[] Tags, int Value, long Timestamp)> GetMetricData(ICollection<TelemetryWrapper> allData, string metricName, string tag1 = null, string tag2 = null, string tag3 = null)
        {
            allData.Should().ContainSingle(x => x.IsRequestType(TelemetryRequestTypes.AppClosing));

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

        internal static IEnumerable<DistributionMetricData> GetDistributions(ICollection<TelemetryWrapper> allData, string metricName, string tag1 = null, string tag2 = null, string tag3 = null)
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
