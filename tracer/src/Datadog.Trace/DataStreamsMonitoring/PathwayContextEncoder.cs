// <copyright file="PathwayContextEncoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DataStreamsMonitoring;

internal static class PathwayContextEncoder
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PathwayContextEncoder));

    /// <summary>
    /// Encodes a <see cref="PathwayContext"/> as a series of bytes
    /// NOTE: the encoding is lossy, in that we convert <see cref="PathwayContext.PathwayStart"/>
    /// and <see cref="PathwayContext.EdgeStart"/> from ns to ms
    /// </summary>
    /// <param name="pathway">The pathway to encode</param>
    /// <returns>The encoded pathway</returns>
    public static byte[] Encode(PathwayContext pathway)
    {
        var pathwayStartMs = ToMilliseconds(pathway.PathwayStart);
        var edgeStartMs = ToMilliseconds(pathway.EdgeStart);

        var pathwayBytes = VarEncodingHelper.VarLongZigZagLength(pathwayStartMs);
        var edgeBytes = VarEncodingHelper.VarLongZigZagLength(edgeStartMs);

        // maximum size = 8 + edge + pathway
        var bytes = new byte[8 + pathwayBytes + edgeBytes];
        BinaryPrimitivesHelper.WriteUInt64LittleEndian(bytes, pathway.Hash.Value);
        VarEncodingHelper.WriteVarLongZigZag(bytes, offset: 8, pathwayStartMs);
        VarEncodingHelper.WriteVarLongZigZag(bytes, offset: 8 + pathwayBytes, edgeStartMs);
        return bytes;
    }

    /// <summary>
    /// Tries to decode a <see cref="PathwayContext"/> from a <c>byte[]</c>.
    /// NOTE: the encoding process is lossy, so the decoded <see cref="PathwayContext"/>
    /// contains truncated values for <see cref="PathwayContext.PathwayStart"/>
    /// and <see cref="PathwayContext.EdgeStart"/> (conversion from ns to ms)
    /// </summary>
    /// <param name="bytes">The pathway to decode</param>
    /// <returns>The decoded <see cref="PathwayContext"/>, or <c>null</c> if decoding fails </returns>
    public static PathwayContext? Decode(byte[] bytes)
    {
        if (bytes.Length < 10)
        {
            Log.Warning<string, int>("Error decoding Data Stream PathwayContext from bytes {Base64EncodedBytes}: insufficient bytes ({ByteCount})", Convert.ToBase64String(bytes), bytes.Length);
            return null;
        }

        // first 8 bytes
        var hash = BinaryPrimitivesHelper.ReadUInt64LittleEndian(bytes);

        var pathwayStartMs = VarEncodingHelper.ReadVarLongZigZag(bytes, offset: 8, out var bytesRead);
        if (pathwayStartMs is null)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes {Base64EncodedBytes}: invalid pathway start", Convert.ToBase64String(bytes));
            return null;
        }

        var edgeStartMs = VarEncodingHelper.ReadVarLongZigZag(bytes, offset: 8 + bytesRead, out _);
        if (edgeStartMs is null)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes {Base64EncodedBytes}: invalid edge start", Convert.ToBase64String(bytes));
            return null;
        }

        var pathwayStartNs = ToNanoseconds(pathwayStartMs.Value);
        var edgeStartNs = ToNanoseconds(edgeStartMs.Value);
        if (pathwayStartMs > pathwayStartNs || edgeStartMs.Value > edgeStartNs)
        {
            Log.Warning(
                "Overflow detected in Data Stream PathwayContext from bytes {Base64EncodedBytes}: invalid pathway {PathwayMs}ms or edge {EdgeMs}ms",
                Convert.ToBase64String(bytes),
                pathwayStartMs,
                edgeStartMs);
            return null;
        }

        // Pathway context values are in ns
        return new PathwayContext(new PathwayHash(hash), pathwayStartNs, edgeStartNs);
    }

    private static long ToNanoseconds(long milliseconds)
        => milliseconds * 1_000_000;

    private static long ToMilliseconds(long nanoseconds)
        => nanoseconds / 1_000_000;
}
