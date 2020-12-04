#if NETCOREAPP
using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeEventListener : EventListener, IRuntimeMetricsListener
    {
        private const string EventSourceName = "Microsoft-Windows-DotNETRuntime";

        private const int EventGcSuspendBegin = 9;
        private const int EventGcRestartEnd = 3;
        private const int EventGcHeapStats = 4;
        private const int EventContentionStop = 91;
        private const int EventGcGlobalHeapHistory = 205;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RuntimeEventListener>();

        private static readonly string[] GcCountMetricNames = { MetricsNames.Gen0CollectionsCount, MetricsNames.Gen1CollectionsCount, MetricsNames.Gen2CollectionsCount };
        private static readonly string[] CompactingGcTags = { "compacting_gc:true" };
        private static readonly string[] NotCompactingGcTags = { "compacting_gc:false" };

        private readonly IDogStatsd _statsd;

        private readonly Timing _contentionTime = new Timing();
        private long _contentionCount;

        private DateTime? _gcStart;

        public RuntimeEventListener(IDogStatsd statsd)
        {
            _statsd = statsd;
        }

        public void Refresh()
        {
            // Can't use a Timing because Dogstatsd doesn't support local aggregation
            // It means that the aggregations in the UI would be wrong
            _statsd.Gauge(MetricsNames.ContentionTime, _contentionTime.Clear());
            _statsd.Counter(MetricsNames.ContentionCount, Interlocked.Exchange(ref _contentionCount, 0));

            _statsd.Gauge(MetricsNames.ThreadPoolWorkersCount, ThreadPool.ThreadCount);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                var keywords = Keywords.GC | Keywords.Contention;

                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)keywords);
            }
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
                if (eventData.EventId == EventGcSuspendBegin)
                {
                    _gcStart = eventData.TimeStamp;
                }
                else if (eventData.EventId == EventGcRestartEnd)
                {
                    var start = _gcStart;

                    if (start != null)
                    {
                        _statsd.Timer(MetricsNames.GcPauseTime, (eventData.TimeStamp - start.Value).TotalMilliseconds);
                    }
                }
                else
                {
                    if (eventData.EventId == EventGcHeapStats)
                    {
                        var stats = HeapStats.FromPayload(eventData.Payload);

                        _statsd.Gauge(MetricsNames.Gen0HeapSize, stats.Gen0Size);
                        _statsd.Gauge(MetricsNames.Gen1HeapSize, stats.Gen1Size);
                        _statsd.Gauge(MetricsNames.Gen2HeapSize, stats.Gen2Size);
                        _statsd.Gauge(MetricsNames.LohSize, stats.LohSize);
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
                            _statsd.Gauge(MetricsNames.GcMemoryLoad, heapHistory.MemoryLoad.Value);
                        }

                        _statsd.Increment(GcCountMetricNames[heapHistory.Generation], 1, tags: heapHistory.Compacting ? CompactingGcTags : NotCompactingGcTags);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while processing event {0} {1}", eventData.EventId, eventData.EventName);
            }
        }
    }
}
#endif
