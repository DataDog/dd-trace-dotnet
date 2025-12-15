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

    private readonly string _id;
    private readonly long _timestamp;
    private readonly int _checkpointId;

    internal DataStreamsTransactionInfo(string id, long timestamp, string checkpoint)
    {
        _id = id;
        _timestamp = timestamp;
        _checkpointId = Cache.GetOrAdd(checkpoint, Interlocked.Increment(ref _counter));
    }

    internal long TimestampNs { get => _timestamp; }

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

    // ClearCache is for using in tests only
    internal static void ClearCache()
    {
        Cache.Clear();
    }

    internal byte[] GetBytes()
    {
        var idBytes = Encoding.UTF8.GetBytes(_id);
        var result = new byte[idBytes.Length + 10];

        // up to 1 byte for checkpoint id
        result[0] = (byte)_checkpointId;

        // 8 bytes for timestamp
        result[1] = (byte)(_timestamp >> 56);
        result[2] = (byte)(_timestamp >> 48);
        result[3] = (byte)(_timestamp >> 40);
        result[4] = (byte)(_timestamp >> 32);
        result[5] = (byte)(_timestamp >> 24);
        result[6] = (byte)(_timestamp >> 16);
        result[7] = (byte)(_timestamp >> 8);
        result[8] = (byte)_timestamp;

        // id size, up to 256 bytes
        result[9] = (byte)(idBytes.Length);

        // copy the ID
        Array.Copy(idBytes, 0, result, 10, idBytes.Length);
        return result;
    }
}
