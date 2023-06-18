// <copyright file="TelemetryMetricType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Telemetry.Metrics;

internal static class TelemetryMetricType
{
    /// <summary>
    /// A count type adds up all the submitted values in a time interval;
    /// </summary>
    public const string Count = "count";

    /// <summary>
    /// The rate type takes the count and divides it by the length of the time interval
    /// </summary>
    public const string Rate = "rate";

    /// <summary>
    /// A gauge type takes the last value reported during the interval
    /// </summary>
    public const string Gauge = "gauge";

    /// <summary>
    /// A distribution type records all the submitted values
    /// </summary>
    public const string Distribution = "distribution";
}
