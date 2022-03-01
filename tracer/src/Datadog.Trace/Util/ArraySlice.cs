// <copyright file="ArraySlice.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Array slice.
    /// Similar to ArraySegment but with write support over the index.
    /// Also as Span`1 it's a readonly ref struct.
    /// </summary>
    /// <typeparam name="T">Type of the array</typeparam>
    internal readonly ref struct ArraySlice<T>
    {
        public readonly T[] Array;
        public readonly int Offset;
        public readonly int Count;

        public ArraySlice(T[] array, int offset, int count)
        {
            Array = array;
            Offset = offset;
            Count = count;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Array[Offset + index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Array[Offset + index] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySlice<T> Slice(int start)
        {
            if (Count - start < 0)
            {
                ThrowHelper.ThrowArgumentException("Start value is greater than the total number of elements", nameof(start));
            }

            return new ArraySlice<T>(Array, Offset + start, Count - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySlice<T> Slice(int start, int count)
        {
            if (count > Count - start)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
            }

            return new ArraySlice<T>(Array, Offset + start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(ArraySlice<T> other)
        {
            System.Array.Copy(Array, Offset, other.Array, other.Offset, Count);
        }
    }
}
