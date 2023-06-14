// <copyright file="DistributionMetricData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class DistributionMetricData
{
    public DistributionMetricData(string metric, List<double> points, bool common)
    {
        Metric = metric;
        Points = points;
        Common = common;
    }

    /// <summary>
    /// Gets or sets the Metric name. This value will be prefixed with dd.app_telemetry.{namespace}.*
    /// or dd.app_telemetry.{namespace}.{language}.*
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets the points for the metric
    /// </summary>
    public List<double> Points { get; set; }

    /// <summary>
    /// Gets or sets a list of tags that will be associated with the points
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether indicates whether the metric is common or language specific
    /// This will affect the tags and prefix of the metric, as explained above.
    /// If this field is missing it defaults to true.
    /// </summary>
    public bool Common { get; set; }

    /// <summary>
    /// Gets or sets one of the following values: “tracers”, “profilers”, “rum”, “appsec”. Per series override for the namespace field on the payload object
    /// </summary>
    public string? Namespace { get; set; }
}
