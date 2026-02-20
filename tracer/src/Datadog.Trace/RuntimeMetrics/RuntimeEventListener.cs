// <copyright file="RuntimeEventListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Threading;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal sealed class RuntimeEventListener : EventListener, IRuntimeMetricsListener
    {
        private const string RuntimeEventSourceName = "Microsoft-Windows-DotNETRuntime";
        private const string AspNetCoreHostingEventSourceName = "Microsoft.AspNetCore.Hosting";
        private const string AspNetCoreKestrelEventSourceName = "Microsoft-AspNetCore-Server-Kestrel";
        private const string GcHeapStatsMetrics = $"{MetricsNames.Gen0HeapSize}, {MetricsNames.Gen1HeapSize}, {MetricsNames.Gen2HeapSize}, {MetricsNames.LohSize}, {{MetricsNames.PohSize}}, {MetricsNames.GcAllocatedBytes}, {MetricsNames.GcFragmentationPercent}, {MetricsNames.GcTotalAvailableMemory}, {MetricsNames.GcHighMemoryLoadThreshold}";
        private const string GcGlobalHeapMetrics = $"{MetricsNames.GcMemoryLoad}, runtime.dotnet.gc.count.gen#";
        private const string ThreadStatsMetrics = $"{MetricsNames.ContentionTime}, {MetricsNames.ContentionCount}, {MetricsNames.ThreadPoolWorkersCount}, {MetricsNames.ThreadsQueueLength}, {MetricsNames.ThreadsAvailableWorkers}, {MetricsNames.ThreadsAvailableCompletionPorts}, {MetricsNames.ThreadsCompletedWorkItems}";
        private const string JitMetrics = $"{MetricsNames.JitCompiledILBytes}, {MetricsNames.JitCompiledMethods}, {MetricsNames.JitCompilationTime}";

        private const int EventGcSuspendBegin = 9;
        private const int EventGcRestartEnd = 3;
        private const int EventGcHeapStats = 4;
        private const int EventContentionStop = 91;
        private const int EventGcGlobalHeapHistory = 205;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RuntimeEventListener>();

        private static readonly string[] GcCountMetricNames = { MetricsNames.Gen0CollectionsCount, MetricsNames.Gen1CollectionsCount, MetricsNames.Gen2CollectionsCount };
        private static readonly string[] CompactingGcTags = { "compacting_gc:true" };
        private static readonly string[] NotCompactingGcTags = { "compacting_gc:false" };

        private static readonly IReadOnlyDictionary<string, string> MetricsMapping;

        private readonly Timing _contentionTime = new Timing();

        private readonly string _delayInSeconds;

        private readonly IStatsdManager _statsd;

        private long _contentionCount;

        private DateTime? _gcStart;

        static RuntimeEventListener()
        {
            MetricsMapping = new Dictionary<string, string>
            {
                ["current-requests"] = MetricsNames.AspNetCoreCurrentRequests,
                ["failed-requests"] = MetricsNames.AspNetCoreFailedRequests,
                ["total-requests"] = MetricsNames.AspNetCoreTotalRequests,
                ["request-queue-length"] = MetricsNames.AspNetCoreRequestQueueLength,
                ["current-connections"] = MetricsNames.AspNetCoreCurrentConnections,
                ["connection-queue-length"] = MetricsNames.AspNetCoreConnectionQueueLength,
                ["total-connections"] = MetricsNames.AspNetCoreTotalConnections
            };
        }

        public RuntimeEventListener(IStatsdManager statsd, TimeSpan delay)
        {
            _statsd = statsd;
            _delayInSeconds = ((int)delay.TotalSeconds).ToString();

            EventSourceCreated += (_, e) => EnableEventSource(e.EventSource);
        }

        public void Refresh()
        {
            // if we can't send stats (e.g. we're shutting down), there's not much point in
            // running all this, but seeing as we update various state, play it safe and jadd ust do no-ops
            using var lease = _statsd.TryGetClientLease();
            var statsd = lease.Client ?? NoOpStatsd.Instance;

            // Can't use a Timing because Dogstatsd doesn't support local aggregation
            // It means that the aggregations in the UI would be wrong
            statsd?.Gauge(MetricsNames.ContentionTime, _contentionTime.Clear());
            statsd?.Counter(MetricsNames.ContentionCount, Interlocked.Exchange(ref _contentionCount, 0));

            statsd?.Gauge(MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);

            // ThreadPool Metrics
#if NETCOREAPP3_0_OR_GREATER
            statsd?.Gauge(MetricsNames.ThreadsQueueLength, ThreadPool.PendingWorkItemCount);
#endif

            ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
            statsd?.Gauge(MetricsNames.ThreadsAvailableWorkers, availableWorkers);
            statsd?.Gauge(MetricsNames.ThreadsAvailableCompletionPorts, availableIo);

#if NET5_0_OR_GREATER
            statsd?.Gauge(MetricsNames.ThreadsCompletedWorkItems, ThreadPool.CompletedWorkItemCount);
#endif

            if (statsd is not NoOpStatsd)
            {
                Log.Debug("Sent the following metrics to the DD agent: {Metrics}", ThreadStatsMetrics);
            }

            // JIT Metrics (.NET 6+)
#if NET6_0_OR_GREATER
            statsd?.Gauge(MetricsNames.JitCompiledILBytes, System.Runtime.JitInfo.GetCompiledILBytes());
            statsd?.Gauge(MetricsNames.JitCompiledMethods, System.Runtime.JitInfo.GetCompiledMethodCount());
            statsd?.Gauge(MetricsNames.JitCompilationTime, System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds);

            if (statsd is not NoOpStatsd)
            {
                Log.Debug("Sent the following JIT metrics to the DD agent: {Metrics}", JitMetrics);
            }
#endif
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (_statsd == null)
            {
                // I know it sounds crazy at first, but because OnEventSourceCreated is called from the base constructor,
                // and EnableEvents is called from OnEventSourceCreated, it's entirely possible that OnEventWritten
                // gets called before the child constructor is called.
                // In that case, just bail out.
                return;
            }

            try
            {
                using var lease = _statsd.TryGetClientLease();
                // We want to make sure we still refresh everything, so use a noop if not available
                var client = lease.Client;
                var statsd = client ?? NoOpStatsd.Instance;
                if (eventData.EventName == "EventCounters")
                {
                    ExtractCounters(statsd, eventData.Payload);
                }
                else if (eventData.EventId == EventGcSuspendBegin)
                {
                    _gcStart = eventData.TimeStamp;
                }
                else if (eventData.EventId == EventGcRestartEnd)
                {
                    var start = _gcStart;

                    if (start != null)
                    {
                        statsd.Timer(MetricsNames.GcPauseTime, (eventData.TimeStamp - start.Value).TotalMilliseconds);
                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", MetricsNames.GcPauseTime);
                    }
                }
                else
                {
                    if (eventData.EventId == EventGcHeapStats)
                    {
                        var stats = HeapStats.FromPayload(eventData.Payload);

                        statsd.Gauge(MetricsNames.Gen0HeapSize, stats.Gen0Size);
                        statsd.Gauge(MetricsNames.Gen1HeapSize, stats.Gen1Size);
                        statsd.Gauge(MetricsNames.Gen2HeapSize, stats.Gen2Size);
                        statsd.Gauge(MetricsNames.LohSize, stats.LohSize);

                        // GC Metrics
                        var gcInfo = GC.GetGCMemoryInfo();
#if NETCOREAPP3_0_OR_GREATER
                        statsd.Gauge(MetricsNames.GcAllocatedBytes, GC.GetTotalAllocatedBytes());
#endif

                        if (gcInfo.HeapSizeBytes > 0)
                        {
                            var fragmentationPercent = (double)gcInfo.FragmentedBytes * 100.0 / gcInfo.HeapSizeBytes;
                            statsd.Gauge(MetricsNames.GcFragmentationPercent, fragmentationPercent);
                        }

                        statsd.Gauge(MetricsNames.GcTotalAvailableMemory, gcInfo.TotalAvailableMemoryBytes);
                        statsd.Gauge(MetricsNames.GcHighMemoryLoadThreshold, gcInfo.HighMemoryLoadThresholdBytes);

#if NET5_0_OR_GREATER
                        if (gcInfo.GenerationInfo.Length > 4)
                        {
                            statsd.Gauge(MetricsNames.PohSize, gcInfo.GenerationInfo[4].SizeAfterBytes);
                        }
#endif

                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", GcHeapStatsMetrics);
                    }
                    else if (eventData.EventId == EventContentionStop)
                    {
                        var durationInNanoseconds = (double)eventData.Payload[2];

                        _contentionTime.Time(durationInNanoseconds / 1_000_000);
                        Interlocked.Increment(ref _contentionCount);
                    }
                    else if (eventData.EventId == EventGcGlobalHeapHistory)
                    {
                        var heapHistory = HeapHistory.FromPayload(eventData.Payload);

                        if (heapHistory.MemoryLoad != null)
                        {
                            statsd.Gauge(MetricsNames.GcMemoryLoad, heapHistory.MemoryLoad.Value);
                        }

                        statsd.Increment(GcCountMetricNames[heapHistory.Generation], 1, tags: heapHistory.Compacting ? CompactingGcTags : NotCompactingGcTags);
                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", GcGlobalHeapMetrics);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning<int, string>(ex, "Error while processing event {EventId} {EventName}", eventData.EventId, eventData.EventName);
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
                    ["EventCounterIntervalSec"] = _delayInSeconds
                };

                EnableEvents(eventSource, EventLevel.Critical, EventKeywords.All, settings);
            }
        }

        private void ExtractCounters(IDogStatsd statsd, ReadOnlyCollection<object> payload)
        {
            for (int i = 0; i < payload.Count; ++i)
            {
                if (!(payload[i] is IDictionary<string, object> eventPayload))
                {
                    continue;
                }

                if (!eventPayload.TryGetValue("Name", out object name)
                    || !MetricsMapping.TryGetValue(name.ToString(), out var statName))
                {
                    continue;
                }

                if (eventPayload.TryGetValue("Mean", out object rawValue)
                    || eventPayload.TryGetValue("Increment", out rawValue))
                {
                    var value = (double)rawValue;

                    statsd.Gauge(statName, value);
                    Log.Debug("Sent the following metrics to the DD agent: {Metrics}", statName);
                }
                else
                {
                    Log.Debug<object>("EventCounter {CounterName} has no Mean or Increment field", name);
                }
            }
        }
    }
}
#endif
