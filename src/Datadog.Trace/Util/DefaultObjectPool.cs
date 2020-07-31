using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Datadog.Trace.Util
{
    internal sealed class DefaultObjectPool<TObject> : ObjectPool<TObject, DefaultAllocator<TObject>>
        where TObject : class, new()
    {
        public static readonly DefaultObjectPool<TObject> Shared = new DefaultObjectPool<TObject>();

        public DefaultObjectPool(int minItems = 1, int maxItems = 250, int? dropFrequencyInMinutes = null)
            : base(minItems, maxItems, dropFrequencyInMinutes)
        {
        }
    }
}
