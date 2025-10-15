// <copyright file="OtlpTemporalityPreference.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

/// <summary>
/// Defines the user's preference for OTLP metrics temporality when exporting.
/// This configuration determines which AggregationTemporality to use for different metric types.
/// </summary>
internal enum OtlpTemporalityPreference
{
    /// <summary>
    /// Prefer cumulative temporality for all supported metric types
    /// </summary>
    Cumulative = 0,

    /// <summary>
    /// Prefer delta temporality for all supported metric types
    /// </summary>
    Delta = 1,

    /// <summary>
    /// Prefer delta temporality to reduce memory usage (same as Delta)
    /// </summary>
    LowMemory = 2,
}
