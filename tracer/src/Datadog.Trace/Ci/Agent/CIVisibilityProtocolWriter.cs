// <copyright file="CIVisibilityProtocolWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// CI Visibility Protocol Writer
    /// </summary>
    /*
     *  Current Architecture of the writer:
     *
     *         ┌────────────────────────────────────────────────────────────────┐
     *         │                                                                │
     *         │ CIVisibilityProtocolWriter┌────────────────────────────────┐   │
     *         │                           │ Buffers                        │   │
     *         │                         ┌─┤                                │   │
     *         │                         │ │ ┌────────────────────────────┐ │   │
     *         │                         │ │ │ CITestCyclePayload  Buffer │ │   │
     *         │                         │ │ │                            │ │   │
     *         │                         │ │ │         SpanEvent          │ │   │  CITestCyclePayload Url
     *         │                         │ │ │         TestEvent          ├─┼───┼────────────────────────────►
     *         │   ┌───────────────┐     │ │ ├────────────────────────────┤ │   │
     *         │   │               │     │ │ │ Items: inf     Bytes: 5MB  │ │   │
     *         │   │  Event Queue  │     │ │ └────────────────────────────┘ │   │
     * IEvent  │   │               │     │ │                                │   │
     *  ───────┼──►│               ├─────┤►│ ┌────────────────────────────┐ │   │
     *         │   │   Max: 25000  │     │ │ │ CICodeCoveragePayload Buf. │ │   │
     *         │   │               │     │ │ │                            │ │   │
     *         │   └───────────────┘     │ │ │      CoveragePayload       │ │   │  CICodeCoveragePayload Url
     *         │                         │ │ │                            ├─┼───┼────────────────────────────►
     *         │                         │ │ ├────────────────────────────┤ │   │
     *         │                         │ │ │ Items: 100     Bytes: 50MB │ │   │
     *         │                         │ │ └────────────────────────────┘ │   │
     *         │                         │ │                                │   │
     *         │                         │ │ Flush each sec or limit reach  │   │
     *         │                         │ └──────────────────────────────┬─┘   │
     *         │                         │                                │     │
     *         │                         └────────────────────────────────┘     │
     *         │                                   1 .. N Consumers             │
     *         │                                                                │
     *         │                                      Max N = 4                 │
     *         │                                                                │
     *         └────────────────────────────────────────────────────────────────┘
     */
    internal sealed class CIVisibilityProtocolWriter : IEventWriter
    {
        private const int DefaultBatchInterval = 2500;
        private const int DefaultMaxItemsInQueue = 25000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIVisibilityProtocolWriter>();

        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly Buffers[] _buffersArray;
        private readonly int _batchInterval;

        public CIVisibilityProtocolWriter(
            CIVisibilitySettings settings,
            ICIVisibilityProtocolWriterSender sender,
            IFormatterResolver formatterResolver = null,
            int? concurrency = null,
            int batchInterval = DefaultBatchInterval,
            int maxItemsInQueue = DefaultMaxItemsInQueue)
        {
            _eventQueue = new BlockingCollection<IEvent>(maxItemsInQueue);
            _batchInterval = batchInterval;

            // Concurrency Level is a simple algorithm where we select a number between 1 and 4 depending on the number of Logical Processor Count
            // To scale the number of senders with a hard limit.
            var concurrencyLevel = concurrency ?? Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 4);
            _buffersArray = new Buffers[concurrencyLevel];
            for (var i = 0; i < _buffersArray.Length; i++)
            {
                var buffers = new Buffers(
                    sender,
                    new CITestCyclePayload(settings, formatterResolver: formatterResolver),
                    new CICodeCoveragePayload(settings, formatterResolver: formatterResolver));
                _buffersArray[i] = buffers;
                var tskFlush = Task.Run(() => InternalFlushEventsAsync(this, buffers));
                tskFlush.ContinueWith(t => Log.Error(t.Exception, "CIVisibilityProtocolWriter: Error in sending ci visibility events"), TaskContinuationOptions.OnlyOnFaulted);
                _buffersArray[i].SetFlushTask(tskFlush);
            }

            Log.Information<int>("CIVisibilityProtocolWriter Initialized with concurrency level of: {ConcurrencyLevel}", concurrencyLevel);
        }

        public void WriteEvent(IEvent @event)
        {
            if (_eventQueue.IsAddingCompleted)
            {
                return;
            }

            try
            {
                _eventQueue.Add(@event);
                TelemetryFactory.Metrics.RecordCountCIVisibilityEventsEnqueueForSerialization();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CIVisibilityProtocolWriter: Error Writing event in a queue.");
            }
        }

        public async Task FlushAndCloseAsync()
        {
            _eventQueue.CompleteAdding();
            foreach (var buffers in _buffersArray)
            {
                buffers.FlushDelayEvent.Set();
                await buffers.FlushTaskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        public Task FlushTracesAsync()
        {
            if (_eventQueue.IsAddingCompleted)
            {
                return Task.CompletedTask;
            }

            try
            {
                var countdownEvent = new AsyncCountdownEvent(_buffersArray.Length);
                foreach (var buffer in _buffersArray)
                {
                    _eventQueue.Add(new WatermarkEvent(countdownEvent));
                    buffer.FlushDelayEvent.Set();
                }

                return countdownEvent.WaitAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CIVisibilityProtocolWriter: Error Writing event in a queue.");
                return Task.FromException(ex);
            }
        }

        public Task<bool> Ping()
        {
            return Task.FromResult(true);
        }

        public void WriteTrace(ArraySegment<Span> trace)
        {
            // Transform spans to events
            for (var i = trace.Offset; i < trace.Count; i++)
            {
                if (trace.Array is { } array)
                {
                    WriteEvent(CIVisibilityEventsFactory.FromSpan(array[i]));
                }
            }
        }

        private static async Task InternalFlushEventsAsync(CIVisibilityProtocolWriter writer, Buffers buffers)
        {
            var batchInterval = writer._batchInterval;
            var eventQueue = writer._eventQueue;
            var index = buffers.Index;
            var flushDelayEvent = buffers.FlushDelayEvent;
            var ciTestCycleBuffer = buffers.CiTestCycleBuffer;
            var ciTestCycleBufferWatch = buffers.CiTestCycleBufferWatch;
            var ciCodeCoverageBuffer = buffers.CiCodeCoverageBuffer;
            var ciCodeCoverageBufferWatch = buffers.CiCodeCoverageBufferWatch;
            var completionSource = buffers.FlushTaskCompletionSource;

            Log.Debug("CIVisibilityProtocolWriter: InternalFlushEventsAsync/ Starting FlushEventsAsync loop");

            while (!eventQueue.IsCompleted)
            {
                AsyncCountdownEvent watermarkCountDown = null;

                try
                {
                    // Retrieve events from the queue and add them to the respective buffer.
                    ciTestCycleBufferWatch.Restart();
                    ciCodeCoverageBufferWatch.Restart();
                    while (eventQueue.TryTake(out var item))
                    {
                        // ***
                        // Check if the item is a watermark
                        // ***
                        if (item is WatermarkEvent watermarkEvent)
                        {
                            // Flush operation.
                            // We get the countdown event and exit this loop
                            // to flush buffers (in case there's any event)
                            watermarkCountDown = watermarkEvent.Countdown;
                            Log.Debug<int>("CIVisibilityProtocolWriter: Watermark detected on [Buffer: {BufferIndex}]", index);
                            break;
                        }

                        // ***
                        // Force a flush by time (When the queue was never empty in a complete BatchInterval period)
                        // ***
                        var flushWithInterval1 = buffers.FlushCiTestCycleBufferWhenTimeElapsedAsync(batchInterval);
                        var flushWithInterval2 = buffers.FlushCiCodeCoverageBufferWhenTimeElapsedAsync(batchInterval);
                        await flushWithInterval1.ConfigureAwait(false);
                        await flushWithInterval2.ConfigureAwait(false);

                        // ***
                        // Add the item to the right buffer, and flush the buffer in case is needed.
                        // ***
                        if (ciTestCycleBuffer.CanProcessEvent(item))
                        {
                            // The CITestCycle endpoint can process this event, we try to add it to the buffer.
                            // If the item cannot be added to the buffer but the buffer has events
                            // we assume that is full and needs to be flushed.
                            while (!ciTestCycleBuffer.TryProcessEvent(item) && ciTestCycleBuffer.HasEvents)
                            {
                                await buffers.FlushCiTestCycleBufferAsync().ConfigureAwait(false);
                            }

                            continue;
                        }

                        if (ciCodeCoverageBuffer.CanProcessEvent(item))
                        {
                            // The CICodeCoverage track/endpoint can process this event, we try to add it to the buffer.
                            // If the item cannot be added to the buffer but the buffer has events
                            // we assume that is full and needs to be flushed.
                            while (!ciCodeCoverageBuffer.TryProcessEvent(item) && ciCodeCoverageBuffer.HasEvents)
                            {
                                await buffers.FlushCiCodeCoverageBufferAsync().ConfigureAwait(false);
                            }

                            continue;
                        }
                    }

                    // After removing all items from the queue, we check if buffers needs to flushed.
                    var flush1 = buffers.FlushCiTestCycleBufferAsync();
                    var flush2 = buffers.FlushCiCodeCoverageBufferAsync();
                    await flush1.ConfigureAwait(false);
                    await flush2.ConfigureAwait(false);

                    // If there's a flush watermark we marked as resolved.
                    if (watermarkCountDown is not null)
                    {
                        watermarkCountDown.Signal();
                        Log.Debug<int>("CIVisibilityProtocolWriter: Waiting for signals from other buffers [Buffer: {BufferIndex}]", index);
                        await watermarkCountDown.WaitAsync().ConfigureAwait(false);
                        Log.Debug<int>("CIVisibilityProtocolWriter: Signals received, continue processing.. [Buffer: {BufferIndex}]", index);
                    }
                }
                catch (ThreadAbortException ex)
                {
                    completionSource?.TrySetException(ex);
                    watermarkCountDown?.Signal();
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "CIVisibilityProtocolWriter: Error in InternalFlushEventsAsync");

                    // If there's a flush watermark we marked as resolved.
                    if (watermarkCountDown is not null)
                    {
                        watermarkCountDown.Signal();
                        Log.Debug<int>("CIVisibilityProtocolWriter: Waiting for signals from other buffers [Buffer: {BufferIndex}]", index);
                        await watermarkCountDown.WaitAsync().ConfigureAwait(false);
                        Log.Debug<int>("CIVisibilityProtocolWriter: Signals received, continue processing.. [Buffer: {BufferIndex}]", index);
                    }
                }
                finally
                {
                    if (watermarkCountDown is null)
                    {
                        // In case there's no flush watermark, we wait before start processing new events.
                        await flushDelayEvent.WaitAsync(batchInterval).ConfigureAwait(false);
                        flushDelayEvent.Reset();
                    }
                }
            }

            completionSource?.TrySetResult(true);
            Log.Debug("CIVisibilityProtocolWriter: InternalFlushEventsAsync/ Finishing FlushEventsAsync loop");
        }

        internal class WatermarkEvent : IEvent
        {
            public WatermarkEvent(AsyncCountdownEvent countdownEvent)
            {
                Countdown = countdownEvent;
            }

            public AsyncCountdownEvent Countdown { get; }
        }

        private class Buffers
        {
            private static int _globalIndexes = 0;
            private readonly ICIVisibilityProtocolWriterSender _sender;

            public Buffers(ICIVisibilityProtocolWriterSender sender, CIVisibilityProtocolPayload ciTestCycleBuffer, CICodeCoveragePayload ciCodeCoverageBuffer)
            {
                _sender = sender;
                Index = Interlocked.Increment(ref _globalIndexes);
                FlushDelayEvent = new AsyncManualResetEvent(false);
                CiTestCycleBuffer = ciTestCycleBuffer;
                CiTestCycleBufferWatch = Stopwatch.StartNew();
                CiCodeCoverageBuffer = ciCodeCoverageBuffer;
                CiCodeCoverageBufferWatch = Stopwatch.StartNew();
                FlushTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                FlushTask = null;
            }

            public int Index { get; }

            public AsyncManualResetEvent FlushDelayEvent { get; }

            public CIVisibilityProtocolPayload CiTestCycleBuffer { get; }

            public Stopwatch CiTestCycleBufferWatch { get; }

            public CICodeCoveragePayload CiCodeCoverageBuffer { get; }

            public Stopwatch CiCodeCoverageBufferWatch { get; }

            public TaskCompletionSource<bool> FlushTaskCompletionSource { get; }

            public Task FlushTask { get; private set; }

            internal void SetFlushTask(Task flushTask)
            {
                FlushTask = flushTask;
            }

            public Task FlushCiTestCycleBufferWhenTimeElapsedAsync(int batchInterval)
            {
                return CiTestCycleBufferWatch.ElapsedMilliseconds >= batchInterval ?
                           FlushCiTestCycleBufferAsync() : Task.CompletedTask;
            }

            public Task FlushCiTestCycleBufferAsync()
            {
                return CiTestCycleBuffer.HasEvents ? InternalFlushCiTestCycleBufferAsync() : Task.CompletedTask;

                async Task InternalFlushCiTestCycleBufferAsync()
                {
                    await _sender.SendPayloadAsync(CiTestCycleBuffer).ConfigureAwait(false);
                    CiTestCycleBuffer.Reset();
                    CiTestCycleBufferWatch.Restart();
                }
            }

            public Task FlushCiCodeCoverageBufferWhenTimeElapsedAsync(int batchInterval)
            {
                return CiCodeCoverageBufferWatch.ElapsedMilliseconds >= batchInterval ?
                           FlushCiCodeCoverageBufferAsync() : Task.CompletedTask;
            }

            public Task FlushCiCodeCoverageBufferAsync()
            {
                return CiCodeCoverageBuffer.HasEvents ? InternalFlushCiCodeCoverageBufferAsync() : Task.CompletedTask;

                async Task InternalFlushCiCodeCoverageBufferAsync()
                {
                    await _sender.SendPayloadAsync(CiCodeCoverageBuffer).ConfigureAwait(false);
                    CiCodeCoverageBuffer.Reset();
                    CiCodeCoverageBufferWatch.Restart();
                }
            }
        }
    }
}
