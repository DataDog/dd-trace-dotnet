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

    /// <summary>Maximum byte length produced by EncodeInto: 8 (hash) + 9 (pathway ms) + 9 (edge ms).</summary>
    internal const int MaxEncodedSize = 26;

    /// <summary>Maximum byte length of the Base64 encoding of <see cref="MaxEncodedSize"/> bytes: ceil(26/3)*4 = 36.</summary>
    internal const int MaxBase64EncodedSize = 36;

    /// <summary>
    /// Tolerance (in nanoseconds) applied when validating that decoded pathway/edge timestamps
    /// are not in the future. Allows for normal clock drift between hosts.
    /// </summary>
    internal const long MaxClockSkewNs = 60L * 1_000_000_000L;

    // Node.js emits a fixed-size context, zero-padding after shorter timestamps.
    private const int NodeJsEncodedSize = 20;

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

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Zero-allocation alternative to <see cref="Encode"/>: writes the encoded pathway directly into
    /// a caller-supplied <paramref name="buffer"/> (must be at least <see cref="MaxEncodedSize"/> bytes).
    /// </summary>
    /// <returns>Number of bytes written into <paramref name="buffer"/>.</returns>
    public static int EncodeInto(PathwayContext pathway, Span<byte> buffer)
    {
        var pathwayStartMs = ToMilliseconds(pathway.PathwayStart);
        var edgeStartMs = ToMilliseconds(pathway.EdgeStart);

        BinaryPrimitivesHelper.WriteUInt64LittleEndian(buffer, pathway.Hash.Value);
        var pathwayBytes = VarEncodingHelper.WriteVarLongZigZag(buffer.Slice(8), pathwayStartMs);
        var edgeBytes = VarEncodingHelper.WriteVarLongZigZag(buffer.Slice(8 + pathwayBytes), edgeStartMs);

        return 8 + pathwayBytes + edgeBytes;
    }
#endif

    /// <summary>
    /// Checks that the buffer contains a hash and two encoded timestamps, optionally zero-padded
    /// to the fixed 20-byte size emitted by Node.js.
    /// Used to distinguish binary pathways from an extra Base64 layer in AWS message attributes.
    /// Timestamp values are validated by <see cref="Decode(byte[])"/>.
    /// </summary>
    internal static bool IsCompleteEncoding(byte[] bytes)
    {
        return bytes.Length is >= 10 and <= MaxEncodedSize
            && VarEncodingHelper.ReadVarLongZigZag(bytes, offset: 8, out var pathwayBytes) is not null
            && VarEncodingHelper.ReadVarLongZigZag(bytes, offset: 8 + pathwayBytes, out var edgeBytes) is not null
            && HasValidPadding(bytes, 8 + pathwayBytes + edgeBytes);
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

        var edgeStartMs = VarEncodingHelper.ReadVarLongZigZag(bytes, offset: 8 + bytesRead, out var edgeBytesRead);
        if (edgeStartMs is null)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes {Base64EncodedBytes}: invalid edge start", Convert.ToBase64String(bytes));
            return null;
        }

        if (!HasValidPadding(bytes, 8 + bytesRead + edgeBytesRead))
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes: unexpected trailing bytes");
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

        var maxAllowedNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() + MaxClockSkewNs;
        if (pathwayStartNs > maxAllowedNs || edgeStartNs > maxAllowedNs)
        {
            Log.Warning(
                "Error decoding Data Stream PathwayContext from bytes {Base64EncodedBytes}: pathway or edge start is in the future",
                Convert.ToBase64String(bytes));
            return null;
        }

        // Pathway context values are in ns
        return new PathwayContext(new PathwayHash(hash), pathwayStartNs, edgeStartNs);
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Zero-allocation alternative to <see cref="Decode(byte[])"/>: decodes directly from a
    /// caller-supplied <paramref name="bytes"/> span (e.g. a stackalloc buffer).
    /// </summary>
    public static PathwayContext? Decode(Span<byte> bytes)
    {
        if (bytes.Length < 10)
        {
            Log.Warning<int>("Error decoding Data Stream PathwayContext from bytes: insufficient bytes ({ByteCount})", bytes.Length);
            return null;
        }

        var hash = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);

        var pathwayStartMs = VarEncodingHelper.ReadVarLongZigZag(bytes.Slice(8), out var bytesRead);
        if (pathwayStartMs is null)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes: invalid pathway start");
            return null;
        }

        var edgeStartMs = VarEncodingHelper.ReadVarLongZigZag(bytes.Slice(8 + bytesRead), out var edgeBytesRead);
        if (edgeStartMs is null)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes: invalid edge start");
            return null;
        }

        if (!HasValidPadding(bytes, 8 + bytesRead + edgeBytesRead))
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes: unexpected trailing bytes");
            return null;
        }

        var pathwayStartNs = ToNanoseconds(pathwayStartMs.Value);
        var edgeStartNs = ToNanoseconds(edgeStartMs.Value);
        if (pathwayStartMs > pathwayStartNs || edgeStartMs.Value > edgeStartNs)
        {
            Log.Warning(
                "Overflow detected in Data Stream PathwayContext from bytes: invalid pathway {PathwayMs}ms or edge {EdgeMs}ms",
                pathwayStartMs,
                edgeStartMs);
            return null;
        }

        var maxAllowedNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() + MaxClockSkewNs;
        if (pathwayStartNs > maxAllowedNs || edgeStartNs > maxAllowedNs)
        {
            Log.Warning("Error decoding Data Stream PathwayContext from bytes: pathway or edge start is in the future");
            return null;
        }

        return new PathwayContext(new PathwayHash(hash), pathwayStartNs, edgeStartNs);
    }
#endif

    private static bool HasValidPadding(ReadOnlySpan<byte> bytes, int encodedLength)
    {
        if (encodedLength == bytes.Length)
        {
            return true;
        }

        if (bytes.Length != NodeJsEncodedSize)
        {
            return false;
        }

        for (var i = encodedLength; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static long ToNanoseconds(long milliseconds)
        => milliseconds * 1_000_000;

    private static long ToMilliseconds(long nanoseconds)
        => nanoseconds / 1_000_000;
}
