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
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

internal class DataStreamsWriter : IDataStreamsWriter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsWriter>();

    private readonly object _initLock = new();
    private readonly long _bucketDurationMs;
    private readonly BoundedConcurrentQueue<StatsPoint> _buffer = new(queueLimit: 10_000);
    private readonly BoundedConcurrentQueue<BacklogPoint> _backlogBuffer = new(queueLimit: 10_000);
    private readonly DataStreamsAggregator _aggregator;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDataStreamsApi _api;
    private readonly bool _isInDefaultState;

    private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly SemaphoreSlim _backgroundThreadSemaphore = new(0, 1);
    private readonly TimeSpan _releaseTimeout = TimeSpan.FromSeconds(2);
    private TaskCompletionSource<bool>? _currentFlushTcs;
    private MemoryStream? _serializationBuffer;
    private long _pointsDropped;
    private long _flushRequested;
    private Timer? _flushTimer;
    private Thread? _backgroundProcessingThread;

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
        IDiscoveryService discoveryService,
        string defaultServiceName)
        => new(
            settings,
            new DataStreamsAggregator(
                new DataStreamsMessagePackFormatter(settings, profilerSettings, defaultServiceName),
                bucketDurationMs: DataStreamsConstants.DefaultBucketDurationMs),
            new DataStreamsApi(DataStreamsTransportStrategy.GetAgentIntakeFactory(settings.Exporter)),
            bucketDurationMs: DataStreamsConstants.DefaultBucketDurationMs,
            discoveryService);

    private void Initialize()
    {
        lock (_initLock)
        {
            if (_backgroundProcessingThread != null)
            {
                return;
            }

            _backgroundProcessingThread = new Thread(ProcessQueueLoop);
            _backgroundProcessingThread.Start();

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
        // signal exit
        if (!_processExit.TrySetResult(true))
        {
            return;
        }

        // wait for processing thread
        await _backgroundThreadSemaphore.WaitAsync(_releaseTimeout).ConfigureAwait(false);
        _backgroundThreadSemaphore.Dispose();

        // one final flush
        var tsc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ProcessQueue(true, tsc);
        await tsc.Task.WaitAsync(_releaseTimeout).ConfigureAwait(false);
    }

    public async Task FlushAsync()
    {
        await _flushSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var timeout = TimeSpan.FromSeconds(5);

            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            if (!Volatile.Read(ref _isInitialized) || _backgroundProcessingThread == null)
            {
                return;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _currentFlushTcs, tcs);

            RequestFlush();

            var completedTask = await Task.WhenAny(
                tcs.Task,
                _processExit.Task,
                Task.Delay(timeout)).ConfigureAwait(false);

            if (completedTask != tcs.Task)
            {
                Log.Error("Data streams flush timeout after {Timeout}ms", timeout.TotalMilliseconds);
            }
        }
        finally
        {
            _currentFlushTcs = null;
            _flushSemaphore.Release();
        }
    }

    private void RequestFlush()
    {
        Interlocked.Exchange(ref _flushRequested, 1);
    }

    private void WriteToApi()
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

            var success = _api.SendAsync(data).ConfigureAwait(false).GetAwaiter().GetResult();
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

    private void ProcessQueue(bool flush, TaskCompletionSource<bool>? tcs)
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

            if (flush)
            {
                WriteToApi();
                FlushComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured in the processing thread");
        }

        tcs?.TrySetResult(true);
    }

    private void ProcessQueueLoop()
    {
        _backgroundThreadSemaphore.Wait();
        try
        {
            while (!_processExit.Task.IsCompleted)
            {
                var flushRequested = Interlocked.CompareExchange(ref _flushRequested, 0, 1);
                var currentFlushTcs = Volatile.Read(ref _currentFlushTcs);

                ProcessQueue(flushRequested == 1, currentFlushTcs);
                Thread.Sleep(5);
            }
        }
        finally
        {
            _backgroundThreadSemaphore.Release();
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
