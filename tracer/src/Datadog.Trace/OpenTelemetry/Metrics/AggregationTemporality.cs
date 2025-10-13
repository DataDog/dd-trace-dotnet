// <copyright file="AggregationTemporality.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Represents the aggregation temporality of a metric.
/// Values match the OTLP protobuf specification.
/// </summary>
internal enum AggregationTemporality
{
    /// <summary>
    /// Delta temporality, representing changes since the last measurement.
    /// </summary>
    Delta = 1,

    /// <summary>
    /// Cumulative temporality, representing the total value since the start.
    /// </summary>
    Cumulative = 2,
}
#endif
