#if NETCOREAPP
using System;
using System.Diagnostics.Tracing;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeEventListener : EventListener
    {
        private const string EventSourceName = "Microsoft-Windows-DotNETRuntime";

        private const int EventGcSuspendBegin = 9;
        private const int EventGcRestartEnd = 3;
        private const int EventGcHeapStats = 4;
        private const int EventContentionStop = 91;
        private const int EventGcGlobalHeapHistory = 205;

        private DateTime? _gcStart;

        internal event Action<HeapStats> GcHeapStats;

        internal event Action<HeapHistory> GcHeapHistory;

        internal event Action<TimeSpan> GcPauseTime;

        internal event Action<double> Contention;

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
            if (eventData.EventId == EventGcSuspendBegin)
            {
                _gcStart = eventData.TimeStamp;
            }
            else if (eventData.EventId == EventGcRestartEnd)
            {
                var start = _gcStart;

                if (start != null)
                {
                    GcPauseTime?.Invoke(eventData.TimeStamp - start.Value);
                }
            }
            else if (eventData.EventId == EventGcHeapStats)
            {
                GcHeapStats?.Invoke(HeapStats.FromPayload(eventData.Payload));
            }
            else if (eventData.EventId == EventContentionStop)
            {
                Contention?.Invoke((double)eventData.Payload[2]);
            }
            else if (eventData.EventId == EventGcGlobalHeapHistory)
            {
                GcHeapHistory?.Invoke(HeapHistory.FromPayload(eventData.Payload));
            }
        }
    }
}
#endif
