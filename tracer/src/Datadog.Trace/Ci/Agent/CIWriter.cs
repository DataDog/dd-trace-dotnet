// <copyright file="CIWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Agent
{
    internal abstract class CIWriter : IAgentWriter, ICIAppWriter
    {
        private const int BatchInterval = 1000;
        private const int MaxItemsPerBatch = 1000;

        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIWriter>();

        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly TaskCompletionSource<bool> _flushTaskCompletionSource;
        private readonly Task _periodicFlush;
        private readonly AutoResetEvent _flushDelayEvent;

        public CIWriter(ImmutableTracerSettings settings, ISampler sampler)
        {
            _eventQueue = new BlockingCollection<IEvent>(MaxItemsPerBatch * 2);
            _flushTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _flushDelayEvent = new AutoResetEvent(false);

            _periodicFlush = Task.Factory.StartNew(InternalFlushEventsAsync, this, TaskCreationOptions.LongRunning);
            _periodicFlush.ContinueWith(t => Log.Error(t.Exception, "Error in sending ciapp events"), TaskContinuationOptions.OnlyOnFaulted);
        }

        public void AddEvent(IEvent @event)
        {
            if (_eventQueue.IsAddingCompleted)
            {
                return;
            }

            try
            {
                _eventQueue.Add(@event);
            }
            catch (Exception)
            {
                // .
            }
        }

        public Task FlushAndCloseAsync()
        {
            _eventQueue.CompleteAdding();
            _flushDelayEvent.Set();
            return _flushTaskCompletionSource.Task;
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
                return Task.FromException(ex);
            }

            _flushDelayEvent.Set();
            return wme.CompletionSource.Task;
        }

        private static async Task InternalFlushEventsAsync(object state)
        {
            var writer = (CIWriter)state;
            var eventQueue = writer._eventQueue;
            var completionSource = writer._flushTaskCompletionSource;
            var flushDelayEvent = writer._flushDelayEvent;
            var lstTaskCompletionSource = new List<TaskCompletionSource<bool>>();

            Log.Debug("CIWriter:: InternalFlushEventsAsync/ Starting FlushEventsAsync loop");

            while (!eventQueue.IsCompleted)
            {
                try
                {
                    List<IEvent> eventList = null;
                    while ((eventList is null || eventList.Count <= MaxItemsPerBatch) && eventQueue.TryTake(out var item))
                    {
                        if (item is WatermarkEvent watermarkEvent)
                        {
                            lstTaskCompletionSource.Add(watermarkEvent.CompletionSource);
                        }
                        else
                        {
                            if (eventList is null)
                            {
                                eventList = new List<IEvent>();
                            }

                            eventList.Add(item);
                        }
                    }

                    if (eventList is not null)
                    {
                        await writer.SendEvents(eventList).ConfigureAwait(false);
                    }

                    foreach (var tcs in lstTaskCompletionSource)
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (ThreadAbortException ex)
                {
                    completionSource?.TrySetException(ex);
                    foreach (var tcs in lstTaskCompletionSource)
                    {
                        tcs.TrySetException(ex);
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    foreach (var tcs in lstTaskCompletionSource)
                    {
                        tcs.TrySetException(ex);
                    }
                }
                finally
                {
                    lstTaskCompletionSource.Clear();
                    flushDelayEvent.WaitOne(BatchInterval, true);
                }
            }

            completionSource?.TrySetResult(true);
            Log.Debug("CIWriter:: InternalFlushEventsAsync/ Finishing FlushEventsAsync loop");
        }

        public abstract Task<bool> Ping();

        protected abstract Task SendEvents(IEnumerable<IEvent> events);

        public void WriteTrace(ArraySegment<Span> trace)
        {
            int numberOfTests = 0;
            for (var i = trace.Offset; i < trace.Count; i++)
            {
                if (trace.Array[i].Type == SpanTypes.Test)
                {
                    AddEvent(new TestEvent(trace.Array[i]));
                    trace.Array[i] = null;
                    numberOfTests++;
                }
            }

            if (numberOfTests < trace.Count)
            {
                AddEvent(new TraceEvent(trace));
            }
        }

        internal class WatermarkEvent : IEvent
        {
            public WatermarkEvent()
            {
                CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public TaskCompletionSource<bool> CompletionSource { get; }
        }
    }
}
