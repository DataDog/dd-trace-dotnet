// <copyright file="DataStreamsTransactionContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

/// <summary>
/// Accumulates serialized transaction bytes for the DSM flush cycle.
/// This class is NOT thread-safe. All access must be serialized by the caller
/// (in practice, via <c>DataStreamsWriter._flushSemaphore</c>).
/// </summary>
internal sealed class DataStreamsTransactionContainer
{
    private const int MaxSizeBytes = 512 * 1024;
    private const int MaxDropSizeBytes = 2 * 1024 * 1024;

    private readonly int _initialByteSize;

    private byte[] _data;
    private int _size;

    internal DataStreamsTransactionContainer(int initialSizeBytes)
    {
        _initialByteSize = initialSizeBytes;
        _data = new byte[initialSizeBytes];
    }

    internal bool ShouldFlush => _size >= MaxSizeBytes;

    public bool Add(DataStreamsTransactionInfo transactionInfo)
    {
        if (_size >= MaxDropSizeBytes)
        {
            return false;
        }

        var byteCount = transactionInfo.GetByteCount();

        if (_data.Length - _size < byteCount)
        {
            var resized = new byte[Math.Max(_data.Length * 2, _size + byteCount)];
            Array.Copy(_data, 0, resized, 0, _size);
            _data = resized;
        }

        transactionInfo.WriteTo(_data, _size);
        _size += byteCount;
        return true;
    }

    public int Size() => _size;

    public byte[] GetDataAndReset()
    {
        if (_size == 0)
        {
            return [];
        }

        // trim zeros
        var result = new byte[_size];
        Array.Copy(_data, 0, result, 0, _size);
        // reset buffer and position
        _data = new byte[_initialByteSize];
        _size = 0;
        return result;
    }
}
