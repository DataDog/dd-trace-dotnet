// <copyright file="OverrideErrorLogExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using MetricDefinition = (Datadog.Trace.Telemetry.Metrics.Count? Metric, string OtelSetting, string DdSetting);

namespace Datadog.Trace.Tests.Configuration;

internal static class OverrideErrorLogExtensions
{
    public static void ShouldHaveExpectedOtelMetric(this OverrideErrorLog errorLog, int? metric, string otelSetting, string ddSetting)
        => errorLog.ShouldHaveExpectedOtelMetric(metric.HasValue ? (Count?)metric.Value : null, otelSetting, ddSetting);

    public static void ShouldHaveExpectedOtelMetric(this OverrideErrorLog errorLog, Count? metric, string otelSetting, string ddSetting)
        => errorLog.ShouldHaveExpectedOtelMetric((metric, otelSetting, ddSetting));

    public static void ShouldHaveExpectedOtelMetric(this OverrideErrorLog errorLog, IEnumerable<MetricDefinition> metric)
        => errorLog.ShouldHaveExpectedOtelMetric([..metric]);

    private static void ShouldHaveExpectedOtelMetric(this OverrideErrorLog errorLog, params MetricDefinition[] expectedMetrics)
    {
        var telemetry = new MetricsTelemetryCollector();
        errorLog.ProcessAndClearActions(DatadogSerilogLogger.NullLogger, telemetry);
        telemetry.AggregateMetrics();

        var actual = telemetry.GetMetrics().Metrics;
        var expected = expectedMetrics
                      .Where(x => x.Metric.HasValue)
                      .Select(metric => new
                       {
                           Metric = metric.Metric.Value.GetName(),
                           Tags = new[] { $"config_datadog:{metric.DdSetting}", $"config_opentelemetry:{metric.OtelSetting}" },
                       })
                      .ToList();

        if (expected.Count == 0)
        {
            actual.Should().BeNullOrEmpty();
        }
        else
        {
            actual.Should().BeEquivalentTo(expected);
        }
    }
}
