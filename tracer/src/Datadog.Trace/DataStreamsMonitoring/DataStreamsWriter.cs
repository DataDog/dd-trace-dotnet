// <copyright file="DataStreamsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

internal class DataStreamsWriter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsWriter>();

    private readonly BoundedConcurrentQueue<StatsPoint> _buffer = new(queueLimit: 10_000);
    private readonly Task _processTask;
    private readonly ManualResetEventSlim _processingMutex = new(initialState: false, spinCount: 0);
    private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DataStreamsAggregator _aggregator;
    private readonly IDataStreamsApi _api;
    private readonly Timer _flushTimer;
    private byte[]? _serializationBuffer;
    private long _pointsDropped;
    private int _flushRequested;

    public DataStreamsWriter(
        DataStreamsAggregator aggregator,
        IDataStreamsApi api,
        long bucketDurationMs)
    {
        _aggregator = aggregator;
        _api = api;

        _processTask = Task.Run(ProcessQueueLoopAsync);
        _processTask.ContinueWith(t => Log.Error(t.Exception, "Error in processing task"), TaskContinuationOptions.OnlyOnFaulted);

        _flushTimer = new Timer(
            x => ((DataStreamsWriter)x!).RequestFlush(),
            this,
            dueTime: bucketDurationMs,
            period: bucketDurationMs);
    }

    public void Add(in StatsPoint point)
    {
        if (_buffer.TryEnqueue(point))
        {
            if (!_processingMutex.IsSet)
            {
                _processingMutex.Set();
            }
        }
        else
        {
            Interlocked.Increment(ref _pointsDropped);
        }
    }

    public async Task DisposeAsync()
    {
#if NETCOREAPP3_1_OR_GREATER
        await _flushTimer.DisposeAsync().ConfigureAwait(false);
#else
        _flushTimer.Dispose();
#endif
        await FlushAndCloseAsync().ConfigureAwait(false);
    }

    private async Task FlushAndCloseAsync()
    {
        if (!_processExit.TrySetResult(true))
        {
            return;
        }

        // request a final flush - as the _processExit flag is now set
        // this ensures we will definitely flush all the stats
        // (and sets the mutex if it isn't already set)
        RequestFlush();

        // wait for the processing loop to complete
        var completedTask = await Task.WhenAny(
                                           _processTask,
                                           Task.Delay(TimeSpan.FromSeconds(20)))
                                      .ConfigureAwait(false);

        if (completedTask != _processTask)
        {
            Log.Error("Could not flush all data streams stats before process exit");
        }
    }

    private void RequestFlush()
    {
        Interlocked.Exchange(ref _flushRequested, 1);
        if (!_processingMutex.IsSet)
        {
            _processingMutex.Set();
        }
    }

    private async Task WriteToApiAsync()
    {
        // This method blocks ingestion of new stats points into the aggregator
        // but they will continue to be added to the queue, and will be processed later
        // Default buffer capacity matches Java implementation:
        // https://cs.github.com/DataDog/dd-trace-java/blob/3386bd137e58ed7450d1704e269d3567aeadf4c0/dd-trace-core/src/main/java/datadog/trace/core/datastreams/MsgPackDatastreamsPayloadWriter.java?q=MsgPackDatastreamsPayloadWriter#L28
        _serializationBuffer ??= new byte[512 * 1024];

        var flushTimeNs = _processExit.Task.IsCompleted
                          ? long.MaxValue // flush all buckets
                          : DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(); // don't flush current bucket

        const int offset = 0;
        var bytesWritten = _aggregator.Serialize(ref _serializationBuffer, offset: offset, flushTimeNs);

        if (bytesWritten > 0)
        {
            // This flushes on the same thread as the processing loop
            var data = new ArraySegment<byte>(_serializationBuffer, offset, bytesWritten);

            var success = await _api.SendAsync(data).ConfigureAwait(false);

            var dropCount = Interlocked.Exchange(ref _pointsDropped, 0);
            if (success)
            {
                Log.Debug("Flushed {Count}bytes to data streams intake. {Dropped} points were dropped since last flush", bytesWritten, dropCount);
            }
            else
            {
                Log.Warning("Error flushing {Count}bytes to data streams intake. {Dropped} points were dropped since last flush", bytesWritten, dropCount);
            }
        }
    }

    private async Task ProcessQueueLoopAsync()
    {
        while (true)
        {
            try
            {
                while (_buffer.TryDequeue(out var statsPoint))
                {
                    _aggregator.Add(in statsPoint);
                }

                var flushRequested = Interlocked.CompareExchange(ref _flushRequested, 0, 1);
                if (flushRequested == 1)
                {
                    await WriteToApiAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occured in the processing thread");
            }

            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            _processingMutex.Wait();
            _processingMutex.Reset();
        }
    }
}
