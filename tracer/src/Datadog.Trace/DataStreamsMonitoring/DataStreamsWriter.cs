// <copyright file="DataStreamsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

internal sealed class DataStreamsWriter : IDataStreamsWriter
{
    private const TaskCreationOptions TaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsWriter>();

    private readonly object _initLock = new();
    private readonly long _bucketDurationMs;
    private readonly BoundedConcurrentQueue<StatsPoint> _buffer = new(queueLimit: 10_000);
    private readonly BoundedConcurrentQueue<BacklogPoint> _backlogBuffer = new(queueLimit: 10_000);
    private readonly BoundedConcurrentQueue<DataStreamsTransactionInfo> _transactionBuffer = new(queueLimit: 10_000);
    private readonly TimeSpan _waitTimeSpan = TimeSpan.FromMilliseconds(10);
    private readonly TimeSpan _flushSemaphoreWaitTime = TimeSpan.FromSeconds(1);
    private readonly DataStreamsAggregator _aggregator;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDataStreamsApi _api;
    private readonly bool _isInDefaultState;

    private readonly TaskCompletionSource<bool> _processExit = new(TaskOptions);
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private MemoryStream? _serializationBuffer;
    private long _pointsDropped;
    private Task? _processTask;
    private Task? _flushTask;
    private Timer? _flushTimer;
    private TaskCompletionSource<bool> _forceFlush = new(TaskOptions);

    private int _isSupported = SupportState.Unknown;
    private bool _isInitialized;

    public DataStreamsWriter(
        TracerSettings settings,
        DataStreamsAggregator aggregator,
        IDataStreamsApi api,
        long bucketDurationMs,
        IDiscoveryService discoveryService)
    {
        _isInDefaultState = settings.IsDataStreamsMonitoringInDefaultState;
        _aggregator = aggregator;
        _api = api;
        _discoveryService = discoveryService;
        _discoveryService.SubscribeToChanges(HandleConfigUpdate);
        _bucketDurationMs = bucketDurationMs;
    }

    /// <summary>
    /// Public for testing only
    /// </summary>
    public event EventHandler<EventArgs>? FlushComplete;

    /// <summary>
    /// Gets the number of points dropped due to a full buffer or disabled DSM.
    /// Public for testing only
    /// </summary>
    public long PointsDropped => Interlocked.Read(ref _pointsDropped);

    public static DataStreamsWriter Create(
        TracerSettings settings,
        ProfilerSettings profilerSettings,
        IDiscoveryService discoveryService)
        => new(
            settings,
            new DataStreamsAggregator(
                new DataStreamsMessagePackFormatter(settings, profilerSettings),
                bucketDurationMs: DataStreamsConstants.DefaultBucketDurationMs),
            new DataStreamsApi(settings.Manager, DataStreamsTransportStrategy.GetAgentIntakeFactory),
            bucketDurationMs: DataStreamsConstants.DefaultBucketDurationMs,
            discoveryService);

    private void Initialize()
    {
        lock (_initLock)
        {
            if (_processTask != null)
            {
                return;
            }

            _processTask = Task.Factory.StartNew(ProcessQueueLoop, TaskCreationOptions.LongRunning);
            _processTask.ContinueWith(t => Log.Error(t.Exception, "Error in processing task"), TaskContinuationOptions.OnlyOnFaulted);

            _flushTask = Task.Run(FlushTaskLoopAsync);
            _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in data streams flush task"), TaskContinuationOptions.OnlyOnFaulted);

            _flushTimer = new Timer(
                x => ((DataStreamsWriter)x!).RequestFlush(),
                this,
                dueTime: _bucketDurationMs,
                period: _bucketDurationMs);

            Volatile.Write(ref _isInitialized, true);
        }
    }

    public void Add(in StatsPoint point)
    {
        if (Volatile.Read(ref _isSupported) != SupportState.Unsupported)
        {
            if (!Volatile.Read(ref _isInitialized))
            {
                Initialize();
            }

            if (_buffer.TryEnqueue(point))
            {
                return;
            }
        }

        Interlocked.Increment(ref _pointsDropped);
    }

    public void AddTransaction(in DataStreamsTransactionInfo transaction)
    {
        Log.Debug(@"### Processing data streams transaction {Transaction}", transaction);
        if (!Volatile.Read(ref _isInitialized))
        {
            Initialize();
        }

        if (Volatile.Read(ref _isSupported) != SupportState.Unsupported)
        {
            if (_transactionBuffer.TryEnqueue(transaction))
            {
                return;
            }
        }

        Interlocked.Increment(ref _pointsDropped);
    }

    public void AddBacklog(in BacklogPoint point)
    {
        if (!Volatile.Read(ref _isInitialized))
        {
            Initialize();
        }

        if (Volatile.Read(ref _isSupported) != SupportState.Unsupported)
        {
            if (_backlogBuffer.TryEnqueue(point))
            {
                return;
            }
        }

        Interlocked.Increment(ref _pointsDropped);
    }

    public async Task DisposeAsync()
    {
        _discoveryService.RemoveSubscription(HandleConfigUpdate);
#if NETCOREAPP3_1_OR_GREATER
        if (_flushTimer != null)
        {
            await _flushTimer.DisposeAsync().ConfigureAwait(false);
        }
#else
        _flushTimer?.Dispose();
#endif
        await FlushAndCloseAsync().ConfigureAwait(false);
        _flushSemaphore.Dispose();
    }

    private async Task FlushAndCloseAsync()
    {
        if (!_processExit.TrySetResult(true))
        {
            return;
        }

        // nothing else to do, since the writer was not fully initialized
        if (!Volatile.Read(ref _isInitialized) || _processTask == null || _flushTask == null)
        {
            return;
        }

        var allTasks = Task.WhenAll(_processTask, _flushTask);
        var completedTask = await Task.WhenAny(
                                           allTasks,
                                           Task.Delay(TimeSpan.FromSeconds(1)))
                                      .ConfigureAwait(false);

        if (completedTask != allTasks)
        {
            Log.Warning("Could not flush all data streams stats before process exit");
        }

        await FlushAsync().ConfigureAwait(false);
    }

    private void RequestFlush()
    {
        _forceFlush.TrySetResult(true);
    }

    private async Task FlushTaskLoopAsync()
    {
        Task[] tasks = new Task[2];
        tasks[0] = _processTask!;
        tasks[1] = _forceFlush.Task;

        while (true)
        {
            await Task.WhenAny(tasks).ConfigureAwait(false);

            if (_forceFlush.Task.IsCompleted)
            {
                _forceFlush = new TaskCompletionSource<bool>(TaskOptions);
                tasks[1] = _forceFlush.Task;
            }

            await FlushAggregatorAsync().ConfigureAwait(false);

            if (_processTask!.IsCompleted)
            {
                return;
            }
        }
    }

    public async Task FlushAsync()
    {
        if (!Volatile.Read(ref _isInitialized) || _processTask == null)
        {
            return;
        }

        if (await _flushSemaphore.WaitAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false))
        {
            try
            {
                while (_buffer.TryDequeue(out var statsPoint))
                {
                    _aggregator.Add(in statsPoint);
                }

                while (_backlogBuffer.TryDequeue(out var backlogPoint))
                {
                    _aggregator.AddBacklog(in backlogPoint);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occured while processing data streams buffers");
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        await FlushAggregatorAsync().ConfigureAwait(false);
    }

    private async Task FlushAggregatorAsync()
    {
        if (!await _flushSemaphore.WaitAsync(_flushSemaphoreWaitTime).ConfigureAwait(false))
        {
            Log.Warning("Data streams flush timeout");
            return;
        }

        try
        {
            await WriteToApiAsync().ConfigureAwait(false);
            FlushComplete?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured while writing data streams");
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    private async Task WriteToApiAsync()
    {
        // This method blocks ingestion of new stats points into the aggregator,
        // but they will continue to be added to the queue, and will be processed later
        // Default buffer capacity matches Java implementation:
        // https://cs.github.com/DataDog/dd-trace-java/blob/3386bd137e58ed7450d1704e269d3567aeadf4c0/dd-trace-core/src/main/java/datadog/trace/core/datastreams/MsgPackDatastreamsPayloadWriter.java?q=MsgPackDatastreamsPayloadWriter#L28
        _serializationBuffer ??= new MemoryStream(capacity: 512 * 1024);

        var flushTimeNs = _processExit.Task.IsCompleted
                              ? long.MaxValue // flush all buckets
                              : DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(); // don't flush current bucket

        bool wasDataWritten;
        _serializationBuffer.SetLength(0); // reset the stream
        using (var gzip = new GZipStream(_serializationBuffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            wasDataWritten = _aggregator.Serialize(gzip, flushTimeNs);
        }

        if (wasDataWritten && (Volatile.Read(ref _isSupported) == SupportState.Supported))
        {
            // This flushes on the same thread as the processing loop
            var data = new ArraySegment<byte>(_serializationBuffer.GetBuffer(), offset: 0, (int)_serializationBuffer.Length);

            var success = await _api.SendAsync(data).ConfigureAwait(false);

            var dropCount = Interlocked.Exchange(ref _pointsDropped, 0);
            if (success)
            {
                Log.Debug("Flushed {Count}bytes to data streams intake. {Dropped} points were dropped since last flush", data.Count, dropCount);
            }
            else
            {
                Log.Warning("Error flushing {Count}bytes to data streams intake. {Dropped} points were dropped since last flush", data.Count, dropCount);
            }
        }
    }

    private void ProcessQueueLoop()
    {
        while (true)
        {
            Thread.Sleep(_waitTimeSpan);

            if (!_flushSemaphore.Wait(_flushSemaphoreWaitTime))
            {
                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                continue;
            }

            try
            {
                while (_buffer.TryDequeue(out var statsPoint))
                {
                    _aggregator.Add(in statsPoint);
                }

                while (_backlogBuffer.TryDequeue(out var backlogPoint))
                {
                    _aggregator.AddBacklog(in backlogPoint);
                }

                while (_transactionBuffer.TryDequeue(out var transactionPoint))
                {
                    Log.Debug("### Aggregating transaction id {Id}", transactionPoint.TransactionId);
                    _aggregator.AddTransaction(transactionPoint);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occured in the data streams processing thread");
            }
            finally
            {
                _flushSemaphore.Release();
            }

            if (_processExit.Task.IsCompleted)
            {
                return;
            }
        }
    }

    private void HandleConfigUpdate(AgentConfiguration config)
    {
        var isSupported = string.IsNullOrEmpty(config.DataStreamsMonitoringEndpoint)
                              ? SupportState.Unsupported
                              : SupportState.Supported;
        var wasSupported = Volatile.Read(ref _isSupported);

        if (isSupported != wasSupported)
        {
            _isSupported = isSupported;
            if (isSupported == SupportState.Supported)
            {
                Log.Information("Data streams monitoring supported, enabling flush");
            }
            else
            {
                const string msg = "Data streams monitoring was enabled but is not supported by the Agent. Disabling Data streams. " +
                          "Consider upgrading your Datadog Agent to at least version 7.34.0+";
                if (_isInDefaultState)
                {
                    Log.Information(msg);
                }
                else
                {
                    Log.Warning(msg);
                }
            }
        }
    }

    private static class SupportState
    {
        public const int Unknown = 0;
        public const int Supported = 1;
        public const int Unsupported = 2;
    }
}
