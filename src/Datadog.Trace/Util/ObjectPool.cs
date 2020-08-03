using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Generic object pool
    /// </summary>
    /// <typeparam name="TObject">Type of the pool item object</typeparam>
    /// <typeparam name="TStruct">Type of the allocator</typeparam>
    internal class ObjectPool<TObject, TStruct>
        where TObject : class
        where TStruct : struct, IPoolAllocator<TObject>
    {
        private readonly TStruct _allocator = default;
        private readonly ConcurrentStack<TObject> _stack = new ConcurrentStack<TObject>();
        private readonly Timer _dropTimer;
        private TObject _fastItem;
        private int _maxItems;
        private int _minItems;
        private int _count;

        public ObjectPool(int minItems = 5, int maxItems = 250, int? dropFrequencyInMinutes = 5)
        {
            _minItems = minItems;
            _maxItems = maxItems;

            for (int i = 0; i < minItems; i++)
            {
                _stack.Push(_allocator.New());
            }

            if (dropFrequencyInMinutes.HasValue && dropFrequencyInMinutes > 0)
            {
                var frequency = TimeSpan.FromMinutes(dropFrequencyInMinutes.Value);
                _dropTimer = new Timer(DropTimerMethod, this, frequency, frequency);
            }
        }

        public TObject Get()
        {
            var value = _fastItem;
            if (value != null && value == Interlocked.CompareExchange(ref _fastItem, null, value))
            {
                return value;
            }

            if (_stack.TryPop(out value))
            {
                Interlocked.Decrement(ref _count);
                return value;
            }

            return _allocator.New();
        }

        public void Return(TObject value)
        {
            if (value is null)
            {
                return;
            }

            _allocator.Clear(value);

            var currentItem = _fastItem;
            if (currentItem == null && Interlocked.CompareExchange(ref _fastItem, value, null) == null)
            {
                return;
            }

            if (_count < _maxItems)
            {
                _stack.Push(value);
                Interlocked.Increment(ref _count);
            }
        }

        private static void DropTimerMethod(object state)
        {
            var oPool = (ObjectPool<TObject, TStruct>)state;
            if (oPool is null)
            {
                return;
            }

            if (oPool._minItems == 0)
            {
                oPool._stack.Clear();
                return;
            }

            var count = oPool._count;
            while (count > oPool._minItems && oPool._stack.TryPop(out _))
            {
                count = Interlocked.Decrement(ref oPool._count);
            }
        }
    }
}
