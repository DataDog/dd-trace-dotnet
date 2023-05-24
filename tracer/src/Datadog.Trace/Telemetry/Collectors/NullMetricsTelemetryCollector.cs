// <copyright file="NullMetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal class NullMetricsTelemetryCollector : IMetricsTelemetryCollector
{
    public static readonly NullMetricsTelemetryCollector Instance = new();

    public void Record(PublicApiUsage api)
    {
    }

    public void Record(Count metric, int increment = 1)
    {
    }

    public void Record(Count metric, MetricTags tag, int increment = 1)
    {
    }

    public void Record(Count metric, MetricTags tag1, MetricTags tag2, int increment = 1)
    {
    }

    public void Record(Count metric, MetricTags tag1, MetricTags tag2, MetricTags tag3, int increment = 1)
    {
    }

    public void Record(Gauge metric, int value)
    {
    }

    public void Record(Gauge metric, MetricTags tag, int value)
    {
    }

    public void Record(Distribution metric, double value)
    {
    }

    public void Record(Distribution metric, MetricTags tag, double value)
    {
    }

    public MetricResults GetMetrics() => new(null, null);
}
