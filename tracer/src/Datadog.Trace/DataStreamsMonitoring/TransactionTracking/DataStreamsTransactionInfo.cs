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

internal readonly struct DataStreamsTransactionInfo
{
    private static readonly ConcurrentDictionary<string, int> Cache = new();
    private static int _counter;

    private readonly byte[] _idBytes;
    private readonly long _timestamp;
    private readonly int _checkpointId;

    internal DataStreamsTransactionInfo(string id, long timestamp, string checkpoint)
    {
        _idBytes = Encoding.UTF8.GetBytes(id);
        _timestamp = timestamp;
        _checkpointId = Cache.GetOrAdd(checkpoint, Interlocked.Increment(ref _counter));
    }

    internal DataStreamsTransactionInfo(byte[] idBytes, long timestamp, string checkpoint)
    {
        _idBytes = idBytes;
        _timestamp = timestamp;
        _checkpointId = Cache.GetOrAdd(checkpoint, Interlocked.Increment(ref _counter));
    }

    internal long TimestampNs { get => _timestamp; }

    internal string TransactionId { get => Encoding.UTF8.GetString(_idBytes); }

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

    // ClearCache is for using in tests only — resets both the map and the counter so IDs are deterministic
    internal static void ClearCache()
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

        // id size, up to 256 bytes
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
