// <copyright file="DiagnosticLogCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry.Collectors;

internal class DiagnosticLogCollector
{
    private const TaskCreationOptions TaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;

    internal const int MaximumQueueSize = 10_000;
    // Real maximum size is 5MB, but we do _very_ conservative estimates on size, so this should be fine
    internal const int MaximumBatchSizeBytes = 4_800_000;
    // Rough size of queued messages that should force trigger a flush
    internal const int QueueSizeTrigger = 100;
    private readonly BoundedConcurrentQueue<DiagnosticLogMessageData> _queue = new(MaximumQueueSize);
    private TaskCompletionSource<bool> _tcs = new(TaskOptions);

    public List<List<DiagnosticLogMessageData>>? GetLogs()
    {
        // This method should only be called in a single-threaded loop
        List<List<DiagnosticLogMessageData>>? batches = null;
        var batchSize = 0;
        List<DiagnosticLogMessageData>? logs = null;
        while (_queue.TryDequeue(out var log))
        {
            var logSize = log.GetApproximateSerializationSize();
            if (batchSize + logSize < MaximumBatchSizeBytes)
            {
                batchSize += log.GetApproximateSerializationSize();
                logs ??= new();
                logs.Add(log);
            }
            else if (logSize < MaximumBatchSizeBytes)
            {
                // ignore oversized log message (shouldn't happen in practice)
                // Exceeded max batch size. As we reject over-size logs,
                // we know we will always have some logs here
                batches ??= new();
                batches.Add(logs!);

                // Start a new batch with the left-over log
                batchSize = logSize;
                logs = new() { log };
            }
        }

        // add final partial batch
        if (logs is { Count: > 0 })
        {
            batches ??= new();
            batches.Add(logs);
        }

        // TODO: Add telemetry metrics for batch size etc
        return batches;
    }

    public void EnqueueLog(DiagnosticLogMessageData log)
    {
        // TODO: Add telemetry metric for queue size
        _queue.TryEnqueue(log);

        if (_queue.Count > QueueSizeTrigger && _tcs is { Task.IsCompleted: false } tcs)
        {
            tcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// Returns a task that completes when the queue reaches the trigger size.
    /// If the queue has already reached the required size, triggers an immediate flush
    /// </summary>
    public Task WaitForLogsAsync()
    {
        // we're already above the trigger
        if (_queue.Count > QueueSizeTrigger)
        {
            return Task.CompletedTask;
        }

        var tcs = _tcs.Task.IsCompleted
                      ? Interlocked.Exchange(ref _tcs, new TaskCompletionSource<bool>(TaskOptions))
                      : _tcs;

        // small chance that we missed an update, so check again to be on the safe side
        if (_queue.Count > QueueSizeTrigger)
        {
            tcs.TrySetResult(true);
        }

        return tcs.Task;
    }
}
