// <copyright file="TimestampType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

internal enum TimestampType
{
    /// <summary>
    /// The timestamp is relative to the current bucket
    /// </summary>
    Current = 0,

    /// <summary>
    /// The timestamp is relative to the start of the pathway
    /// </summary>
    Origin = 1,
}
