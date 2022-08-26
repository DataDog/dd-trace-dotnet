// <copyright file="CIAgentlessWriter.cs" company="Datadog">
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
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// CI Visibility Agentless Writer
    /// </summary>
    /*
     *  Current Architecture of the writer:
     *
     *         ┌────────────────────────────────────────────────────────────────┐
     *         │                                                                │
     *         │ CIAgentlessWriter         ┌────────────────────────────────┐   │
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
     *         │   │   Max: 2500   │     │ │ │ CICodeCoveragePayload Buf. │ │   │
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
     *         │                                      Max N = 8                 │
     *         │                                                                │
     *         └────────────────────────────────────────────────────────────────┘
     */
    internal sealed class CIAgentlessWriter : IEventWriter
    {
        private const int DefaultBatchInterval = 1000;
        private const int DefaultMaxItemsInQueue = 2500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIAgentlessWriter>();

        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly Buffers[] _buffersArray;
        private readonly int _batchInterval;

        public CIAgentlessWriter(
            CIVisibilitySettings settings,
            ICIAgentlessWriterSender sender,
            IFormatterResolver formatterResolver = null,
            int? concurrency = null,
            int batchInterval = DefaultBatchInterval,
            int maxItemsInQueue = DefaultMaxItemsInQueue)
        {
            _eventQueue = new BlockingCollection<IEvent>(maxItemsInQueue);
            _batchInterval = batchInterval;

            // Concurrency Level is a simple algorithm where we select a number between 1 and 8 depending on the number of Logical Processor Count
            // To scale the number of senders with a hard limit.
            var concurrencyLevel = concurrency ?? Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 8);
            _buffersArray = new Buffers[concurrencyLevel];
            for (var i = 0; i < _buffersArray.Length; i++)
            {
                _buffersArray[i] = new Buffers(
                    sender,
                    new CITestCyclePayload(settings, formatterResolver: formatterResolver),
                    new CICodeCoveragePayload(settings, formatterResolver: formatterResolver));
                var tskFlush = Task.Factory.StartNew(InternalFlushEventsAsync, new object[] { this, _buffersArray[i] }, TaskCreationOptions.LongRunning);
                tskFlush.ContinueWith(t => Log.Error(t.Exception, "Error in sending ci visibility events"), TaskContinuationOptions.OnlyOnFaulted);
                _buffersArray[i].SetFlushTask(tskFlush);
            }

            Log.Information<int>($"CIAgentlessWriter Initialized with concurrency level of: {concurrencyLevel}", concurrencyLevel);
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Writing event in a queue.");
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
                var countdownEvent = new CountdownEvent(_buffersArray.Length);
                var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Task.Factory.StartNew(
                    state =>
                    {
                        var argsArray = (object[])state;
                        var cev = (CountdownEvent)argsArray[0];
                        var tcs = (TaskCompletionSource<bool>)argsArray[1];
                        try
                        {
                            cev.Wait();
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    },
                    new object[] { countdownEvent, completionSource },
                    TaskCreationOptions.DenyChildAttach);

                foreach (var buffer in _buffersArray)
                {
                    _eventQueue.Add(new WatermarkEvent(countdownEvent));
                    buffer.FlushDelayEvent.Set();
                }

                return completionSource.Task;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Writing event in a queue.");
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
                    if (array[i].Type == SpanTypes.Test)
                    {
                        WriteEvent(new TestEvent(array[i]));
                    }
                    else
                    {
                        WriteEvent(new SpanEvent(array[i]));
                    }
                }
            }
        }

        private static async Task InternalFlushEventsAsync(object state)
        {
            var stateArray = (object[])state;
            var writer = (CIAgentlessWriter)stateArray[0];
            var batchInterval = writer._batchInterval;
            var eventQueue = writer._eventQueue;
            var buffers = (Buffers)stateArray[1];
            var index = buffers.Index;
            var flushDelayEvent = buffers.FlushDelayEvent;
            var ciTestCycleBuffer = buffers.CiTestCycleBuffer;
            var ciTestCycleBufferWatch = buffers.CiTestCycleBufferWatch;
            var ciCodeCoverageBuffer = buffers.CiCodeCoverageBuffer;
            var ciCodeCoverageBufferWatch = buffers.CiCodeCoverageBufferWatch;
            var completionSource = buffers.FlushTaskCompletionSource;

            Log.Debug("CIAgentlessWriter: InternalFlushEventsAsync/ Starting FlushEventsAsync loop");

            while (!eventQueue.IsCompleted)
            {
                CountdownEvent watermarkCountDown = null;

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
                            Log.Debug<int?>("CIAgentlessWriter: Watermark detected on [Buffer: {bufferIndex}]", index);
                            break;
                        }

                        // ***
                        // Force a flush by time (When the queue was never empty in a complete BatchInterval period)
                        // ***
                        await Task.WhenAll(
                            buffers.FlushCiTestCycleBufferWhenTimeElapsedAsync(batchInterval),
                            buffers.FlushCiCodeCoverageBufferWhenTimeElapsedAsync(batchInterval)).ConfigureAwait(false);

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
                    await Task.WhenAll(
                        buffers.FlushCiTestCycleBufferAsync(),
                        buffers.FlushCiCodeCoverageBufferAsync()).ConfigureAwait(false);

                    // If there's a flush watermark we marked as resolved.
                    if (watermarkCountDown is not null)
                    {
                        watermarkCountDown.Signal();
                        Log.Debug<int?>("CIAgentlessWriter: Waiting for signals from other buffers [Buffer: {bufferIndex}]", index);
                        watermarkCountDown.Wait();
                        Log.Debug<int?>("CIAgentlessWriter: Signals received, continue processing.. [Buffer: {bufferIndex}]", index);
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
                    Log.Warning(ex, "Error in CIAgentlessWriter.InternalFlushEventsAsync");

                    // If there's a flush watermark we marked as resolved.
                    if (watermarkCountDown is not null)
                    {
                        watermarkCountDown.Signal();
                        Log.Debug<int?>("CIAgentlessWriter: Waiting for signals from other buffers [Buffer: {bufferIndex}]", index);
                        watermarkCountDown.Wait();
                        Log.Debug<int?>("CIAgentlessWriter: Signals received, continue processing.. [Buffer: {bufferIndex}]", index);
                    }
                }
                finally
                {
                    if (watermarkCountDown is null)
                    {
                        // In case there's no flush watermark, we wait before start procesing new events.
                        flushDelayEvent.WaitOne(batchInterval, true);
                    }
                }
            }

            completionSource?.TrySetResult(true);
            Log.Debug("CIAgentlessWriter: InternalFlushEventsAsync/ Finishing FlushEventsAsync loop");
        }

        internal class WatermarkEvent : IEvent
        {
            public WatermarkEvent(CountdownEvent countdownEvent)
            {
                Countdown = countdownEvent;
            }

            public CountdownEvent Countdown { get; }
        }

        private class Buffers
        {
            private static int _globalIndexes = 0;
            private readonly ICIAgentlessWriterSender _sender;

            public Buffers(ICIAgentlessWriterSender sender, CIVisibilityProtocolPayload ciTestCycleBuffer, CICodeCoveragePayload ciCodeCoverageBuffer)
            {
                _sender = sender;
                Index = Interlocked.Increment(ref _globalIndexes);
                FlushDelayEvent = new AutoResetEvent(false);
                CiTestCycleBuffer = ciTestCycleBuffer;
                CiTestCycleBufferWatch = Stopwatch.StartNew();
                CiCodeCoverageBuffer = ciCodeCoverageBuffer;
                CiCodeCoverageBufferWatch = Stopwatch.StartNew();
                FlushTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                FlushTask = null;
            }

            public int Index { get; }

            public AutoResetEvent FlushDelayEvent { get; }

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

            public async Task FlushCiTestCycleBufferAsync()
            {
                if (CiTestCycleBuffer.HasEvents)
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

            public async Task FlushCiCodeCoverageBufferAsync()
            {
                if (CiCodeCoverageBuffer.HasEvents)
                {
                    await _sender.SendPayloadAsync(CiCodeCoverageBuffer).ConfigureAwait(false);
                    CiCodeCoverageBuffer.Reset();
                    CiCodeCoverageBufferWatch.Restart();
                }
            }
        }
    }
}
