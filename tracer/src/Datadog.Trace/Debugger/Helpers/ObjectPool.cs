// <copyright file="ObjectPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.Debugger.Helpers
{
    internal class ObjectPool<T, TSetParameters>
        where T : class, IPoolable<TSetParameters>, new()
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectFactory;
        private readonly int _maxSize;

        public ObjectPool(Func<T>? objectFactory = null, int maxSize = 100)
        {
            _objectFactory = objectFactory ?? (() => new T());
            _maxSize = maxSize;
            _objects = new ConcurrentBag<T>();
        }

        public T Get() => _objects.TryTake(out T item) ? item : _objectFactory();

        public T? Get(TSetParameters parameters)
        {
            var item = _objects.TryTake(out var obj) ? obj : _objectFactory();
            item?.Set(parameters);
            return item;
        }

        public void Return(T? item)
        {
            item?.Reset();
            if (item != null && _objects.Count < _maxSize)
            {
                _objects.Add(item);
            }
        }

        public int Count => _objects.Count;
    }
}
