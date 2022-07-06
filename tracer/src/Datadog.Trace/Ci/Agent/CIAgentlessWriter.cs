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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// CI Visibility Agentless Writer
    /// </summary>
    internal sealed class CIAgentlessWriter : IEventWriter
    {
        private const int BatchInterval = 1000;
        private const int MaxItemsInQueue = 2500;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIAgentlessWriter>();

        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly AutoResetEvent _flushDelayEvent;
        private readonly Buffers[] _buffersArray;
        private readonly ICIAgentlessWriterSender _sender;

        public CIAgentlessWriter(ICIAgentlessWriterSender sender, IFormatterResolver formatterResolver = null)
        {
            _eventQueue = new BlockingCollection<IEvent>(MaxItemsInQueue);
            _flushDelayEvent = new AutoResetEvent(false);
            _sender = sender;

            var concurrencyLevel = Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 8);
            _buffersArray = new Buffers[concurrencyLevel];
            for (var i = 0; i < _buffersArray.Length; i++)
            {
                _buffersArray[i] = new Buffers(new CITestCyclePayload(formatterResolver), new CICodeCoveragePayload(formatterResolver));
                var tskFlush = Task.Factory.StartNew(InternalFlushEventsAsync, new object[] { this, _buffersArray[i] }, TaskCreationOptions.LongRunning);
                tskFlush.ContinueWith(t => Log.Error(t.Exception, "Error in sending ciapp events"), TaskContinuationOptions.OnlyOnFaulted);
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
                if (trace.Array[i].Type == SpanTypes.Test)
                {
                    WriteEvent(new TestEvent(trace.Array[i]));
                }
                else
                {
                    WriteEvent(new SpanEvent(trace.Array[i]));
                }
            }
        }

        private static async Task InternalFlushEventsAsync(object state)
        {
            var stateArray = (object[])state;
            var writer = (CIAgentlessWriter)stateArray[0];
            var eventQueue = writer._eventQueue;
            var flushDelayEvent = writer._flushDelayEvent;
            var sender = writer._sender;
            var buffers = (Buffers)stateArray[1];
            var ciTestCycleBuffer = buffers.CiTestCycleBuffer;
            var ciCodeCoverageBuffer = buffers.CICodeCoverageBuffer;
            var completionSource = buffers.FlushTaskCompletionSource;

            Log.Debug("CIAgentlessWriter:: InternalFlushEventsAsync/ Starting FlushEventsAsync loop");

            var ciTestCycleBufferWatch = Stopwatch.StartNew();
            var ciCodeCoverageBufferWatch = Stopwatch.StartNew();

            async Task FlushCITestCycleBufferAsync()
            {
                if (ciTestCycleBuffer.HasEvents)
                {
                    await sender.SendPayloadAsync(ciTestCycleBuffer).ConfigureAwait(false);
                    ciTestCycleBuffer.Clear();
                    ciTestCycleBufferWatch.Restart();
                }
            }

            async Task FlushCICodeCoverageBufferAsync()
            {
                if (ciCodeCoverageBuffer.HasEvents)
                {
                    await sender.SendPayloadAsync(ciCodeCoverageBuffer).ConfigureAwait(false);
                    ciCodeCoverageBuffer.Clear();
                    ciCodeCoverageBufferWatch.Restart();
                }
            }

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
                        // Force a flush by time (When there's always items in the queue)
                        // ***
                        if (ciTestCycleBufferWatch.ElapsedMilliseconds >= BatchInterval)
                        {
                            await FlushCITestCycleBufferAsync().ConfigureAwait(false);
                        }

                        if (ciCodeCoverageBufferWatch.ElapsedMilliseconds >= BatchInterval)
                        {
                            await FlushCICodeCoverageBufferAsync().ConfigureAwait(false);
                        }

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
                                await FlushCITestCycleBufferAsync().ConfigureAwait(false);
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
                                await FlushCICodeCoverageBufferAsync().ConfigureAwait(false);
                            }

                            continue;
                        }
                    }

                    // After removing all items from the queue, we check if buffers needs to flushed.
                    await Task.WhenAll(
                        FlushCITestCycleBufferAsync(),
                        FlushCICodeCoverageBufferAsync()).ConfigureAwait(false);

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
            public Buffers(CIVisibilityProtocolPayload ciTestCycleBuffer, CICodeCoveragePayload ciCodeCoverageBuffer)
            {
                CiTestCycleBuffer = ciTestCycleBuffer;
                CICodeCoverageBuffer = ciCodeCoverageBuffer;
                FlushTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                FlushTask = null;
            }

            public CIVisibilityProtocolPayload CiTestCycleBuffer { get; }

            public CICodeCoveragePayload CICodeCoverageBuffer { get; }

            public TaskCompletionSource<bool> FlushTaskCompletionSource { get; }

            public Task FlushTask { get; private set; }

            internal void SetFlushTask(Task flushTask)
            {
                FlushTask = flushTask;
            }
        }
    }
}
