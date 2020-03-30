using System;

namespace Datadog.Trace.Agent
{
    internal class AgentWriterBuffer<T>
    {
        private readonly Random _random = new Random();
        private readonly T[] _items;

        private int _count;

        public AgentWriterBuffer(int maxSize)
        {
            _items = new T[maxSize];
        }

        public bool Push(T item)
        {
            lock (_items)
            {
                if (_count < _items.Length)
                {
                    _items[_count++] = item;
                    return true;
                }
                else
                {
                    // drop a random trace.
                    // note that Random.Next() is NOT thread-safe,
                    // but we are running this inside a lock
                    _items[_random.Next(_items.Length)] = item;
                    return false;
                }
            }
        }

        public T[] Pop()
        {
            lock (_items)
            {
                // copy items from buffer into new array
                var result = new T[_count];
                Array.Copy(_items, result, _count);

                // clear buffer
                Array.Clear(_items, 0, _items.Length);
                _count = 0;

                return result;
            }
        }
    }
}
