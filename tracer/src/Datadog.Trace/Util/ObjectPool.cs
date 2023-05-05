// <copyright file="ObjectPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Datadog.Trace.Util;

internal class ObjectPool<T>
    where T : class
{
    private readonly Func<T> _createFunc;
    private readonly int _maxCapacity;
    private readonly ConcurrentQueue<T> _items = new();
    private int _numItems;
    private T? _fastItem;

    public ObjectPool(Func<T>? createFunc = null, int? maximumRetained = null)
    {
        _createFunc = createFunc ?? (() => Activator.CreateInstance<T>());
        _maxCapacity = (maximumRetained ?? Environment.ProcessorCount * 2) - 1;  // -1 to account for _fastItem
    }

    public static ObjectPool<T> Shared { get; } = new();

    public T Get()
    {
        var item = _fastItem;
        if (item == null || Interlocked.CompareExchange(ref _fastItem, null, item) != item)
        {
            if (_items.TryDequeue(out item))
            {
                Interlocked.Decrement(ref _numItems);
                return item;
            }

            // no object available, so go get a brand new one
            return _createFunc();
        }

        return item;
    }

    public bool Return(T obj)
    {
        if (_fastItem != null || Interlocked.CompareExchange(ref _fastItem, obj, null) != null)
        {
            if (Interlocked.Increment(ref _numItems) <= _maxCapacity)
            {
                _items.Enqueue(obj);
                return true;
            }

            // no room, clean up the count and drop the object on the floor
            Interlocked.Decrement(ref _numItems);
            return false;
        }

        return true;
    }
}
