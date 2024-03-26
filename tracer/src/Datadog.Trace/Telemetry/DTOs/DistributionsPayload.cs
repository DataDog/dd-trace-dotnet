// <copyright file="DistributionsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class DistributionsPayload : IPayload
{
    public DistributionsPayload(ICollection<DistributionMetricData> series)
    {
        Series = series;
    }

    /// <summary>
    /// Gets or sets the default namespace for the metrics.
    /// Default to assuming that all metrics are for the tracer.
    /// Can be overwritten on a per-metric basis
    /// </summary>
    public string Namespace { get; set; } = MetricNamespaceConstants.Tracer;

    public ICollection<DistributionMetricData> Series { get; set; }
}
