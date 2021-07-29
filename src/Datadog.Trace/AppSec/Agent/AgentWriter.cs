// <copyright file="AgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Abstractions;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Agent
{
    internal class AgentWriter : IAgentWriter
    {
        private const int BatchInterval = 1000;
        private const int MaxItemsPerBatch = 1000;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentWriter>();
        private readonly ManualResetEventSlim _senderMutex = new ManualResetEventSlim(initialState: false, spinCount: 0);
        private readonly ConcurrentQueue<IEvent> events;
        private readonly Task _periodicFlush;
        private readonly Sender _sender;

        internal AgentWriter()
        {
            events = new ConcurrentQueue<IEvent>();
            _periodicFlush = Task.Factory.StartNew(FlushTracesAsync, TaskCreationOptions.LongRunning);
            _periodicFlush.ContinueWith(t => Log.Error(t.Exception, "Error in sending appsec events"), TaskContinuationOptions.OnlyOnFaulted);
            _sender = new Sender();
        }

        public void AddEvent(IEvent @event)
        {
            events.Enqueue(@event);
            if (!_senderMutex.IsSet)
            {
                _senderMutex.Set();
            }
        }

        private async Task FlushTracesAsync()
        {
            while (true)
            {
                if (events.Count == 0)
                {
                    _senderMutex.Wait();
                    _senderMutex.Reset();
                }

                try
                {
                    var appsecEvents = new List<IEvent>();
                    while (events.TryDequeue(out var result) && appsecEvents.Count <= MaxItemsPerBatch)
                    {
                        appsecEvents.Add(result);
                    }

                    await _sender.Send(appsecEvents);
                }

                // see ThreadAbortAnalyzer and related .net framework bug
                catch (ThreadAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // todo don't drop traces
                    Log.Error(ex, "An error occured in sending appsec events");
                }
                finally
                {
                    await Task.Delay(BatchInterval);
                }
            }
        }
    }
}
