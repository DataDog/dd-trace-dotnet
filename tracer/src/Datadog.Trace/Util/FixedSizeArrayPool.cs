// <copyright file="FixedSizeArrayPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.Util;

internal class FixedSizeArrayPool<T>
{
    private readonly ConcurrentStack<T[]> _items;
    private readonly int _arrayItems;

    public FixedSizeArrayPool(int arrayItems)
    {
        _arrayItems = arrayItems;
        _items = new();
    }

    public ArrayPoolItem Get()
    {
        var array = GetArray();
        return new(array, this);
    }

    public T[] GetArray()
    {
        if (_items.TryPop(out var value))
        {
            return value;
        }

        value = new T[_arrayItems];
        return value;
    }

    public void ReturnArray(T[] value)
    {
        if (value is null || value.Length != _arrayItems)
        {
            return;
        }

        Array.Clear(value, 0, value.Length);
        _items.Push(value);
    }

    internal ref struct ArrayPoolItem
    {
        private readonly FixedSizeArrayPool<T> _owner;
        private T[]? _array;

        internal ArrayPoolItem(T[] array, FixedSizeArrayPool<T> owner)
        {
            _array = array;
            _owner = owner;
        }

        public T[] Array => _array ?? [];

        public void Dispose()
        {
            if (_array is null)
            {
                return;
            }

            _owner.ReturnArray(_array);
            _array = null;
        }
    }
}
