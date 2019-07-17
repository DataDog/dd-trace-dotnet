using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Datadog.Trace
{
    internal class SpanFlushManager
    {
        private readonly ConcurrentDictionary<Guid, DatadogSpanContext> _stagedContexts = new ConcurrentDictionary<Guid, DatadogSpanContext>();
        private readonly ConcurrentDictionary<Guid, UnfinishedContextFlushTask> _unfinishedContexts = new ConcurrentDictionary<Guid, UnfinishedContextFlushTask>();
        private readonly ConcurrentQueue<DatadogSpanContext> _readyToFlush = new ConcurrentQueue<DatadogSpanContext>();
        private readonly Action<ICollection<DatadogSpanContext>> _flush;
        private readonly int _maximumSpansToFlush = 250;

        public SpanFlushManager(Action<ICollection<DatadogSpanContext>> flushAction)
        {
            _flush = flushAction;
        }

        public void FlushAll()
        {
            var bowl = new Collection<DatadogSpanContext>();

            while (_readyToFlush.TryDequeue(out var spanContext))
            {
                bowl.Add(spanContext);

                if (bowl.Count >= _maximumSpansToFlush)
                {
                    _flush(bowl);
                    bowl = new Collection<DatadogSpanContext>();
                }
            }
        }

        public void CheckForStuckSpans()
        {
            var keys = _unfinishedContexts.Keys;
            var now = DateTime.UtcNow;

            foreach (var uniqueId in keys)
            {
                if (!_unfinishedContexts.TryGetValue(uniqueId, out var unfinished))
                {
                    continue;
                }

                if (unfinished.FlushAt < now)
                {
                    _readyToFlush.Enqueue(unfinished.UnfinishedContext);
                    _unfinishedContexts.TryRemove(uniqueId, out _);
                }
            }
        }

        public bool TryStageContext(DatadogSpanContext context)
        {
            return _stagedContexts.TryAdd(context.UniqueId, context);
        }

        public bool TryBeginFlush(DatadogSpanContext context)
        {
            var contextFound = false;
            context.IsFinished = true;

            var traceId = context.TraceId;
            var isTraceSpan = traceId == context.SpanId;

            if (_stagedContexts.TryGetValue(context.UniqueId, out var contextCandidate))
            {
                // Another thread must have got it
                _readyToFlush.Enqueue(contextCandidate);
                _unfinishedContexts.TryRemove(context.UniqueId, out _);
                contextFound = true;
            }

            if (isTraceSpan || _stagedContexts.Count > _maximumSpansToFlush)
            {
                CleanHouse(context);
            }

            if (!contextFound)
            {
                // The trace may have closed before this
                if (_unfinishedContexts.TryGetValue(context.UniqueId, out var unfinished))
                {
                    _readyToFlush.Enqueue(unfinished.UnfinishedContext);
                    _unfinishedContexts.TryRemove(context.UniqueId, out _);
                }
            }

            return contextFound;
        }

        public void CleanHouse(DatadogSpanContext context)
        {
            var traceId = context.TraceId;
            var isTraceSpan = traceId == context.SpanId;

            var keys = _stagedContexts.Keys;
            var now = DateTime.UtcNow;

            foreach (var uniqueId in keys)
            {
                if (!_stagedContexts.TryGetValue(uniqueId, out var contextCandidate))
                {
                    // Another thread must have got it
                    continue;
                }

                // Clean out every time we run through
                if (contextCandidate.IsFinished)
                {
                    _readyToFlush.Enqueue(contextCandidate);
                    _stagedContexts.TryRemove(uniqueId, out _);
                    continue;
                }

                if (contextCandidate.TraceId != traceId)
                {
                    // We don't care to evaluate further
                    continue;
                }

                if (isTraceSpan)
                {
                    // Put everything that hasn't finished on a timer
                    var unfinishedSpanTask = new UnfinishedContextFlushTask
                    {
                        UnfinishedContext = contextCandidate,
                        FlushAt = now.Add(contextCandidate.TimeToLiveAfterTraceClose)
                    };

                    _unfinishedContexts.TryAdd(uniqueId, unfinishedSpanTask);

                    continue;
                }

                if (contextCandidate.TimeToForceFlush < now)
                {
                    _readyToFlush.Enqueue(contextCandidate);
                    _stagedContexts.TryRemove(uniqueId, out _);
                }
            }
        }
    }
}
