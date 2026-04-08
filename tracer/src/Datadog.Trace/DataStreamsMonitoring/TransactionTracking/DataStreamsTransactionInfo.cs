// <copyright file="DataStreamsTransactionInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

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

    private static readonly ConcurrentDictionary<string, int> Cache = new();
    private static int _counter;

    private readonly byte[] _idBytes;
    private readonly long _timestamp;
    private readonly int _checkpointId;

    internal DataStreamsTransactionInfo(string id, long timestamp, string checkpoint)
    {
        var encoded = Encoding.UTF8.GetBytes(id);
        _idBytes = Truncate(encoded);
        _timestamp = timestamp;
        _checkpointId = Cache.GetOrAdd(checkpoint, _ => Interlocked.Increment(ref _counter));
    }

    internal DataStreamsTransactionInfo(byte[] idBytes, long timestamp, string checkpoint)
    {
        _idBytes = Truncate(idBytes);
        _timestamp = timestamp;
        _checkpointId = Cache.GetOrAdd(checkpoint, _ => Interlocked.Increment(ref _counter));
    }

    internal long TimestampNs => _timestamp;

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
        var result = new byte[512];
        var index = 0;

        foreach (var pair in Cache)
        {
            var keyBytes = Encoding.UTF8.GetBytes(pair.Key);
            // resize the buffer if needed
            if (result.Length - index <= keyBytes.Length + 2)
            {
                var resized = new byte[result.Length * 2];
                Array.Copy(result, 0, resized, 0, result.Length);
                result = resized;
            }

            result[index] = (byte)pair.Value;
            index++;
            result[index] = (byte)(keyBytes.Length);
            index++;

            Array.Copy(keyBytes, 0, result, index, keyBytes.Length);
            index += keyBytes.Length;
        }

        var trimmed = new byte[index];
        Array.Copy(result, trimmed, index);
        return trimmed;
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
