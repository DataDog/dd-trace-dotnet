// <copyright file="ArrayBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal struct ArrayBuilder<T>
    {
        private const int DefaultInitialCapacity = 4;

        private T[] _array;
        private int _count;

        public ArrayBuilder(int initialCapacity)
        {
            _array = new T[initialCapacity];
            _count = 0;
        }

        public int Count => _count;

        public void Add(T item)
        {
            GrowIfNeeded();
            _array[_count] = item;
            _count++;
        }

        public ArraySegment<T> GetArray() => new(_array ?? ArrayHelper.Empty<T>(), 0, _count);

        private void GrowIfNeeded()
        {
            if (_array == null)
            {
                _array = new T[DefaultInitialCapacity];
                return;
            }

            if (_count < _array.Length)
            {
                // The array is already big enough
                return;
            }

            var newArray = new T[_array.Length * 2];

            Array.Copy(_array, 0, newArray, 0, _array.Length);

            _array = newArray;
        }
    }
}
