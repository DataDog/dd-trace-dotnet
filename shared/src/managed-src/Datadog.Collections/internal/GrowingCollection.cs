using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using Datadog.Util;

#pragma warning disable SA1124 // Do not use regions
#pragma warning disable SA1131 // Use readable conditions
namespace Datadog.Collections
{
    #region class GrowingCollection<T>

    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.</summary>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingCollection<T> : GrowingCollectionSegment<T>, IEnumerable<T>, IReadOnlyCollection<T>
    {
        private const int DefaultSegmentSize = 32;

        private GrowingCollectionSegment<T> _dataHead;

        /// <summary>Initializes a new instance of the <see cref="GrowingCollection{T}"/> class.
        /// </summary>
        public GrowingCollection()
            : base(DefaultSegmentSize)
        {
            _dataHead = this;
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingCollection{T}"/> class.
        /// </summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.</param>
        public GrowingCollection(int segmentSize)
            : base(segmentSize)
        {
            _dataHead = this;
        }

        /// <summary>Gets the current number of items in the collection.</summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                GrowingCollectionSegment<T> currHead = _dataHead;
                return currHead.GlobalCount;
            }
        }

        /// <summary>Adds an item to the collection.</summary>
        /// <param name="item">Item to be added.</param>
        public void Add(T item)
        {
            GrowingCollectionSegment<T> currHead = Volatile.Read(ref _dataHead);

            bool added = currHead.TryAdd(item);
            while (!added)
            {
                var newHead = new GrowingCollectionSegment<T>(currHead);
                Concurrent.CompareExchangeResult(ref _dataHead, newHead, currHead);

                GrowingCollectionSegment<T> updatedHead = Interlocked.CompareExchange(ref _dataHead, newHead, currHead);
                added = updatedHead.TryAdd(item);
            }
        }

        /// <summary>Gets an enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GrowingCollection<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingCollection<T>.Enumerator(_dataHead);
            return enumerator;
        }

        /// <summary>Gets an enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>Gets an enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #region class Enumerator

        /// <summary>An enumerator implementation for a <see cref="GrowingCollection{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        public class Enumerator : IEnumerator<T>
        {
            private readonly GrowingCollectionSegment<T> _head;
            private readonly int _headOffset;
            private readonly int _count;
            private GrowingCollectionSegment<T> _currentSegment;
            private int _currentSegmentOffset;

            internal Enumerator(GrowingCollectionSegment<T> head)
            {
                Validate.NotNull(head, nameof(head));

                _head = _currentSegment = head;
                _headOffset = _currentSegmentOffset = head.LocalCount;
                _count = _headOffset + (_head.NextSegment == null ? 0 : _head.NextSegment.GlobalCount);
            }

            /// <summary>Gets the total number of elements returned by this enumerator.</summary>
            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return _count;
                }
            }

            /// <summary>Gets the current element.</summary>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return _currentSegment[_currentSegmentOffset];
                }
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return this.Current;
                }
            }

            /// <summary>Disposes this enumerator.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>Move to the next element in the underlying colection.</summary>
            /// <returns>The next element in the underlying collection.</returns>
            public bool MoveNext()
            {
                if (_currentSegmentOffset == 0)
                {
                    if (_currentSegment.NextSegment == null)
                    {
                        return false;
                    }
                    else
                    {
                        _currentSegment = _currentSegment.NextSegment;
                        _currentSegmentOffset = _currentSegment.LocalCount - 1;
                        return true;
                    }
                }
                else
                {
                    _currentSegmentOffset--;
                    return true;
                }
            }

            /// <summary>Restarts this enumerator to the same state as it was created in.</summary>
            public void Reset()
            {
                _currentSegment = _head;
                _currentSegmentOffset = _headOffset;
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                }
            }
        }

        #endregion class Enumerator
    }

    #endregion class GrowingCollection<T>

    #region class GrowingCollectionSegment<T>

    internal class GrowingCollectionSegment<T>
    {
        private readonly GrowingCollectionSegment<T> _nextSegment;
        private readonly int _segmentSize;
        private readonly int _nextSegmentGlobalCount;
        private readonly T[] _data;
        private int _localCount;

        public GrowingCollectionSegment(int segmentSize)
        {
            if (segmentSize < 1 || 1024 < segmentSize)
            {
                throw new ArgumentOutOfRangeException(
                                nameof(segmentSize),
                                $"{segmentSize} must be in range 1..1024, but the specified value is {segmentSize}.");
            }

            _segmentSize = segmentSize;
            _nextSegment = null;
            _nextSegmentGlobalCount = 0;

            _data = new T[segmentSize];
            _localCount = 0;
        }

        public GrowingCollectionSegment(GrowingCollectionSegment<T> nextSegment)
        {
            if (nextSegment == null)
            {
                throw new ArgumentNullException(
                                nameof(nextSegment),
                                $"{nextSegment} may not be null; if there is no {nextSegment}, use another ctor overload.");
            }

            _segmentSize = nextSegment.SegmentSize;
            _nextSegment = nextSegment;
            _nextSegmentGlobalCount = nextSegment.GlobalCount;

            _data = new T[_segmentSize];
            _localCount = 0;
        }

        public int SegmentSize
        {
            get
            {
                return _segmentSize;
            }
        }

        public int LocalCount
        {
            get
            {
                int lc = _localCount;
                if (lc > _segmentSize)
                {
                    return _segmentSize;
                }
                else
                {
                    return lc;
                }
            }
        }

        public GrowingCollectionSegment<T> NextSegment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _nextSegment;
            }
        }

        public int GlobalCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.LocalCount + _nextSegmentGlobalCount;
            }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || _localCount <= index || _segmentSize <= index)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"Invalid index ({index})");
                }

                return _data[index];
            }
        }

        internal bool TryAdd(T item)
        {
            int index = Interlocked.Increment(ref _localCount) - 1;
            if (index >= _segmentSize)
            {
                Interlocked.Decrement(ref _localCount);
                return false;
            }

            _data[index] = item;
            return true;
        }
    }

    #endregion class GrowingCollectionSegment<T>
}
