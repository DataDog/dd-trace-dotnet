using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class AgentWriter : ITraceWriter
    {
#if NET45
        private const TaskCreationOptions TaskOptions = TaskCreationOptions.None;
#else
        private const TaskCreationOptions TaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;
#endif

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

        /// <summary>
        /// The currently active buffer.
        /// Note: Thread-safetiness in this class relies on the fact that only the serialization thread can change the active buffer
        /// </summary>
        private SpanBuffer _activeBuffer;

        private byte[] _temporaryBuffer = new byte[1024];

        private TaskCompletionSource<bool> _forceFlush;

        static AgentWriter()
        {
            var data = Vendors.MessagePack.MessagePackSerializer.Serialize(ArrayHelper.Empty<Span[]>());
            EmptyPayload = new ArraySegment<byte>(data);
        }

        public AgentWriter(IApi api, IDogStatsd statsd, bool automaticFlush = true, int maxBufferSize = 1024 * 1024 * 10, int batchInterval = 100)
        {
            _api = api;
            _statsd = statsd;
            _batchInterval = batchInterval;

            var formatterResolver = SpanFormatterResolver.Instance;

            _forceFlush = new TaskCompletionSource<bool>(TaskOptions);

            _frontBuffer = new SpanBuffer(maxBufferSize, formatterResolver);
            _backBuffer = new SpanBuffer(maxBufferSize, formatterResolver);
            _activeBuffer = _frontBuffer;

            _serializationTask = automaticFlush ? Task.Factory.StartNew(SerializeTracesLoop, TaskCreationOptions.LongRunning) : Task.FromResult(true);
            _serializationTask.ContinueWith(t => Log.Error(t.Exception, "Error in serialization task"), TaskContinuationOptions.OnlyOnFaulted);

            _flushTask = automaticFlush ? Task.Run(FlushBuffersTaskLoopAsync) : Task.FromResult(true);
            _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in flush task"), TaskContinuationOptions.OnlyOnFaulted);
        }

        internal event Action Flushed;

        internal SpanBuffer ActiveBuffer => _activeBuffer;

        internal SpanBuffer FrontBuffer => _frontBuffer;

        internal SpanBuffer BackBuffer => _backBuffer;

        public Task<bool> Ping()
        {
            return _api.SendTracesAsync(EmptyPayload, 0);
        }

        public void WriteTrace(Span[] trace)
        {
            if (_serializationTask.IsCompleted)
            {
                // Serialization thread is not running, serialize the trace in the current thread
                SerializeTrace(trace);
            }
            else
            {
                _pendingTraces.Enqueue(new WorkItem(trace));

                if (!_serializationMutex.IsSet)
                {
                    _serializationMutex.Set();
                }
            }

            if (_statsd != null)
            {
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedTraces);
                _statsd.Increment(TracerMetricNames.Queue.EnqueuedSpans, trace.Length);
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

            if (completedTask != delay)
            {
                await Task.WhenAny(_flushTask, Task.Delay(TimeSpan.FromSeconds(20)))
                    .ConfigureAwait(false);

                if (_frontBuffer.TraceCount == 0 && _backBuffer.TraceCount == 0)
                {
                    // All good
                    return;
                }

                // In some situations, the flush thread can exit before flushing all the threads
                // Force a flush for the leftover traces
                completedTask = await Task.WhenAny(Task.Run(() => FlushBuffers(flushAllBuffers: true)), delay)
                    .ConfigureAwait(false);

                if (completedTask != delay)
                {
                    return;
                }
            }

            Log.Warning("Could not flush all traces before process exit");
        }

        public async Task FlushTracesAsync()
        {
            if (!_serializationTask.IsCompleted)
            {
                // Serialization thread is still running
                // Enqueue a watermark to know when it's done serializing all currently enqueued traces
                var tcs = new TaskCompletionSource<bool>(TaskOptions);

                WriteWatermark(() => CompleteTaskCompletionSource(tcs));

                await tcs.Task.ConfigureAwait(false);
            }

            await FlushBuffers().ConfigureAwait(false);
        }

        internal void WriteWatermark(Action watermark, bool wakeUpThread = true)
        {
            _pendingTraces.Enqueue(new WorkItem(watermark));

            if (wakeUpThread)
            {
                _serializationMutex.Set();
            }
        }

        private static void CompleteTaskCompletionSource<T>(TaskCompletionSource<T> tcs)
        {
#if NET45
            // TaskCreationOptions.RunContinuationsAsynchronously does not exist in .NET 4.5
            // Complete the TCS in a separate thread to prevent the continuation from being inlined
            Task.Run(() => tcs.TrySetResult(default));
#else
            tcs.TrySetResult(default);
#endif
        }

        private void RequestFlush()
        {
            CompleteTaskCompletionSource(_forceFlush);
        }

        private async Task FlushBuffersTaskLoopAsync()
        {
            while (true)
            {
                await Task.WhenAny(
                        Task.Delay(TimeSpan.FromSeconds(1)),
                        _serializationTask,
                        _forceFlush.Task)
                    .ConfigureAwait(false);

                if (_forceFlush.Task.IsCompleted)
                {
                    _forceFlush = new TaskCompletionSource<bool>(TaskOptions);
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

                if (buffer.TraceCount > 0)
                {
                    await _api.SendTracesAsync(buffer.Data, buffer.TraceCount).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unhandled error occurred while flushing a buffer");
            }
            finally
            {
                // Clear and unlock the buffer
                buffer.Clear();
            }
        }

        private void SerializeTrace(Span[] trace)
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
            Log.Warning("Trace buffer is full. Dropping a trace.");

            if (_statsd != null)
            {
                _statsd.Increment(TracerMetricNames.Queue.DroppedTraces);
                _statsd.Increment(TracerMetricNames.Queue.DroppedSpans, trace.Length);
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
                        if (item.Trace == null)
                        {
                            // Found a watermark
                            item.Callback();
                            continue;
                        }

                        hasDequeuedTraces = true;
                        SerializeTrace(item.Trace);
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
            public readonly Span[] Trace;
            public readonly Action Callback;

            public WorkItem(Span[] trace)
            {
                Trace = trace;
                Callback = null;
            }

            public WorkItem(Action callback)
            {
                Trace = null;
                Callback = callback;
            }
        }
    }
}
