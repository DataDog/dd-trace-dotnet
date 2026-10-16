// <copyright file="EdgeTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading;
using Datadog.Trace.DataStreamsMonitoring.Hashes;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// The edge tags for a checkpoint, along with their node hash.
/// The node hash is memoized on the instance, so instances that are reused
/// (e.g. cached via <see cref="DataStreamsManager.GetOrCreateEdgeTags{TKey}"/>)
/// only compute it once per node hash base, while one-off instances are simply collected.
/// </summary>
internal sealed class EdgeTags
{
    public static readonly EdgeTags Empty = new([]);

    private NodeHashSnapshot? _nodeHash;

    /// <param name="tags">The edge tags. MUST be sorted in alphabetical order</param>
    public EdgeTags(string[] tags)
    {
        Tags = tags;
    }

    public string[] Tags { get; }

    public NodeHash GetNodeHash(in NodeHashBase nodeHashBase)
    {
        var snapshot = Volatile.Read(ref _nodeHash);
        if (snapshot is not null && snapshot.Base == nodeHashBase.Value)
        {
            return snapshot.Hash;
        }

        // The base changes when settings are updated, so the memoized hash must be recomputed.
        // Racing threads compute the same value, so last write wins is fine.
        var nodeHash = HashHelper.CalculateNodeHash(nodeHashBase, Tags);
        Volatile.Write(ref _nodeHash, new NodeHashSnapshot(nodeHashBase.Value, nodeHash));
        return nodeHash;
    }

    // Immutable so that the base and hash are always read together
    private sealed class NodeHashSnapshot
    {
        public NodeHashSnapshot(ulong @base, NodeHash hash)
        {
            Base = @base;
            Hash = hash;
        }

        public ulong Base { get; }

        public NodeHash Hash { get; }
    }
}
