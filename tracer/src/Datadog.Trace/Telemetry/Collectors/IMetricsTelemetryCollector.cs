// <copyright file="IMetricsTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Telemetry;

internal interface IMetricsTelemetryCollector
{
    /// <summary>
    /// Called to record the usage of a public API by a customer
    /// </summary>
    /// <param name="api">The API that was accessed</param>
    public void Record(PublicApiUsage api);

    void Record(Count metric, int increment = 1);

    void Record(Count metric, MetricTags tag, int increment = 1);

    void Record(Count metric, MetricTags tag1, MetricTags tag2, int increment = 1);

    void Record(Count metric, MetricTags tag1, MetricTags tag2, MetricTags tag3, int increment = 1);

    void Record(Gauge metric, int value);

    void Record(Gauge metric, MetricTags tag, int value);

    void Record(Distribution metric, double value);

    void Record(Distribution metric, MetricTags tag, double value);

    MetricResults GetMetrics();
}
