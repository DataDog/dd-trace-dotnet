using System;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    internal class AgentWriterBuffer<T>
    {
        private readonly Object _lock = new Object();
        private readonly int _maxSize;
        private List<T> _items;

        public AgentWriterBuffer(int maxSize)
        {
            _maxSize = maxSize;
            _items = new List<T>();
        }

        public bool Push(T item)
        {
            lock (_lock)
            {
                if(_items.Count < _maxSize)
                {
                    _items.Add(item);
                    return true;
                }
                return false;
            }
        }

        public List<T> Pop()
        {
            lock (_lock)
            {
                var ret = _items;
                _items = new List<T>();
                return ret;
            }
        }
    }
}
