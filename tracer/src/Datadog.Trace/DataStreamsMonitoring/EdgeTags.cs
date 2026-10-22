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
/// The node hash is cached on reused instances (e.g. cached via
/// <see cref="DataStreamsManager.GetOrCreateEdgeTags{TKey}"/>), so they only compute it once
/// per node hash base. One-off instances skip hash caching to avoid an extra allocation.
/// </summary>
internal sealed class EdgeTags
{
    public static readonly EdgeTags Empty = new([]);

    private readonly bool _cacheNodeHash;
    private NodeHashSnapshot? _nodeHash;

    /// <param name="tags">The edge tags. MUST be sorted in alphabetical order</param>
    /// <param name="cacheNodeHash">Whether to cache the node hash. Pass <c>false</c> for instances used for a single checkpoint.</param>
    public EdgeTags(string[] tags, bool cacheNodeHash = true)
    {
        Tags = tags;
        _cacheNodeHash = cacheNodeHash;
    }

    public string[] Tags { get; }

    public NodeHash GetNodeHash(in NodeHashBase nodeHashBase)
    {
        if (!_cacheNodeHash)
        {
            return HashHelper.CalculateNodeHash(nodeHashBase, Tags);
        }

        var snapshot = Volatile.Read(ref _nodeHash);
        if (snapshot is not null && snapshot.Base == nodeHashBase.Value)
        {
            return snapshot.Hash;
        }

        // The base changes when settings are updated, so the cached hash must be recomputed.
        // Racing threads may use different bases; every reader checks the snapshot's base
        // before reusing its hash, so an older snapshot can only cause another recomputation.
        var nodeHash = HashHelper.CalculateNodeHash(nodeHashBase, Tags);
        Volatile.Write(ref _nodeHash, new NodeHashSnapshot(nodeHashBase.Value, nodeHash));
        return nodeHash;
    }

    // Immutable so that the base and hash are always read together
    private sealed class NodeHashSnapshot
    {
        public NodeHashSnapshot(ulong nodeHashBase, NodeHash hash)
        {
            Base = nodeHashBase;
            Hash = hash;
        }

        public ulong Base { get; }

        public NodeHash Hash { get; }
    }
}
