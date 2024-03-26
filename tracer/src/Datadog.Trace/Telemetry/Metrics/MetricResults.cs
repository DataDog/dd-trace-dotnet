// <copyright file="MetricResults.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry.Metrics;

internal readonly record struct MetricResults
{
    public readonly List<MetricData>? Metrics;
    public readonly List<DistributionMetricData>? Distributions;

    public MetricResults(List<MetricData>? metrics, List<DistributionMetricData>? distributions)
    {
        Metrics = metrics;
        Distributions = distributions;
    }
}
