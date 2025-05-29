// <copyright file="BoundedChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

internal class BoundedChannel<T>
{
    private readonly BoundedConcurrentQueue<T> _buffer;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _backgroundTask;
    private readonly Action<T> _handler;

    public BoundedChannel(int capacity, Action<T> handler)
    {
        _buffer = new BoundedConcurrentQueue<T>(capacity);
        _cancellationTokenSource = new CancellationTokenSource();
        _handler = handler;
        _backgroundTask = Task.Run(Run);
    }

    public bool TryEnqueue(T item)
    {
        if (_backgroundTask.IsCompleted)
        {
            return false;
        }

        return _buffer.TryEnqueue(item);
    }

    public void Close()
    {
        _cancellationTokenSource.Cancel();
        Task.WaitAll(_backgroundTask);
    }

    private void Run()
    {
        var spinner = new SpinWait();

        // dequeue loop
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            while (_buffer.TryDequeue(out var item))
            {
                _handler(item);
            }

            spinner.SpinOnce();
        }

        // final processing
        while (_buffer.TryDequeue(out var item))
        {
            _handler(item);
        }
    }
}
