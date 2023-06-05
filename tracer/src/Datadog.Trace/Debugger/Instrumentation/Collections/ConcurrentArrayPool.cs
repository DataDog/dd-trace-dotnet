// <copyright file="ConcurrentArrayPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// <summary>
    /// ConcurrentArrayPool is a high-performance, custom array pool implementation suitable for concurrent use in .NET Framework and .NET Core applications.
    /// This pool minimizes contention, reduces memory allocations, and provides fast access to arrays of varying lengths. It employs dynamic length partitioning,
    /// custom lock-free stacks, and multiple bucket partitions to reduce contention.
    /// </summary>
    /// <typeparam name="T">The type of the elements stored in the arrays in the pool.</typeparam>
    /// <remarks>
    /// Thread-Safety and Lock-Free Operations:
    /// ConcurrentArrayPool utilizes AsyncLocal&lt;LockFreeStack&lt;T[]&gt;&gt; and custom-designed lock-free stacks along with ConcurrentDictionary to handle
    /// concurrent access by multiple threads securely and without manual locks.
    ///
    /// Dynamic Length Partitioning:
    /// Arrays are partitioned into length groups with a multi-level structure, allowing threads working with different array sizes to access separate sets of buckets,
    /// reducing contention between threads.
    ///
    /// Efficient Array Reuse and Caching:
    /// Arrays are buffered in both thread-local caches and a global pool to minimize memory allocations and deallocations, and ultimately boosting overall performance.
    ///
    /// Usage Scenarios:
    /// Consider using ConcurrentArrayPool in multi-threaded and high-concurrency environments where fast access to arrays of various lengths is necessary.
    /// Be sure to fine-tune and test the implementation according to the specific workloads and application requirements to ensure optimal performance.
    /// </remarks>
    public class ConcurrentArrayPool<T> : IDisposable
    {
        private const int MaxArrayLength = 50_000;
        private const int MaxArraysPerBucket = 50;
        private const int SmallCacheSize = 3;
        private const int LargeCacheSize = 10;
        private const double LengthThreshold = 0.5;
        private const int NumberOfLengthGroups = 3;
        private const int PartitionCount = 4;

        private static readonly int[] GroupUpperBounds = { 256, 4096, MaxArrayLength };

        private readonly ConcurrentDictionary<int, LockFreeStack<T[]>>[][] _groupedBuckets;
        private readonly AsyncLocal<LockFreeStack<T[]>> _threadLocalCache = new AsyncLocal<LockFreeStack<T[]>>();
        private readonly object _disposeLock = new object();
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentArrayPool{T}"/> class.
        /// </summary>
        public ConcurrentArrayPool()
        {
            _groupedBuckets = new ConcurrentDictionary<int, LockFreeStack<T[]>>[NumberOfLengthGroups][];

            for (var group = 0; group < NumberOfLengthGroups; group++)
            {
                _groupedBuckets[group] = new ConcurrentDictionary<int, LockFreeStack<T[]>>[PartitionCount];
                for (var partition = 0; partition < PartitionCount; partition++)
                {
                    _groupedBuckets[group][partition] = new ConcurrentDictionary<int, LockFreeStack<T[]>>();
                }
            }
        }

        /// <summary>
        /// Rents an array from the pool.
        /// </summary>
        /// <param name="minimumLength">The required array length</param>
        /// <returns>The rented array</returns>
        public T[] Rent(int minimumLength)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("ConcurrentArrayPool");
            }

            if (minimumLength > MaxArrayLength)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLength), $"Length of array cannot exceed {MaxArrayLength}.");
            }

            minimumLength = GetBucketSize(minimumLength);

            var cache = _threadLocalCache.Value;
            if (cache == null)
            {
                cache = new LockFreeStack<T[]>();
                _threadLocalCache.Value = cache;
            }

            if (cache.TryPop(out var buffer) && buffer.Length >= minimumLength)
            {
                return buffer;
            }

            var bucketGroup = _groupedBuckets[GetLengthGroup(minimumLength)];
            int partition = minimumLength % PartitionCount;

            if (bucketGroup[partition].TryGetValue(minimumLength, out var bucket) && bucket.TryPop(out var array))
            {
                return array;
            }

            try
            {
                return new T[minimumLength];
            }
            catch (OutOfMemoryException)
            {
                if (bucket != null && bucket.TryPop(out array))
                {
                    return array;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns an array to the pool.
        /// </summary>
        /// <param name="array">Returning array.</param>
        /// <param name="clearArray">Whether the array should be cleaned.</param>
        public void Return(T[] array, bool clearArray = false)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("ConcurrentArrayPool");
            }

            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Length > MaxArrayLength)
            {
                return; // Avoid accumulating very large arrays
            }

            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            var cache = _threadLocalCache.Value;
            if (cache == null)
            {
                cache = new LockFreeStack<T[]>();
                _threadLocalCache.Value = cache;
            }

            int cacheSize = (array.Length <= (int)(LengthThreshold * MaxArrayLength)) ? SmallCacheSize : LargeCacheSize;

            if (cache.Count < cacheSize)
            {
                cache.Push(array);
                return;
            }

            int lengthGroup = GetLengthGroup(array.Length);
            int partition = array.Length % PartitionCount;
            var bucketGroup = _groupedBuckets[lengthGroup];
            var bucket = bucketGroup[partition].GetOrAdd(array.Length, _ => new LockFreeStack<T[]>());

            if (bucket.Count >= MaxArraysPerBucket)
            {
                return;
            }

            bucket.Push(array);
        }

        /// <summary>
        /// Disposes.
        /// </summary>
        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _threadLocalCache.Value = null;

                foreach (var bucketGroup in _groupedBuckets)
                {
                    foreach (var partition in bucketGroup)
                    {
                        foreach (var bucket in partition.Values)
                        {
                            while (bucket.TryPop(out _))
                            {
                                // Ignored
                            }
                        }

                        partition.Clear();
                    }
                }

                _isDisposed = true;
            }
        }

        private static int GetBucketSize(int length)
        {
            int bucketSize = 1;
            while (bucketSize < length)
            {
                bucketSize <<= 1;
            }

            return bucketSize;
        }

        private int GetLengthGroup(int arrayLength)
        {
            for (int i = 0; i < GroupUpperBounds.Length; ++i)
            {
                if (arrayLength <= GroupUpperBounds[i])
                {
                    return i;
                }
            }

            return GroupUpperBounds.Length - 1;
        }

        /// <summary>
        /// LockFreeStack helper class.
        /// </summary>
        /// <typeparam name="TStackItem">The stack item type</typeparam>
        public class LockFreeStack<TStackItem>
        {
            private Node _head;

            /// <summary>
            /// Gets the amount of elements inside the stack.
            /// </summary>
            public int Count
            {
                get
                {
                    int count = 0;
                    Node current = _head;
                    while (current != null)
                    {
                        count++;
                        current = current.Next;
                    }

                    return count;
                }
            }

            /// <summary>
            /// Pushes an item into the stack.
            /// </summary>
            /// <param name="item">The item.</param>
            public void Push(TStackItem item)
            {
                Node oldHead;
                Node newHead = new Node { Value = item };
                do
                {
                    oldHead = _head;
                    newHead.Next = oldHead;
                }
                while (Interlocked.CompareExchange(ref _head, newHead, oldHead) != oldHead);
            }

            /// <summary>
            /// Popping an item out from the stack.
            /// </summary>
            /// <param name="result">Popped item.</param>
            /// <returns>true on success, false otherwise</returns>
            public bool TryPop(out TStackItem result)
            {
                Node oldHead;
                Node newHead;
                do
                {
                    oldHead = _head;
                    if (oldHead == null)
                    {
                        result = default;
                        return false;
                    }

                    newHead = oldHead.Next;
                }
                while (Interlocked.CompareExchange(ref _head, newHead, oldHead) != oldHead);

                result = oldHead.Value;
                return true;
            }

            private class Node
            {
#pragma warning disable SA1401
                public TStackItem Value;
                public Node Next;
#pragma warning restore SA1401
            }
        }
    }
}
