// <copyright file="FlagEvaluationWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class FlagEvaluationWriter
{
    internal const int DefaultQueueCapacity = 4096;

    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(50);

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<FlagEvaluationWriter>();
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly BoundedConcurrentQueue<FlagEvalEvent> _queue;
    private readonly FlagEvaluationAggregator _aggregator;
    private readonly FlagEvaluationTelemetry _telemetry;
    private readonly Func<ArraySegment<byte>, Task> _send;
    private readonly Func<IReadOnlyDictionary<string, string>> _getContext;
    private readonly int _queueCap;
    private readonly int _wakeThreshold;
    private readonly int _payloadLimitBytes;
    private readonly TimeSpan _flushInterval;
    private readonly Task _consumer;
    private TaskCompletionSource<bool>? _flush;
    private Task? _flushWait;
    private bool _closed;
    private bool _abandon;
    private long _accepted;
    private long _processed;
    private int _parked;

    internal FlagEvaluationWriter(
        Func<ArraySegment<byte>, Task> send,
        Func<IReadOnlyDictionary<string, string>> getContext,
        int queueCap = DefaultQueueCapacity,
        int globalCap = 131072,
        int perFlagCap = 10000,
        int degradedCap = 32768,
        int payloadLimitBytes = 5 * 1024 * 1024,
        TimeSpan? flushInterval = null,
        IMetricsTelemetryCollector? metrics = null)
    {
        _send = send;
        _getContext = getContext;
        _queueCap = queueCap;
        _wakeThreshold = Math.Max(1, queueCap / 8);
        _payloadLimitBytes = payloadLimitBytes;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(10);
        _queue = new BoundedConcurrentQueue<FlagEvalEvent>(queueCap);
        _aggregator = new FlagEvaluationAggregator(globalCap, perFlagCap, degradedCap);
        _telemetry = new FlagEvaluationTelemetry(metrics ?? TelemetryFactory.Metrics);
        // Provider initialization can run inside a request. Do not retain its ambient state
        // for the lifetime of this worker, or change an already-suppressed caller context.
        if (ExecutionContext.IsFlowSuppressed())
        {
            _consumer = Task.Factory.StartNew(ProcessLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                _consumer = Task.Factory.StartNew(ProcessLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }
    }

    internal bool HasCapacity()
    {
        if (Volatile.Read(ref _closed))
        {
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.Closed);
            return false;
        }

        if (_queue.Count >= _queueCap)
        {
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.PreQueueOverflow);
            return false;
        }

        return true;
    }

    internal bool TryEnqueue(FlagEvalEvent observation, int omissionReasons = 0)
    {
        // Provider and defensive event normalization may report the same reason.
        // Record the union before admission: a rejected event will never reach the consumer.
        _telemetry.ContextOmissions(observation.ContextOmissions | (ContextOmissionReason)omissionReasons);
        // Only admission and control signals use this lock: never snapshot, aggregate, or send.
        lock (_gate)
        {
            if (_closed)
            {
                _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.Closed);
                return false;
            }

            if (!_queue.TryEnqueue(observation))
            {
                _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.QueueOverflow);
                return false;
            }

            _accepted++;
            // Wake once when idle, or early enough to drain a growing batch. The full
            // fence pairs with the consumer's park/recheck so an enqueue cannot miss it.
            if (Interlocked.CompareExchange(ref _parked, 0, 1) == 1 || _queue.Count >= _wakeThreshold)
            {
                _wake.Set();
            }

            return true;
        }
    }

    internal Task FlushAsync()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return Task.CompletedTask;
            }

            // Coalesce callers rather than queueing an unbounded list of control messages.
            if (_flush is null)
            {
                _flush = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _flushWait = WaitForFlushAsync(_flush.Task);
            }

            _wake.Set();
            return _flushWait!;
        }
    }

    internal async Task CloseAsync(TimeSpan timeout)
    {
        lock (_gate)
        {
            if (!_closed)
            {
                Volatile.Write(ref _closed, true);
                _wake.Set();
            }
        }

        if (await Task.WhenAny(_consumer, Task.Delay(timeout)).ConfigureAwait(false) != _consumer)
        {
            // Do not race the sole queue consumer or dispose its wake handle while a send is live.
            Volatile.Write(ref _abandon, true);
            return;
        }

        await _consumer.ConfigureAwait(false);
    }

    private static async Task WaitForFlushAsync(Task flush)
    {
        using var cancellation = new CancellationTokenSource();
        var deadline = Task.Delay(TimeSpan.FromSeconds(10), cancellation.Token);
        if (await Task.WhenAny(flush, deadline).ConfigureAwait(false) == flush)
        {
            cancellation.Cancel();
            await flush.ConfigureAwait(false);
        }
        else
        {
            // A snapshot can contain several requests. Bound the caller's total wait,
            // while the sole consumer continues delivery with per-request deadlines.
            Log.Debug("FeatureFlags flagevaluation flush wait timed out; background delivery continues.");
        }
    }

    private static long EvaluationCount(DrainResult state)
    {
        long count = 0;
        foreach (var entry in state.Full.Values)
        {
            count += entry.Count;
        }

        foreach (var entry in state.Degraded.Values)
        {
            count += entry.Count;
        }

        return count;
    }

    private void ProcessLoop()
    {
        var sinceFlush = Stopwatch.StartNew();
        try
        {
            while (!Volatile.Read(ref _abandon))
            {
                // Continuous producers must not starve timer, explicit flush, or close checks.
                var drained = Drain(256);
                TaskCompletionSource<bool>? completion;
                long target;
                bool closing;
                lock (_gate)
                {
                    closing = _closed;
                    var flushDue = closing || _flush is not null || sinceFlush.Elapsed >= _flushInterval;
                    completion = flushDue ? _flush : null;
                    target = flushDue ? _accepted : -1;
                    if (flushDue)
                    {
                        _flush = null;
                        _flushWait = null;
                    }
                }

                if (target >= 0)
                {
                    try
                    {
                        // The target freezes admission for this flush, not intake itself.
                        // Anything accepted before the control check is published in the queue.
                        while (_processed < target && !Volatile.Read(ref _abandon))
                        {
                            Drain((int)Math.Min(256, target - _processed));
                        }

                        if (!Volatile.Read(ref _abandon))
                        {
                            SendSnapshot();
                        }
                    }
                    catch (Exception)
                    {
                        Log.Debug("FeatureFlags flagevaluation flush failed; discarding this batch without exposing diagnostics.");
                    }
                    finally
                    {
                        completion?.TrySetResult(true);
                    }

                    sinceFlush.Restart();
                    if (closing)
                    {
                        break;
                    }
                }

                var remaining = _flushInterval - sinceFlush.Elapsed;
                if (_queue.IsEmpty && remaining > TimeSpan.Zero)
                {
                    if (drained > 0)
                    {
                        _wake.WaitOne(remaining < BatchDelay ? remaining : BatchDelay);
                    }
                    else
                    {
                        Interlocked.Exchange(ref _parked, 1);
                        if (_queue.IsEmpty)
                        {
                            _wake.WaitOne(remaining);
                        }

                        Interlocked.Exchange(ref _parked, 0);
                    }
                }
            }
        }
        catch (Exception)
        {
            Log.Debug("FeatureFlags flagevaluation background consumer stopped unexpectedly.");
        }
        finally
        {
            lock (_gate)
            {
                Volatile.Write(ref _closed, true);
                _flush?.TrySetResult(true);
                _flush = null;
                _flushWait = null;
                _wake.Dispose();
            }

            long discarded = 0;
            while (_queue.TryDequeue(out _))
            {
                discarded++;
            }

            var remaining = _aggregator.Drain();
            RecordAggregationMetrics(remaining);
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.Closed, discarded + EvaluationCount(remaining));
        }
    }

    private int Drain(int limit)
    {
        // Keep the last observation in this short-lived frame, not the idle consumer loop.
        var i = 0;
        for (; i < limit && _queue.TryDequeue(out var observation); i++)
        {
            _processed++;
            try
            {
                _aggregator.Add(observation);
            }
            catch (Exception)
            {
                _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.SerializationError);
                Log.Debug("FeatureFlags flagevaluation observation could not be aggregated.");
            }
        }

        return i;
    }

    private void SendSnapshot()
    {
        // Release the drained maps and their customer data before waiting on network I/O.
        var result = EncodeSnapshot();
        foreach (var payload in result.Payloads)
        {
            if (Volatile.Read(ref _abandon))
            {
                break;
            }

            using var stream = new MemoryStream();
            using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }

            try
            {
                // The dedicated consumer owns all CPU and network work; evaluation never waits here.
                _send(new ArraySegment<byte>(stream.ToArray())).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                Log.Debug("FeatureFlags flagevaluation send failed; dropping this batch without retry.");
            }
        }
    }

    private FlagEvaluationPayloadResult EncodeSnapshot()
    {
        var state = _aggregator.Drain();
        RecordAggregationMetrics(state);
        try
        {
            var result = state.Full.Count == 0 && state.Degraded.Count == 0
                             ? new FlagEvaluationPayloadResult(Array.Empty<byte[]>(), 0, 0, 0)
                             : FlagEvaluationPayload.Encode(state, _getContext(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _payloadLimitBytes);
            _telemetry.Degraded(MetricTags.FlagEvaluationDegradeReason.PayloadLimit, result.PayloadDegradedEvaluations);
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.PayloadLimit, result.PayloadDroppedEvaluations);
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.SerializationError, result.SerializationDroppedEvaluations);
            _telemetry.PayloadSplits(result.Payloads.Count - 1);
            return result;
        }
        catch (Exception)
        {
            _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.SerializationError, EvaluationCount(state));
            throw;
        }
    }

    private void RecordAggregationMetrics(DrainResult state)
    {
        _telemetry.Dropped(MetricTags.FlagEvaluationDropReason.DegradedCap, state.Dropped);
        _telemetry.InvalidTargetingKeys(state.InvalidTargetingKeys);
        long degraded = 0;
        foreach (var entry in state.Degraded.Values)
        {
            degraded += entry.Count;
        }

        _telemetry.Degraded(MetricTags.FlagEvaluationDegradeReason.CardinalityCap, degraded);
    }
}
