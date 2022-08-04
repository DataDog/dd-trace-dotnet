// <copyright file="AgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const TaskCreationOptions TaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentWriter>();

        private static readonly ArraySegment<byte> EmptyPayload;

        private readonly ConcurrentQueue<WorkItem> _pendingTraces = new ConcurrentQueue<WorkItem>();
        private readonly IDogStatsd _statsd;
        private readonly Task _flushTask;
        private readonly Task _serializationTask;
        private readonly TaskCompletionSource<bool> _processExit = new TaskCompletionSource<bool>();

        private readonly IApi _api;

        private readonly SpanBuffer _frontBuffer;
        private readonly SpanBuffer _backBuffer;

        private readonly ManualResetEventSlim _serializationMutex = new ManualResetEventSlim(initialState: false, spinCount: 0);

        private readonly int _batchInterval;
        private readonly IKeepRateCalculator _traceKeepRateCalculator;

        private readonly IStatsAggregator _statsAggregator;

        /// <summary>
        /// The currently active buffer.
        /// Note: Thread-safetiness in this class relies on the fact that only the serialization thread can change the active buffer
        /// </summary>
        private SpanBuffer _activeBuffer;

        private byte[] _temporaryBuffer = new byte[1024];

        private TaskCompletionSource<bool> _forceFlush;

        private Task _frontBufferFlushTask;
        private Task _backBufferFlushTask;

        private long _droppedP0Traces;
        private long _droppedP0Spans;

        private long _droppedSpans;

        static AgentWriter()
        {
            var data = Vendors.MessagePack.MessagePackSerializer.Serialize(Array.Empty<Span[]>());
            EmptyPayload = new ArraySegment<byte>(data);
        }

        public AgentWriter(IApi api, IStatsAggregator statsAggregator, IDogStatsd statsd, bool automaticFlush = true, int maxBufferSize = 1024 * 1024 * 10, int batchInterval = 100)
        : this(api, statsAggregator, statsd, MovingAverageKeepRateCalculator.CreateDefaultKeepRateCalculator(), automaticFlush, maxBufferSize, batchInterval)
        {
        }

        internal AgentWriter(IApi api, IStatsAggregator statsAggregator, IDogStatsd statsd, IKeepRateCalculator traceKeepRateCalculator, bool automaticFlush, int maxBufferSize, int batchInterval)
        {
            _statsAggregator = statsAggregator;

            _api = api;
            _statsd = statsd;
            _batchInterval = batchInterval;
            _traceKeepRateCalculator = traceKeepRateCalculator;

            var formatterResolver = SpanFormatterResolver.Instance;

            _forceFlush = new TaskCompletionSource<bool>(TaskOptions);

            _frontBuffer = new SpanBuffer(maxBufferSize, formatterResolver);
            _backBuffer = new SpanBuffer(maxBufferSize, formatterResolver);
            _activeBuffer = _frontBuffer;

            _serializationTask = automaticFlush ? Task.Factory.StartNew(SerializeTracesLoop, TaskCreationOptions.LongRunning) : Task.CompletedTask;
            _serializationTask.ContinueWith(t => Log.Error(t.Exception, "Error in serialization task"), TaskContinuationOptions.OnlyOnFaulted);

            _flushTask = automaticFlush ? Task.Run(FlushBuffersTaskLoopAsync) : Task.CompletedTask;
            _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in flush task"), TaskContinuationOptions.OnlyOnFaulted);

            _backBufferFlushTask = _frontBufferFlushTask = Task.CompletedTask;
        }

        internal event Action Flushed;

        internal SpanBuffer ActiveBuffer => _activeBuffer;

        internal SpanBuffer FrontBuffer => _frontBuffer;

        internal SpanBuffer BackBuffer => _backBuffer;

        public bool CanComputeStats => _statsAggregator.CanComputeStats ?? false;

        public Task<bool> Ping()
        {
            return _api.SendTracesAsync(EmptyPayload, 0, false, 0, 0);
        }

        public void WriteTrace(ArraySegment<Span> trace, bool shouldSerializeSpans)
        {
            if (trace.Count == 0)
            {
                // If the ArraySegment doesn't have any span we skip it.
                return;
            }

            if (_serializationTask.IsCompleted)
            {
                // Serialization thread is not running, serialize the trace in the current thread
                SerializeTrace(trace, shouldSerializeSpans);
            }
            else
            {
                _pendingTraces.Enqueue(new WorkItem(trace, shouldSerializeSpans));

                if (!_serializationMutex.IsSet)
                {
                    _serializationMutex.Set();
                }
            }

            if (_statsd != null)
            {
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedTraces);
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedSpans, trace.Count);
            }
        }

        public async Task FlushAndCloseAsync()
        {
            if (!_processExit.TrySetResult(true))
            {
                return;
            }

            _serializationMutex.Set();

            var delay = Task.Delay(TimeSpan.FromSeconds(20));

            var completedTask = await Task.WhenAny(_serializationTask, delay)
                .ConfigureAwait(false);

            _traceKeepRateCalculator.CancelUpdates();

            bool success = false;

            if (completedTask != delay)
            {
                await Task.WhenAny(_flushTask, Task.Delay(TimeSpan.FromSeconds(20)))
                    .ConfigureAwait(false);

                if (_frontBuffer.TraceCount != 0 || _backBuffer.TraceCount != 0)
                {
                    // In some situations, the flush thread can exit before flushing all the threads
                    // Force a flush for the leftover traces
                    completedTask = await Task.WhenAny(Task.Run(() => FlushBuffers(flushAllBuffers: true)), delay)
                        .ConfigureAwait(false);

                    if (completedTask != delay)
                    {
                        success = true;
                    }
                }
                else
                {
                    success = true;
                }
            }

            // Once all the spans have been processed, flush the stats
            if (_statsAggregator != null)
            {
                await _statsAggregator.DisposeAsync().ConfigureAwait(false);
            }

            if (!success)
            {
                Log.Warning("Could not flush all traces before process exit");
            }
        }

        public async Task FlushTracesAsync()
        {
            if (!_serializationTask.IsCompleted)
            {
                // Serialization thread is still running
                // Enqueue a watermark to know when it's done serializing all currently enqueued traces
                var tcs = new TaskCompletionSource<bool>(TaskOptions);

                WriteWatermark(() => tcs.TrySetResult(default));

                await tcs.Task.ConfigureAwait(false);
            }

            await FlushBuffers(true).ConfigureAwait(false);
        }

        internal void WriteWatermark(Action watermark, bool wakeUpThread = true)
        {
            _pendingTraces.Enqueue(new WorkItem(watermark));

            if (wakeUpThread)
            {
                _serializationMutex.Set();
            }
        }

        private void RequestFlush()
        {
            _forceFlush.TrySetResult(default);
        }

        private async Task FlushBuffersTaskLoopAsync()
        {
            Task[] tasks = new Task[3];
            tasks[0] = _serializationTask;
            tasks[1] = _forceFlush.Task;

            while (true)
            {
                tasks[2] = Task.Delay(TimeSpan.FromSeconds(1));
                await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks[2] = null;

                if (_forceFlush.Task.IsCompleted)
                {
                    _forceFlush = new TaskCompletionSource<bool>(TaskOptions);
                    tasks[1] = _forceFlush.Task;
                }

                await FlushBuffers().ConfigureAwait(false);

                if (_serializationTask.IsCompleted)
                {
                    return;
                }

                Flushed?.Invoke();
            }
        }

        /// <summary>
        /// Flush the active buffer, and the fallback buffer if full
        /// </summary>
        /// <param name="flushAllBuffers">If set to true, then flush the back buffer even if not full</param>
        /// <returns>Async operation</returns>
        private async Task FlushBuffers(bool flushAllBuffers = false)
        {
            try
            {
                var activeBuffer = Volatile.Read(ref _activeBuffer);
                var fallbackBuffer = activeBuffer == _frontBuffer ? _backBuffer : _frontBuffer;

                // First, flush the back buffer if full
                if (fallbackBuffer.IsFull || flushAllBuffers)
                {
                    await FlushBuffer(fallbackBuffer).ConfigureAwait(false);
                }

                // Then, flush the main buffer
                await FlushBuffer(activeBuffer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unhandled error occurred while flushing trace buffers");
            }
        }

        private async Task FlushBuffer(SpanBuffer buffer)
        {
            if (buffer == _frontBuffer)
            {
                await _frontBufferFlushTask.ConfigureAwait(false);
                await (_frontBufferFlushTask = InternalBufferFlush()).ConfigureAwait(false);
            }
            else
            {
                await _backBufferFlushTask.ConfigureAwait(false);
                await (_backBufferFlushTask = InternalBufferFlush()).ConfigureAwait(false);
            }

            async Task InternalBufferFlush()
            {
                // Wait for write operations to complete, then prevent further modifications
                if (!buffer.Lock())
                {
                    // Buffer is already locked, it's probably being flushed from another thread
                    return;
                }

                try
                {
                    if (_statsd != null)
                    {
                        _statsd.Increment(TracerMetricNames.Queue.DequeuedTraces, buffer.TraceCount);
                        _statsd.Increment(TracerMetricNames.Queue.DequeuedSpans, buffer.SpanCount);
                    }

                    var droppedSpans = Interlocked.Exchange(ref _droppedSpans, 0);

                    if (droppedSpans > 0)
                    {
                        Log.Warning("{count} traces were dropped since the last flush operation.", droppedSpans);
                    }

                    if (buffer.TraceCount > 0)
                    {
                        var droppedP0Traces = Interlocked.Exchange(ref _droppedP0Traces, 0);
                        var droppedP0Spans = Interlocked.Exchange(ref _droppedP0Spans, 0);

                        if (CanComputeStats)
                        {
                            Log.Debug<int, int, long, long>("Flushing {spans} spans across {traces} traces. CanComputeStats is enabled with {droppedP0Traces} droppedP0Traces and {droppedP0Spans} droppedP0Spans", buffer.SpanCount, buffer.TraceCount, droppedP0Traces, droppedP0Spans);
                        }
                        else
                        {
                            Log.Debug<int, int>("Flushing {spans} spans across {traces} traces. CanComputeStats is disabled.", buffer.SpanCount, buffer.TraceCount);
                        }

                        var success = await _api.SendTracesAsync(buffer.Data, buffer.TraceCount, CanComputeStats, droppedP0Traces, droppedP0Spans).ConfigureAwait(false);

                        if (success)
                        {
                            _traceKeepRateCalculator.IncrementKeeps(buffer.TraceCount);
                        }
                        else
                        {
                            _traceKeepRateCalculator.IncrementDrops(buffer.TraceCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An unhandled error occurred while flushing a buffer");
                    _traceKeepRateCalculator.IncrementDrops(buffer.TraceCount);
                }
                finally
                {
                    // Clear and unlock the buffer
                    buffer.Clear();
                }
            }
        }

        private void SerializeTrace(ArraySegment<Span> trace, bool shouldSerializeSpans)
        {
            // Declaring as inline method because only safe to invoke in the context of SerializeTrace
            SpanBuffer SwapBuffers()
            {
                if (_activeBuffer == _frontBuffer)
                {
                    if (!_backBuffer.IsFull)
                    {
                        Volatile.Write(ref _activeBuffer, _backBuffer);
                        return _activeBuffer;
                    }
                }
                else
                {
                    if (!_frontBuffer.IsFull)
                    {
                        Volatile.Write(ref _activeBuffer, _frontBuffer);
                        return _activeBuffer;
                    }
                }

                return null;
            }

            bool forceKeep = _statsAggregator?.AddRange(trace.Array, trace.Offset, trace.Count) ?? false;

            // If stats computation determined that we should drop the P0 Trace,
            // skip all other processing
            if (!shouldSerializeSpans && !forceKeep)
            {
                Interlocked.Increment(ref _droppedP0Traces);
                Interlocked.Add(ref _droppedP0Spans, trace.Count);
                return;
            }

            // Add the current keep rate to the root span
            var rootSpan = trace.Array[trace.Offset].Context.TraceContext?.RootSpan;
            if (rootSpan is not null)
            {
                var currentKeepRate = _traceKeepRateCalculator.GetKeepRate();
                if (rootSpan.Tags is CommonTags commonTags)
                {
                    commonTags.TracesKeepRate = currentKeepRate;
                }
                else
                {
                    rootSpan.Tags.SetMetric(Metrics.TracesKeepRate, currentKeepRate);
                }
            }

            // We use a double-buffering mechanism
            // This allows the serialization thread to keep doing its job while a buffer is being flushed
            var buffer = _activeBuffer;

            if (buffer.TryWrite(trace, ref _temporaryBuffer))
            {
                // Serialization to the primary buffer succeeded
                return;
            }

            // Active buffer is full, swap them
            buffer = SwapBuffers();

            if (buffer != null)
            {
                // One buffer is full, request an eager flush
                RequestFlush();

                if (buffer.TryWrite(trace, ref _temporaryBuffer))
                {
                    // Serialization to the secondary buffer succeeded
                    return;
                }
            }

            // All the buffers are full :( drop the trace
            Interlocked.Increment(ref _droppedSpans);
            _traceKeepRateCalculator.IncrementDrops(1);

            if (_statsd != null)
            {
                _statsd.Increment(TracerMetricNames.Queue.DroppedTraces);
                _statsd.Increment(TracerMetricNames.Queue.DroppedSpans, trace.Count);
            }
        }

        private void SerializeTracesLoop()
        {
            /* Trying to find a compromise between contradictory goals (in order of priority):
             *  - not keeping the traces in the queue for too long
             *  - keeping the overhead of the producer thread to a minimum
             *  - keeping the overhead of the consumer thread to a minimum
             *
             * To achieve this, the thread wakes up every BatchPeriod milliseconds and processes all available traces.
             * If there are no traces, then the mutex is used to sleep for a longer period of time.
             * Having a mutex prevents the thread from waking up if the server receives no traffic.
             * Resetting the mutex only when no traces have been enqueued for a while prevents
             * the producer thread from paying the cost of setting the mutex every time.
             */

            while (true)
            {
                bool hasDequeuedTraces = false;

                try
                {
                    while (_pendingTraces.TryDequeue(out var item))
                    {
                        if (item.Callback != null)
                        {
                            // Found a watermark
                            item.Callback();
                            continue;
                        }

                        hasDequeuedTraces = true;
                        SerializeTrace(item.Trace, item.ShouldSerializeSpans);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occured in the serialization thread");
                }

                if (_processExit.Task.IsCompleted)
                {
                    return;
                }

                if (hasDequeuedTraces)
                {
                    Thread.Sleep(_batchInterval);
                }
                else
                {
                    // No traces were pushed in the last period, wait undefinitely
                    _serializationMutex.Wait();
                    _serializationMutex.Reset();
                }
            }
        }

        private readonly struct WorkItem
        {
            public readonly ArraySegment<Span> Trace;
            public readonly bool ShouldSerializeSpans;
            public readonly Action Callback;

            public WorkItem(ArraySegment<Span> trace, bool shouldSerializeSpans)
            {
                Trace = trace;
                ShouldSerializeSpans = shouldSerializeSpans;
                Callback = null;
            }

            public WorkItem(Action callback)
            {
                Trace = default;
                ShouldSerializeSpans = default;
                Callback = callback;
            }
        }
    }
}
