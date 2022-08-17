// <copyright file="HashHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

internal static class HashHelper
{
    private const FnvHash64.Version HashVersion = FnvHash64.Version.V1;

    /// <summary>
    /// Calculates the Node Hash for a service
    /// NOTE: <paramref name="edgeTags"/> must be in correct sort order
    /// </summary>
    public static ulong CalculateNodeHash(string service, string? env, string? primaryTag, IEnumerable<string> edgeTags)
    {
        var hash = FnvHash64.GenerateHash(service, HashVersion);
        if (!string.IsNullOrEmpty(env))
        {
            hash = FnvHash64.GenerateHash(env, HashVersion, hash);
        }

        if (!string.IsNullOrEmpty(primaryTag))
        {
            hash = FnvHash64.GenerateHash(primaryTag, HashVersion, hash);
        }

        foreach (var edgeTag in edgeTags)
        {
            hash = FnvHash64.GenerateHash(edgeTag, HashVersion, hash);
        }

        return hash;
    }

#if NETCOREAPP3_1_OR_GREATER
    [System.Runtime.CompilerServices.SkipLocalsInit]
#endif
    public static ulong CalculatePathwayHash(ulong nodeHash, ulong parentHash)
    {
#if NETCOREAPP3_1_OR_GREATER
        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, nodeHash.Value);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(8), parentHash.Value);
        return new PathwayHash(FnvHash64.GenerateHash(bytes, HashVersion));
#else
        // annoyingly allocate-y, but meh
        var bytes = new byte[8];
        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, nodeHash);
        var hash = FnvHash64.GenerateHash(bytes, HashVersion);

        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, parentHash);
        return FnvHash64.GenerateHash(bytes, HashVersion, hash);
#endif
    }
}
