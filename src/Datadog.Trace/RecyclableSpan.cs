using System;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Recyclable span implementation backed with a object pool
    /// </summary>
    internal class RecyclableSpan : Span
    {
        private static readonly ObjectPool<RecyclableSpan, RecyclableSpanAllocator> _objectPool
            = new ObjectPool<RecyclableSpan, RecyclableSpanAllocator>( // are these good defaults?
                minItems: 5, // Minimum items in the pool (preallocation)
                maxItems: 250, // Maximum items in the pool
                dropFrequencyInMinutes: 5); // Time in minutes before doing a drop out items to the minimum

        private bool _isDirty = false;

        private RecyclableSpan()
            : base(null, null)
        {
        }

        internal static RecyclableSpan Get(SpanContext context, DateTimeOffset? start)
        {
            RecyclableSpan value = _objectPool.Get();
            value.Init(context, start);
            return value;
        }

        internal static void Return(RecyclableSpan value)
        {
            if (value._isDirty)
            {
                return;
            }

            value.Clear();
            _objectPool.Return(value);
        }

        internal void MarkAsDirty()
        {
            _isDirty = true;
        }

        private struct RecyclableSpanAllocator : IPoolAllocator<RecyclableSpan>
        {
            public void Clear(RecyclableSpan value)
            {
            }

            public RecyclableSpan New()
            {
                return new RecyclableSpan();
            }
        }
    }
}
