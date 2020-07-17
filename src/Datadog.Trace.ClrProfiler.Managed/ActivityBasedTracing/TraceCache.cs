using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler
{
    internal class TraceCache
    {
        // We wrap this abstraction around the disctionary, because using reasonable assumptions about the number of concurrent
        // requests (aka Traces) that may be in flight, we can optimize the lookups here. However, let's leave it for later..

        private readonly ConcurrentDictionary<ulong, TraceActivitiesContainer> _traces = new ConcurrentDictionary<ulong, TraceActivitiesContainer>();

        public bool TryCreate(ulong traceKey, TraceActivitiesContainer trace)
        {
            return _traces.TryAdd(traceKey, trace);
        }

        public bool TryGet(ulong traceKey, out TraceActivitiesContainer trace)
        {
            return _traces.TryGetValue(traceKey, out trace);
        }

        public bool TryRemove(ulong traceKey, out TraceActivitiesContainer trace)
        {
            return _traces.TryRemove(traceKey, out trace);
        }
    }
}
