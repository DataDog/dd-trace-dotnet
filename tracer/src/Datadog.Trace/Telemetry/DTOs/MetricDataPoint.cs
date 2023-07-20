// <copyright file="MetricDataPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Telemetry;

internal readonly struct MetricDataPoint
{
    /// <summary>
    /// The Unix Timestamp in Seconds
    /// </summary>
    public readonly long Timestamp;

    public readonly int Value;

    public MetricDataPoint(long timestampInSeconds, int value)
    {
        Timestamp = timestampInSeconds;
        Value = value;
    }
}
