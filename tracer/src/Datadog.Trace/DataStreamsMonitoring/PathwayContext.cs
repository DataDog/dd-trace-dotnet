// <copyright file="PathwayContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DataStreamsMonitoring.Hashes;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// PathwayContext is used to monitor how payloads are sent across different services.
/// An example Pathway would be:
/// service A -- edge 1 --> service B -- edge 2 --> service C
/// So it's a branch of services (we also call them "nodes") connected via edges.
/// As the payload is sent around, we save the start time (start of service A),
/// and the start time of the previous service.
/// This allows us to measure the latency of each edge, as well as the latency from origin of any service.
/// </summary>
internal readonly struct PathwayContext
{
    /// <summary>
    /// The hash of the current node, of the parent node,
    /// and of the edge that connects the parent node to this node
    /// </summary>
    public readonly PathwayHash Hash;

    /// <summary>
    /// Start time of the first node in the pathway as a unix epoch in nanoseconds
    /// </summary>
    public readonly long PathwayStart;

    /// <summary>
    /// Start time of the previous node as a unix epoch in nanoseconds
    /// </summary>
    public readonly long EdgeStart;

    public PathwayContext(PathwayHash hash, long pathwayStartNs, long edgeStartNs)
    {
        Hash = hash;
        PathwayStart = pathwayStartNs;
        EdgeStart = edgeStartNs;
    }
}
