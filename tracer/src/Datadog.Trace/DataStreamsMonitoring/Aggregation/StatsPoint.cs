// <copyright file="StatsPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DataStreamsMonitoring.Hashes;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

internal readonly struct StatsPoint
{
    public readonly string[] EdgeTags;
    public readonly PathwayHash Hash;
    public readonly PathwayHash ParentHash;
    public readonly long TimestampNs;
    public readonly long PathwayLatencyNs;
    public readonly long EdgeLatencyNs;
    public readonly long PayloadSizeBytes;

    public StatsPoint(
        string[] edgeTags,
        PathwayHash hash,
        PathwayHash parentHash,
        long timestampNs,
        long pathwayLatencyNs,
        long edgeLatencyNs,
        long payloadSizeBytes)
    {
        EdgeTags = edgeTags;
        Hash = hash;
        ParentHash = parentHash;
        TimestampNs = timestampNs;
        PathwayLatencyNs = pathwayLatencyNs;
        EdgeLatencyNs = edgeLatencyNs;
        PayloadSizeBytes = payloadSizeBytes;
    }
}
