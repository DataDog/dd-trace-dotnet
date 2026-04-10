// <copyright file="TraceKeepState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent;

/// <summary>
/// The keep-state for a trace after being processed by the <see cref="StatsAggregator"/>
/// </summary>
internal enum TraceKeepState
{
    /// <summary>
    /// The trace chunk should be kept.
    /// </summary>
    Keep,

    /// <summary>
    /// The trace chunk was filtered using trace filtering, and should not be aggregated.
    /// </summary>
    TraceFilter,

    /// <summary>
    /// The trace chunk was sampled out. Stats should be aggregated, but the chunk should be dropped.
    /// </summary>
    DropUnsampled,
}
