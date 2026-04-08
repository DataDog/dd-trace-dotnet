// <copyright file="DataStreamsTransactionInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

/// <summary>
/// Represents a single tracked transaction and handles its binary serialization.
///
/// <para>
/// <b>IMPORTANT:</b> The serialization format must stay in sync with the Java implementation:
/// https://github.com/DataDog/dd-trace-java/blob/master/internal-api/src/main/java/datadog/trace/api/datastreams/TransactionInfo.java
/// </para>
///
/// On-wire layout per transaction (written by <see cref="WriteTo"/>):
/// <code>
/// [1 byte ] checkpoint ID  – integer key into the checkpoint-name cache
/// [8 bytes] timestamp      – Unix nanoseconds, big-endian
/// [1 byte ] id length      – byte count of the UTF-8 transaction ID (max 255)
/// [N bytes] transaction ID – UTF-8 encoded, truncated to 255 bytes if necessary
/// </code>
///
/// The checkpoint-name cache (<see cref="GetCacheBytes"/>) is serialized separately
/// and sent alongside the transaction data so the receiver can resolve checkpoint IDs
/// back to their string names. Each entry: <c>[1 byte ID] [1 byte name length] [N bytes name]</c>.
/// Checkpoint IDs are assigned monotonically starting at 1 on first use.
/// </summary>
internal readonly struct DataStreamsTransactionInfo
{
    private const int MaxIdBytes = 255;
    // Checkpoint IDs are serialized as a single byte, so 255 is the hard maximum.
    private const int MaxCheckpointNames = byte.MaxValue;
    private const int CheckpointIdUnknown = -1;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsTransactionInfo>();
    private static readonly ConcurrentDictionary<string, int> Cache = new();
    private static int _counter;

    private readonly byte[] _idBytes;
    private readonly long _timestamp;
    private readonly int _checkpointId;

    internal DataStreamsTransactionInfo(string id, long timestamp, string checkpoint)
    {
        _idBytes = EncodeAndTruncate(id);
        _timestamp = timestamp;
        _checkpointId = GetOrAddCheckpointId(checkpoint);
    }

    internal DataStreamsTransactionInfo(byte[] idBytes, long timestamp, string checkpoint)
    {
        _idBytes = Truncate(idBytes);
        _timestamp = timestamp;
        _checkpointId = GetOrAddCheckpointId(checkpoint);
    }

    internal long TimestampNs => _timestamp;

    private static int GetOrAddCheckpointId(string checkpoint)
    {
        if (Cache.TryGetValue(checkpoint, out var id))
        {
            return id;
        }

        // IDs are stored as a single byte, so we can't exceed 255 distinct checkpoint names
        if (Cache.Count >= MaxCheckpointNames)
        {
            Log.Debug<int, string>(
                "Data streams checkpoint name cache is full ({Max} entries). " +
                "Checkpoint '{Name}' will not be tracked.",
                MaxCheckpointNames,
                checkpoint);

            return CheckpointIdUnknown;
        }

        return Cache.GetOrAdd(checkpoint, _ => Interlocked.Increment(ref _counter));
    }

    // Encodes a string to UTF-8, truncating to MaxIdBytes if necessary.
    // GetMaxByteCount is O(1) arithmetic — used as a fast pre-check for the common case.
    // On modern runtimes, truncation stops at a code-point boundary, ensuring valid UTF-8 output.
    private static byte[] EncodeAndTruncate(string value)
    {
        if (Encoding.UTF8.GetMaxByteCount(value.Length) <= MaxIdBytes)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        if (Encoding.UTF8.GetByteCount(value) <= MaxIdBytes)
        {
            return Encoding.UTF8.GetBytes(value);
        }

#if NETCOREAPP3_1_OR_GREATER
        Span<byte> temp = stackalloc byte[MaxIdBytes];
        Encoding.UTF8.GetEncoder().Convert(value.AsSpan(), temp, flush: false, out _, out var bytesUsed, out _);
        return temp[..bytesUsed].ToArray();
#else
        var encoded = Encoding.UTF8.GetBytes(value);
        var result = new byte[MaxIdBytes];
        Array.Copy(encoded, result, MaxIdBytes);
        return result;
#endif
    }

    private static byte[] Truncate(byte[] source)
    {
        if (source.Length <= MaxIdBytes)
        {
            return source;
        }

        var truncated = new byte[MaxIdBytes];
        Array.Copy(source, truncated, MaxIdBytes);
        return truncated;
    }

    internal static byte[] GetCacheBytes()
    {
        var entries = Cache.ToArray();
        if (entries.Length == 0)
        {
            return [];
        }

        // Pre-encode all names so we can size the buffer exactly and avoid a second encoding pass.
        var encodedNames = Array.ConvertAll(entries, p => EncodeAndTruncate(p.Key));
        var result = new byte[encodedNames.Sum(n => 2 + n.Length)];
        var index = 0;

        for (var i = 0; i < entries.Length; i++)
        {
            result[index++] = (byte)entries[i].Value;
            result[index++] = (byte)encodedNames[i].Length;
            Array.Copy(encodedNames[i], 0, result, index, encodedNames[i].Length);
            index += encodedNames[i].Length;
        }

        return result;
    }

    internal static void ClearCacheForTesting()
    {
        Cache.Clear();
        Interlocked.Exchange(ref _counter, 0);
    }

    internal int GetByteCount() => _idBytes.Length + 10;

    internal void WriteTo(byte[] buffer, int offset)
    {
        // up to 1 byte for checkpoint id
        buffer[offset] = (byte)_checkpointId;

        // 8 bytes for timestamp
        buffer[offset + 1] = (byte)(_timestamp >> 56);
        buffer[offset + 2] = (byte)(_timestamp >> 48);
        buffer[offset + 3] = (byte)(_timestamp >> 40);
        buffer[offset + 4] = (byte)(_timestamp >> 32);
        buffer[offset + 5] = (byte)(_timestamp >> 24);
        buffer[offset + 6] = (byte)(_timestamp >> 16);
        buffer[offset + 7] = (byte)(_timestamp >> 8);
        buffer[offset + 8] = (byte)_timestamp;

        // id size, up to 255 bytes
        buffer[offset + 9] = (byte)_idBytes.Length;

        // copy the ID
        Array.Copy(_idBytes, 0, buffer, offset + 10, _idBytes.Length);
    }

    internal byte[] GetBytes()
    {
        var result = new byte[GetByteCount()];
        WriteTo(result, 0);
        return result;
    }
}
