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
     *         │   │               │     │ │ │ Items: inf     Bytes: 4MB  │ │   │
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
        private const int BatchInterval = 1000;
        private const int MaxItemsInQueue = 2500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIAgentlessWriter>();

        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly AutoResetEvent _flushDelayEvent;
        private readonly Buffers[] _buffersArray;

        public CIAgentlessWriter(ICIAgentlessWriterSender sender, IFormatterResolver formatterResolver = null, int? concurrency = null)
        {
            _eventQueue = new BlockingCollection<IEvent>(MaxItemsInQueue);
            _flushDelayEvent = new AutoResetEvent(false);

            // Concurrency Level is a simple algorithm where we select a number between 1 and 8 depending on the number of Logical Processor Count
            // To scale the number of senders with a hard limit.
            var concurrencyLevel = concurrency ?? Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 8);
            _buffersArray = new Buffers[concurrencyLevel];
            for (var i = 0; i < _buffersArray.Length; i++)
            {
                _buffersArray[i] = new Buffers(
                    sender,
                    new CITestCyclePayload(formatterResolver: formatterResolver),
                    new CICodeCoveragePayload(formatterResolver: formatterResolver));
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
            _flushDelayEvent.Set();
            foreach (var buffers in _buffersArray)
            {
                await buffers.FlushTaskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        public Task FlushTracesAsync()
        {
            var wme = new WatermarkEvent();
            if (_eventQueue.IsAddingCompleted)
            {
                return Task.CompletedTask;
            }

            try
            {
                _eventQueue.Add(wme);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Writing event in a queue.");
                return Task.FromException(ex);
            }

            _flushDelayEvent.Set();
            return wme.CompletionSource.Task;
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
            var eventQueue = writer._eventQueue;
            var flushDelayEvent = writer._flushDelayEvent;
            var buffers = (Buffers)stateArray[1];
            var ciTestCycleBuffer = buffers.CiTestCycleBuffer;
            var ciTestCycleBufferWatch = buffers.CiTestCycleBufferWatch;
            var ciCodeCoverageBuffer = buffers.CiCodeCoverageBuffer;
            var ciCodeCoverageBufferWatch = buffers.CiCodeCoverageBufferWatch;
            var completionSource = buffers.FlushTaskCompletionSource;

            Log.Debug("CIAgentlessWriter:: InternalFlushEventsAsync/ Starting FlushEventsAsync loop");

            while (!eventQueue.IsCompleted)
            {
                TaskCompletionSource<bool> watermarkCompletion = null;

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
                            // We get the completion source and exit this loop
                            // to flush buffers (in case there's any event)
                            watermarkCompletion = watermarkEvent.CompletionSource;
                            break;
                        }

                        // ***
                        // Force a flush by time (When the queue was never empty in a complete BatchInterval period)
                        // ***
                        await Task.WhenAll(
                            buffers.FlushCiTestCycleBufferWhenTimeElapsedAsync(BatchInterval),
                            buffers.FlushCiCodeCoverageBufferWhenTimeElapsedAsync(BatchInterval)).ConfigureAwait(false);

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
                    watermarkCompletion?.TrySetResult(true);
                }
                catch (ThreadAbortException ex)
                {
                    completionSource?.TrySetException(ex);
                    watermarkCompletion?.TrySetException(ex);
                    throw;
                }
                catch (Exception ex)
                {
                    watermarkCompletion?.TrySetException(ex);
                }
                finally
                {
                    if (watermarkCompletion is null)
                    {
                        // In case there's no flush watermark, we wait before start procesing new events.
                        flushDelayEvent.WaitOne(BatchInterval, true);
                    }
                    else
                    {
                        // Because the flush interrupts the dequeueing process we don't wait
                        // and start processing events again.
                        watermarkCompletion = null;
                    }
                }
            }

            completionSource?.TrySetResult(true);
            Log.Debug("CIAgentlessWriter:: InternalFlushEventsAsync/ Finishing FlushEventsAsync loop");
        }

        internal class WatermarkEvent : IEvent
        {
            public WatermarkEvent()
            {
                CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public TaskCompletionSource<bool> CompletionSource { get; }
        }

        private class Buffers
        {
            private readonly ICIAgentlessWriterSender _sender;

            public Buffers(ICIAgentlessWriterSender sender, CIVisibilityProtocolPayload ciTestCycleBuffer, CICodeCoveragePayload ciCodeCoverageBuffer)
            {
                _sender = sender;
                CiTestCycleBuffer = ciTestCycleBuffer;
                CiTestCycleBufferWatch = Stopwatch.StartNew();
                CiCodeCoverageBuffer = ciCodeCoverageBuffer;
                CiCodeCoverageBufferWatch = Stopwatch.StartNew();
                FlushTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                FlushTask = null;
            }

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
                    CiTestCycleBuffer.Clear();
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
                    CiCodeCoverageBuffer.Clear();
                    CiCodeCoverageBufferWatch.Restart();
                }
            }
        }
    }
}
