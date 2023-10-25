// <copyright file="StatsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.Vendors.Datadog.Sketches;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

internal readonly struct StatsBucket
{
    public readonly string[] EdgeTags;
    public readonly PathwayHash Hash;
    public readonly PathwayHash ParentHash;
    public readonly DDSketch PathwayLatency;
    public readonly DDSketch EdgeLatency;
    public readonly DDSketch PayloadSize;

    public StatsBucket(string[] edgeTags, PathwayHash hash, PathwayHash parentHash, DDSketch pathwayLatency, DDSketch edgeLatency, DDSketch payloadSize)
    {
        EdgeTags = edgeTags;
        Hash = hash;
        ParentHash = parentHash;
        PathwayLatency = pathwayLatency;
        EdgeLatency = edgeLatency;
        PayloadSize = payloadSize;
    }
}
