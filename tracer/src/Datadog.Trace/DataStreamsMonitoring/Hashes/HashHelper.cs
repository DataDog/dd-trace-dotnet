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
    /// Calculates the base NodeHash for a service.
    /// This can be used to create a <see cref="NodeHash"/> by calling <see cref="CalculateNodeHash"/>
    /// </summary>
    public static NodeHashBase CalculateNodeHashBase(string service, string? env, string? primaryTag, string? processTags, string? containerTagsHash)
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

        return new NodeHashBase(hash);
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
