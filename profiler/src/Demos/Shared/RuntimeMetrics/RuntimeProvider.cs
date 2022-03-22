// <copyright file="RuntimeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Datadog.RuntimeMetrics
{
#if !NETFRAMEWORK
    // Get metrics from .NET Core runtime
    // Subset from Tracer
    internal class RuntimeProvider : EventListener, IMetricsProvider
    {
        private const string RuntimeEventSourceName = "Microsoft-Windows-DotNETRuntime";
        private const string AspNetCoreHostingEventSourceName = "Microsoft.AspNetCore.Hosting";
        private const string AspNetCoreKestrelEventSourceName = "Microsoft-AspNetCore-Server-Kestrel";

        private const int EventGcSuspendBegin = 9;
        private const int EventGcRestartEnd = 3;
        private const int EventGcHeapStats = 4;
        private const int EventContentionStop = 91;
        private const int EventGcGlobalHeapHistory = 205;

        // list of metrics
        private readonly Timing _contentionTime = new Timing();                     // MetricsNames.ContentionTime
        private readonly Counter _contentionCount = new Counter();                  // MetricsNames.ContentionCount
        private readonly Counter _gen0CollectionCount = new Counter();              // MetricsNames.Gen0CollectionsCount
        private readonly Counter _gen1CollectionCount = new Counter();              // MetricsNames.Gen1CollectionsCount
        private readonly Counter _gen2CollectionCount = new Counter();              // MetricsNames.Gen2CollectionsCount
        private readonly Counter _gen2CompactingCollectionCount = new Counter();    // MetricsNames.Gen2CompactingCollectionsCount
        private readonly Timing _gcPauseTime = new Timing();                        // MetricsNames.GcPauseTime
        private readonly Value _gen0Size = new Value();                             // MetricsNames.Gen0HeapSize
        private readonly Value _gen1Size = new Value();                             // MetricsNames.Gen1HeapSize
        private readonly Value _gen2Size = new Value();                             // MetricsNames.Gen2HeapSize
        private readonly Value _lohSize = new Value();                              // MetricsNames.LohSize
        private readonly Value _gcMemoryLoad = new Value();                         // MetricsNames.GcMemoryLoad
        private long _aspNetRequestsCount = -1;                                     // MetricsNames.AspNetCoreTotalRequests

        private bool _canAcceptEvents;
        private DateTime? _gcStart;

        public RuntimeProvider()
        {
            _canAcceptEvents = true;
            EventSourceCreated += (_, e) => EnableEventSource(e.EventSource);
        }

        public void Stop()
        {
            _canAcceptEvents = false;
        }

        public IReadOnlyList<(string Name, string Value)> GetMetrics()
        {
            // return the value of all collected metrics
            var metrics = new List<(string Name, string Value)>()
            {
                (MetricsNames.ContentionTime, _contentionTime.GetTime().ToString()),
                (MetricsNames.ContentionCount, _contentionCount.GetValue().ToString()),
                (MetricsNames.Gen0CollectionsCount, _gen0CollectionCount.GetValue().ToString()),
                (MetricsNames.Gen1CollectionsCount, _gen1CollectionCount.GetValue().ToString()),
                (MetricsNames.Gen2CollectionsCount, _gen2CollectionCount.GetValue().ToString()),
                (MetricsNames.Gen2CompactingCollectionsCount, _gen2CompactingCollectionCount.GetValue().ToString()),
                (MetricsNames.GcPauseTime, _gcPauseTime.GetTime().ToString()),
                (MetricsNames.Gen0HeapSize, _gen0Size.GetMax().ToString()),
                (MetricsNames.Gen1HeapSize, _gen1Size.GetMax().ToString()),
                (MetricsNames.Gen2HeapSize, _gen2Size.GetMax().ToString()),
                (MetricsNames.LohSize, _lohSize.GetMax().ToString()),
                (MetricsNames.GcMemoryLoad, _gcMemoryLoad.GetMax().ToString()),
                (MetricsNames.ProcessorTime, Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds.ToString()),
                (MetricsNames.PrivateBytes, Process.GetCurrentProcess().PrivateMemorySize64.ToString()),
            };

            // add ASP.NET Core counter only if available
            if (_aspNetRequestsCount > -1)
            {
                metrics.Add((MetricsNames.AspNetCoreTotalRequests, _aspNetRequestsCount.ToString()));
            }

            return metrics;
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!_canAcceptEvents)
            {
                // I know it sounds crazy at first, but because OnEventSourceCreated is called from the base constructor,
                // and EnableEvents is called from OnEventSourceCreated, it's entirely possible that OnEventWritten
                // gets called before the child constructor is called.
                // In that case, just bail out.
                return;
            }

            try
            {
                // Listen to "EventCounters" for ASP.NET Core metrics
                if (eventData.EventName == "EventCounters")
                {
                    ExtractCounters(eventData.Payload);
                }
                else
                if (eventData.EventId == EventGcSuspendBegin)
                {
                    _gcStart = eventData.TimeStamp;
                }
                else if (eventData.EventId == EventGcRestartEnd)
                {
                    var start = _gcStart;

                    if (start != null)
                    {
                        _gcPauseTime.Time((eventData.TimeStamp - start.Value).TotalMilliseconds);
                    }
                }
                else
                {
                    if (eventData.EventId == EventGcHeapStats)
                    {
                        var stats = HeapStats.FromPayload(eventData.Payload);

                        _gen0Size.Add((long)stats.Gen0Size);
                        _gen1Size.Add((long)stats.Gen1Size);
                        _gen2Size.Add((long)stats.Gen2Size);
                        _lohSize.Add((long)stats.LohSize);
                    }
                    else if (eventData.EventId == EventContentionStop)
                    {
                        var durationInNanoseconds = (double)eventData.Payload[2];

                        _contentionTime.Time(durationInNanoseconds / 1_000_000);
                        _contentionCount.Inc();
                    }
                    else if (eventData.EventId == EventGcGlobalHeapHistory)
                    {
                        var heapHistory = HeapHistory.FromPayload(eventData.Payload);

                        if (heapHistory.MemoryLoad != null)
                        {
                            _gcMemoryLoad.Add(heapHistory.MemoryLoad.Value);
                        }

                        if (heapHistory.Generation == 0)
                        {
                            _gen0CollectionCount.Inc();
                        }
                        else
                        if (heapHistory.Generation == 1)
                        {
                            _gen1CollectionCount.Inc();
                        }
                        else
                        if (heapHistory.Generation == 2)
                        {
                            _gen2CollectionCount.Inc();
                            if (heapHistory.Compacting)
                            {
                                _gen2CompactingCollectionCount.Inc();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void ExtractCounters(ReadOnlyCollection<object> payload)
        {
            for (int i = 0; i < payload.Count; ++i)
            {
                if (!(payload[i] is IDictionary<string, object> eventPayload))
                {
                    continue;
                }

                if (!eventPayload.TryGetValue("Name", out object name))
                {
                    continue;
                }

                if (string.CompareOrdinal("total-requests", name.ToString()) == 0)
                {
                    // Only Mean or Increment counters are supported
                    if (eventPayload.TryGetValue("Mean", out object rawValue)
                        || eventPayload.TryGetValue("Increment", out rawValue))
                    {
                        var value = (double)rawValue;
                        _aspNetRequestsCount = ((long)value);
                    }
                }
            }
        }

        private void EnableEventSource(EventSource eventSource)
        {
            if (eventSource.Name == RuntimeEventSourceName)
            {
                var keywords = Keywords.GC | Keywords.Contention;

                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)keywords);
            }
            else if (eventSource.Name == AspNetCoreHostingEventSourceName || eventSource.Name == AspNetCoreKestrelEventSourceName)
            {
                var settings = new Dictionary<string, string>
                {
                    ["EventCounterIntervalSec"] = "1"
                };

                EnableEvents(eventSource, EventLevel.Critical, EventKeywords.All, settings);
            }
        }
    }
#endif
}
