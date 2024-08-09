// <copyright file="OpenTelemetryHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry
{
    internal static class OpenTelemetryHelpers
    {
        public static void GetConfigurationMetricTags(
                string openTelemetryKey,
                out MetricTags.OpenTelemetryConfiguration openTelemetryConfig,
                out MetricTags.DatadogConfiguration datadogConfig)
        {
            if (string.Equals(openTelemetryKey, "OTEL_SERVICE_NAME", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.ServiceName;
                datadogConfig = MetricTags.DatadogConfiguration.Service;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_LOG_LEVEL", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.LogLevel;
                datadogConfig = MetricTags.DatadogConfiguration.DebugEnabled;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_PROPAGATORS", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.Propagators;
                datadogConfig = MetricTags.DatadogConfiguration.PropagationStyle;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_TRACES_SAMPLER", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.TracesSampler;
                datadogConfig = MetricTags.DatadogConfiguration.SampleRate;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_TRACES_SAMPLER_ARG", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.TracesSamplerArg;
                datadogConfig = MetricTags.DatadogConfiguration.SampleRate;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_TRACES_EXPORTER", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.TracesExporter;
                datadogConfig = MetricTags.DatadogConfiguration.TraceEnabled;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_METRICS_EXPORTER", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.MetricsExporter;
                datadogConfig = MetricTags.DatadogConfiguration.RuntimeMetricsEnabled;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_RESOURCE_ATTRIBUTES", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.ResourceAttributes;
                datadogConfig = MetricTags.DatadogConfiguration.Tags;
            }
            else if (string.Equals(openTelemetryKey, "OTEL_SDK_DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.SdkDisabled;
                datadogConfig = MetricTags.DatadogConfiguration.OpenTelemetryEnabled;
            }
            else
            {
                openTelemetryConfig = MetricTags.OpenTelemetryConfiguration.Unknown;
                datadogConfig = MetricTags.DatadogConfiguration.Unknown;
            }
        }
    }
}
