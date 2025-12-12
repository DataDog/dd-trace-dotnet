// <copyright file="DataStreamsTransactionContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal class DataStreamsTransactionContainer
{
    private readonly object _lock = new();

    private byte[] _data;
    private int _size;

    internal DataStreamsTransactionContainer(int initialSizeBytes)
    {
        _data = new byte[initialSizeBytes];
    }

    public void Add(DataStreamsTransactionInfo transactionInfo)
    {
        lock (_lock)
        {
            // check if we need to resize
            var transactionBytes = transactionInfo.GetBytes();

            // resize buffer if needed
            if (_data.Length - _size < transactionBytes.Length)
            {
                var resized = new byte[_data.Length * 2];
                Array.Copy(_data, 0, resized, 0, _size);
                _data = resized;
            }

            // add data
            Array.Copy(transactionBytes, 0, _data, _size, transactionBytes.Length);
            _size += transactionBytes.Length;
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
            var result = _data;
            _data = new byte[_size];
            return result;
        }
    }
}
