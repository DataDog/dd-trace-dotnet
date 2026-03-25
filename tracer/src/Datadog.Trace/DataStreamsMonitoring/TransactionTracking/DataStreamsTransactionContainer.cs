// <copyright file="DataStreamsTransactionContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal sealed class DataStreamsTransactionContainer
{
    private readonly object _lock = new();
    private readonly int _initialByteSize;

    private byte[] _data;
    private int _size;

    internal DataStreamsTransactionContainer(int initialSizeBytes)
    {
        _initialByteSize = initialSizeBytes;
        _data = new byte[initialSizeBytes];
    }

    public void Add(DataStreamsTransactionInfo transactionInfo)
    {
        lock (_lock)
        {
            var byteCount = transactionInfo.GetByteCount();

            if (_data.Length - _size < byteCount)
            {
                var resized = new byte[Math.Max(_data.Length * 2, _size + byteCount)];
                Array.Copy(_data, 0, resized, 0, _size);
                _data = resized;
            }

            transactionInfo.WriteTo(_data, _size);
            _size += byteCount;
        }
    }

    public int Size()
    {
        lock (_lock)
        {
            return _size;
        }
    }

    public byte[] GetDataAndReset()
    {
        lock (_lock)
        {
            // trim zeros
            var result = new byte[_size];
            Array.Copy(_data, 0, result, 0, _size);
            // reset buffer and position
            _data = new byte[_initialByteSize];
            _size = 0;
            return result;
        }
    }
}
