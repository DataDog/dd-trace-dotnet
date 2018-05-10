using System;
using System.Collections.Generic;

namespace Datadog.Trace.Agent
{
    internal class AgentWriterBuffer<T>
    {
        private readonly object _lock = new object();
        private readonly int _maxSize;
        private List<T> _items;
        private Random _random;

        public AgentWriterBuffer(int maxSize)
        {
            _maxSize = maxSize;
            _items = new List<T>();
            _random = new Random();
        }

        public bool Push(T item)
        {
            lock (_lock)
            {
                if (_items.Count < _maxSize)
                {
                    _items.Add(item);
                    return true;
                }
                else
                {
                    _items[_random.Next(_items.Count)] = item;
                    return false;
                }
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
