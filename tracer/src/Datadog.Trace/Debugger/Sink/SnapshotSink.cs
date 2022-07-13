// <copyright file="SnapshotSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.Sink;

internal class SnapshotSink
{
    private const int DefaultQueueLimit = 1000;

    private readonly BoundedConcurrentQueue<string> _queue;
    private readonly int _batchSize;

    private SnapshotSink(int batchSize)
    {
        _batchSize = batchSize;
        _queue = new BoundedConcurrentQueue<string>(DefaultQueueLimit);
    }

    public static SnapshotSink Create(ImmutableDebuggerSettings settings)
    {
        return new SnapshotSink(settings.UploadBatchSize);
    }

    public void Add(string snapshot)
    {
        _queue.TryEnqueue(snapshot);
    }

    public List<string> GetSnapshots()
    {
        var snapshots = new List<string>();
        var counter = 0;
        while (!_queue.IsEmpty && counter < _batchSize)
        {
            if (_queue.TryDequeue(out var snapshot))
            {
                snapshots.Add(snapshot);
            }

            counter++;
        }

        return snapshots;
    }

    public int RemainingCapacity()
    {
        return DefaultQueueLimit - _queue.Count;
    }
}
