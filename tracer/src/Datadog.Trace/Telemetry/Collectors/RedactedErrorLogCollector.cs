// <copyright file="RedactedErrorLogCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry.Collectors;

internal class RedactedErrorLogCollector
{
    private const TaskCreationOptions TaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;

    internal const int MaximumQueueSize = 10_000;
    // Real maximum size is 5MB, but we do _very_ conservative estimates on size, so this should be fine
    internal const int MaximumBatchSizeBytes = 4_800_000;
    // Rough size of queued messages that should force trigger a flush
    internal const int QueueSizeTrigger = 100;
    private readonly BoundedConcurrentQueue<LogMessageData> _queue = new(MaximumQueueSize);
    private TaskCompletionSource<bool> _tcs = new(TaskOptions);
    private bool _isEnabled = true;

    private ConcurrentDictionary<uint, int> _logCounts = new();
    private ConcurrentDictionary<uint, int> _logCountsReserve = new();

    public List<List<LogMessageData>>? GetLogs()
    {
        // This method should only be called in a single-threaded loop
        List<List<LogMessageData>>? batches = null;
        var batchSize = 0;
        List<LogMessageData>? logs = null;
        var logCounts = Interlocked.Exchange(ref _logCounts, _logCountsReserve)!;

        while (_queue.TryDequeue(out var log))
        {
            var logSize = log.GetApproximateSerializationSize();
            // modify the message to add the final log count
            var eventId = EventIdHash.Compute(log.Message, log.StackTrace);
            if (logCounts.TryGetValue(eventId, out var count) && count > 1)
            {
                log.Count = count;
            }

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

        logCounts.Clear();
        _logCountsReserve = logCounts;

        return batches;
    }

    // For testing
    internal void EnqueueLog(LogMessageData log)
        => EnqueueLog(log.Message, log.Level, DateTimeOffset.FromUnixTimeSeconds(log.TracerTime), log.StackTrace);

    public void EnqueueLog(string message, TelemetryLogLevel logLevel, DateTimeOffset timestamp, string? stackTrace)
    {
        if (Volatile.Read(ref _isEnabled))
        {
            var eventId = EventIdHash.Compute(message, stackTrace);
            var logCounts = _logCounts;
            var newCount = logCounts.AddOrUpdate(eventId, addValue: 1, updateValueFactory: static (_, prev) => prev + 1);
            if (newCount != 1)
            {
                // already have this log, so don't enqueue it again
                return;
            }

            _queue.TryEnqueue(new(message, logLevel, timestamp) { StackTrace = stackTrace });

            if (_queue.Count > QueueSizeTrigger)
            {
                _tcs.TrySetResult(true);
            }
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

    public void DisableCollector()
    {
        Volatile.Write(ref _isEnabled, false);

        // clear any pending logs
        while (_queue.TryDequeue(out _))
        {
        }
    }
}
