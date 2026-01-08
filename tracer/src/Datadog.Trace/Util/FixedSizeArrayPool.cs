// <copyright file="FixedSizeArrayPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.Util;

internal class FixedSizeArrayPool<T>
{
    private const int MaxStackRetained = 63;
    private readonly ConcurrentStack<T[]> _items;
    private readonly int _arrayItems;
    private T[]? _fastPath;
    private int _stackCount;

    public FixedSizeArrayPool(int arrayItems)
    {
        if (arrayItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayItems), arrayItems, "Array size must be greater than 0.");
        }

        _arrayItems = arrayItems;
        _items = new();
    }

    public static FixedSizeArrayPool<T> OneItemPool => field ??= new(1);

    public static FixedSizeArrayPool<T> TwoItemPool => field ??= new(2);

    public static FixedSizeArrayPool<T> ThreeItemPool => field ??= new(3);

    public static FixedSizeArrayPool<T> FourItemPool => field ??= new(4);

    public static FixedSizeArrayPool<T> FiveItemPool => field ??= new(5);

    public ArrayPoolItem Get() => new(GetArray(), this);

    public T[] GetArray()
    {
        if (Interlocked.Exchange(ref _fastPath, null) is { } cached)
        {
            return cached;
        }

        if (_items.TryPop(out var value))
        {
            Interlocked.Decrement(ref _stackCount);
            return value;
        }

        value = new T[_arrayItems];
        return value;
    }

    public void ReturnArray(T[] value)
    {
#if DEBUG
        if (value is null)
        {
            Debug.Fail("Attempted to return a null array to FixedSizeArrayPool.");
            return;
        }

        if (value.Length != _arrayItems)
        {
            Debug.Fail($"Attempted to return an array of length {value.Length} to a pool of size {_arrayItems}.");
            return;
        }
#else
        if (value is null || value.Length != _arrayItems)
        {
            return;
        }
#endif

#if NETCOREAPP3_0_OR_GREATER
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(value, 0, value.Length);
        }
#else
        Array.Clear(value, 0, value.Length);
#endif

        if (Interlocked.CompareExchange(ref _fastPath, value, null) is not null)
        {
            // Bound the number of retained arrays in the stack to avoid unbounded growth.
            var newCount = Interlocked.Increment(ref _stackCount);
            if (newCount <= MaxStackRetained)
            {
                _items.Push(value);
            }
            else
            {
                Interlocked.Decrement(ref _stackCount);
            }
        }
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

        public T[] Array
        {
            get
            {
#if DEBUG
                if (_array is null)
                {
                    throw new ObjectDisposedException(nameof(ArrayPoolItem));
                }
#endif
                return _array ?? [];
            }
        }

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
