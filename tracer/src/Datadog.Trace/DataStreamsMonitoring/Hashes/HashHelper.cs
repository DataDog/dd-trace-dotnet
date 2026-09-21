// <copyright file="HashHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring.Hashes;

internal static class HashHelper
{
    private const FnvHash64.Version HashVersion = FnvHash64.Version.V1;

    /// <summary>
    /// Calculates the base NodeHash for a service, from its identity alone (service, env, primary tag).
    /// This can be used to create a <see cref="NodeHash"/> by calling <see cref="CalculateNodeHash"/>.
    /// Unlike <see cref="CalculateBaseHash"/>, this excludes process tags and the agent-reported
    /// container-tags hash: those are agent/process metadata that can change on every rolling deploy
    /// without any real change in topology, and folding them in here would needlessly inflate the
    /// cardinality of the pathway hashes DSM's stats are keyed and quota-limited on (DSM2-335).
    /// </summary>
    public static NodeHashBase CalculateNodeHashBase(string service, string? env, string? primaryTag)
    {
        var hash = FnvHash64.GenerateHash(service, HashVersion);
        if (!StringUtil.IsNullOrEmpty(env))
        {
            hash = FnvHash64.GenerateHash(env, HashVersion, hash);
        }

        if (!StringUtil.IsNullOrEmpty(primaryTag))
        {
            hash = FnvHash64.GenerateHash(primaryTag, HashVersion, hash);
        }

        return new NodeHashBase(hash);
    }

    /// <summary>
    /// Calculates a base hash of service, env, primary tag, process tags and the agent-reported
    /// container-tags hash, using the same algorithm as <see cref="CalculateNodeHashBase"/> but also
    /// folding in process tags and container-tags hash. This mirrors the standardized "base hash"
    /// pattern used by the Go and Java tracers (<c>BaseHash</c> / <c>getBaseHash()</c>), which those
    /// tracers use for DBM's per-container SQL comment attribution. Not currently consumed by DSM or
    /// DBM in .NET (see <see cref="Datadog.Trace.ServiceRemappingHash"/> for .NET's current DBM hash), but kept
    /// here, unused, as a standardized, reusable primitive should a future DBM implementation adopt it.
    /// </summary>
    public static ulong CalculateBaseHash(string service, string? env, string? primaryTag, string? processTags, string? containerTagsHash)
    {
        var hash = FnvHash64.GenerateHash(service, HashVersion);
        if (!StringUtil.IsNullOrEmpty(env))
        {
            hash = FnvHash64.GenerateHash(env, HashVersion, hash);
        }

        if (!StringUtil.IsNullOrEmpty(primaryTag))
        {
            hash = FnvHash64.GenerateHash(primaryTag, HashVersion, hash);
        }

        if (!StringUtil.IsNullOrEmpty(processTags))
        {
            hash = FnvHash64.GenerateHash(processTags, HashVersion, hash);
            // container tags are only added if process tags are in use
            if (!StringUtil.IsNullOrEmpty(containerTagsHash))
            {
                hash = FnvHash64.GenerateHash(containerTagsHash, HashVersion, hash);
            }
        }

        return hash;
    }

    /// <summary>
    /// Calculates the Node Hash for a service
    /// NOTE: <paramref name="edgeTags"/> must be in correct sort order
    /// </summary>
    public static NodeHash CalculateNodeHash(in NodeHashBase baseNodeHash, IEnumerable<string> edgeTags)
    {
        // Already includes the static config, i.e. service, env, primary tag
        var hash = baseNodeHash.Value;
        foreach (var edgeTag in edgeTags)
        {
            hash = FnvHash64.GenerateHash(edgeTag, HashVersion, hash);
        }

        return new NodeHash(hash);
    }

    [System.Runtime.CompilerServices.SkipLocalsInit]
    public static PathwayHash CalculatePathwayHash(NodeHash nodeHash, PathwayHash parentHash)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, nodeHash.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(8), parentHash.Value);
        return new PathwayHash(FnvHash64.GenerateHash(bytes, HashVersion));
    }
}
