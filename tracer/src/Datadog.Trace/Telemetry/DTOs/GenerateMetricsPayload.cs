// <copyright file="GenerateMetricsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class GenerateMetricsPayload : IPayload
{
    public GenerateMetricsPayload(ICollection<MetricData> series)
    : this(libVersion: TracerConstants.AssemblyVersion, libLanguage: TracerConstants.Language, series)
    {
        Series = series;
    }

    public GenerateMetricsPayload(string libVersion, string libLanguage, ICollection<MetricData> allMetricData)
    {
        LibVersion = libVersion;
        LibLanguage = libLanguage;
        Series = allMetricData;
    }

    // currently required, but should be removed soon
    public string LibVersion { get; set; }

    // currently required, but should be removed soon
    public string LibLanguage { get; set; }

    /// <summary>
    /// Gets or sets the default namespace for the metrics.
    /// Default to assuming that all metrics are for the tracer.
    /// Can be overwritten on a per-metric basis
    /// </summary>
    public string Namespace { get; set; } = MetricNamespaceConstants.Tracer;

    public ICollection<MetricData> Series { get; set; }
}
