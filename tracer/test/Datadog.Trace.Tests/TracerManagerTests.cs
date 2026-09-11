// <copyright file="TracerManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class TracerManagerTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void WriteOtlpExportSettingsWritesConfiguredStates(bool tracesEnabled, bool metricsEnabled, bool logsEnabled)
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.OpenTelemetry.TracesExporter, tracesEnabled ? "otlp" : "none" },
                { ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsEnabled, metricsEnabled },
                { ConfigurationKeys.OpenTelemetry.MetricsExporter, metricsEnabled ? "otlp" : "none" },
                { ConfigurationKeys.FeatureFlags.OpenTelemetryLogsEnabled, logsEnabled },
                { ConfigurationKeys.OpenTelemetry.LogsExporter, logsEnabled ? "otlp" : "none" },
            });

        using var stringWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(stringWriter))
        {
            jsonWriter.WriteStartObject();
            TracerManager.WriteOtlpExportSettings(jsonWriter, settings, settings.Manager.InitialExporterSettings);
            jsonWriter.WriteEndObject();
        }

        var json = JObject.Parse(stringWriter.ToString());
        json["otlp_traces_export_enabled"]?.Type.Should().Be(JTokenType.Boolean);
        json["otlp_traces_export_enabled"]?.Value<bool>().Should().Be(tracesEnabled);
        json["otlp_metrics_export_enabled"]?.Type.Should().Be(JTokenType.Boolean);
        json["otlp_metrics_export_enabled"]?.Value<bool>().Should().Be(metricsEnabled);
        json["otlp_logs_export_enabled"]?.Type.Should().Be(JTokenType.Boolean);
        json["otlp_logs_export_enabled"]?.Value<bool>().Should().Be(logsEnabled);
    }
}

#endif
