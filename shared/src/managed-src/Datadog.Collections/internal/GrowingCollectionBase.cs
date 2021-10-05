using System;
using System.Runtime.CompilerServices;
using System.Threading;

using Datadog.Util;

#pragma warning disable SA1124 // Do not use regions
#pragma warning disable SA1131 // Use readable conditions
namespace Datadog.Collections
{
    #region class GrowingCollectionBase<T>

    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.
    /// This class is the base for specific implementations and should not be used directly. Implementations include:
    /// <ul>
    ///   <li><see cref="GrowingCollection{T}" />
    ///         A "normal" collection where items are accessed by value.
    ///         If <c>T</c> is a class type or a primitive type, you propably (but not certainly) want this favor.
    ///   </li>
    ///   <li><see cref="GrowingRefCollectionInternal{T}" />
    ///         A by-ref collection where items are accessed by reference directly inside the underlying memory.
    ///         If <c>T</c> is a custom value type, you probably (but not certainly) want this flavor.
    ///         Values will be accessed directly within the underlying storage, modified in-place and never boxed.
    ///         Pattern based for-each-iteration is supported using an internal interface (<see cref="IRefEnumerableInternal{T}" />),
    ///         so using this class does not expose any public types form the assembly.
    ///         Other than the implemented by-ref iteration iface, this class is equivalent to <see cref="GrowingRefCollection{T}" />.
    ///   </li>
    ///   <li><see cref="GrowingRefCollection{T}" />
    ///         A by-ref collection where items are accessed by reference directly inside the underlying memory.
    ///         If <c>T</c> is a custom value type, you probably (but not certainly) want this flavor.
    ///         Values will be accessed directly within the underlying storage, modified in-place and never boxed.
    ///         Pattern based for-each-iteration is supported using a public interface (<see cref="IRefEnumerable{T}" />),
    ///         collections backd by this class can be exposed through public APIs via that iface.
    ///         Other than the implemented by-ref iteration iface, this class is equivalent to <see cref="GrowingRefCollectionInternal{T}" />.
    ///   </li>
    /// </ul>
    /// Items in the collection must be value types and boxing/unboxing is always avoided during access.</summary>
    /// <remarks>The name suffix <c>Internal</c> indicates that this class implements only internal interfaces.
    /// There is a subclass without that suffix (<see cref="GrowingRefCollection{T}" />) that implements the
    /// equivalent public interfaces.
    /// Application can choose which to use in the context of allowing vs. avoiding adding any public types.
    /// </remarks>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingCollectionBase<T> : GrowingCollectionSegment<T>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0049:Simplify Names", Justification = "Max Size logic reasons on Framework rather than language type names")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0055:Formatting", Justification = "Using spaces to lay out Max Size table")]
        public static class SegmentSizes
        {
            /// <summary>
            /// When working with a ralatively small number of elements, a small defualt size allows wasting little memory.
            /// If you know to process hundreds or thousands of objects, use larger segment sizes.
            /// </summary>
            public const int Default = 32;

            private const int MaxMax = 50000;

            /// <summary>
            /// We restrict the max segment size to make it less likely that huge segments get allocated and end up on the
            /// large object heap (LOH). Note that if the collection items are of a custom value-type, this can still happen.
            /// For example, if the size of a value-typed collection item is just 18 bytes (e.g. 2 longs and 2 bytes),
            /// it would only take 4,723 items per segment to cross the LOH threshold of 85,000 bytes.
            /// We restrict the max segment sizes for built-in types to avoid LOH allocations.
            /// E.g., in 64 bit systems, a segment of 10000 class-type items takes up 78.125 KBytes, a sizable block,
            /// but still safely within the Large Object Head threshold.
            /// <para>! However, be very careful to avoid LOH allocations when chosing segment sizes for storing custom value-type items !</para>
            /// <para>Unless working with very large amounts of items, smaller segment sizes of a few KBytes or less will yield better
            /// application performance.</para>
            /// </summary>
            public static readonly int Max = (typeof(T) == typeof(Boolean)) ? MaxMax
                                           : (typeof(T) == typeof(Byte))    ? MaxMax
                                           : (typeof(T) == typeof(SByte))   ? MaxMax
                                           : (typeof(T) == typeof(Char))    ?  40000
                                           : (typeof(T) == typeof(Decimal)) ?   5000
                                           : (typeof(T) == typeof(Double))  ?  10000
                                           : (typeof(T) == typeof(Single))  ?  20000
                                           : (typeof(T) == typeof(Int32))   ?  20000
                                           : (typeof(T) == typeof(UInt32))  ?  20000
                                           : (typeof(T) == typeof(IntPtr))  ?  10000
                                           : (typeof(T) == typeof(UIntPtr)) ?  10000
                                           : (typeof(T) == typeof(Int64))   ?  10000
                                           : (typeof(T) == typeof(UInt64))  ?  10000
                                           : (typeof(T) == typeof(Int16))   ?  40000
                                           : (typeof(T) == typeof(UInt16))  ?  40000
                                           : (typeof(T).IsClass)            ?  10000
                                           : (typeof(T).IsInterface)        ?  10000
                                           :                                   4700;
        }

        private GrowingCollectionSegment<T> _dataHead;

        /// <summary>Initializes a new instance of the <see cref="GrowingCollectionBase{T}"/> class.
        /// </summary>
        public GrowingCollectionBase()
            : base(SegmentSizes.Default)
        {
            _dataHead = this;
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingCollectionBase{T}"/> class.</summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.
        /// Be very careful to avoid LOH allocations when chosing segment sizes for storing custom value-type items.
        /// Unless working with very large numbers of items, smaller segment sizes (order of a few dozen to a few thousand) yield better performance.
        /// See also: <seealso cref="GrowingCollectionBase{T}.SegmentSizes.Max" /></param>
        public GrowingCollectionBase(int segmentSize)
            : base(segmentSize)
        {
            _dataHead = this;
        }

        private protected GrowingCollectionSegment<T> DataHead
        {
            get { return _dataHead; }
        }

        /// <summary>Gets the current number of items in the collection.</summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                GrowingCollectionSegment<T> currHead = _dataHead;
                return currHead.Segment_GlobalCount;
            }
        }

        /// <summary>Adds an item that equals to <c>default(T)</c> to the collection.</summary>
        /// <param name="item">Reference to the item that was added.</param>
        private protected ref T AddDefault()
        {
            GrowingCollectionSegment<T> currHead = Volatile.Read(ref _dataHead);

            bool added = currHead.Segment_TryAddDefault(out int index);
            while (!added)
            {
                var newHead = new GrowingCollectionSegment<T>(currHead);
                currHead = Concurrent.CompareExchangeResult(ref _dataHead, newHead, currHead);

                added = currHead.Segment_TryAddDefault(out index);
            }

            return ref currHead.Segment_GetItemRef(index);
        }

        /// <summary>Adds an item to the collection.</summary>
        /// <param name="item">Item to be added.</param>
        private protected void AddItem(T item)
        {
            GrowingCollectionSegment<T> currHead = Volatile.Read(ref _dataHead);

            bool added = currHead.Segment_TryAddItem(item);
            while (!added)
            {
                var newHead = new GrowingCollectionSegment<T>(currHead);
                currHead = Concurrent.CompareExchangeResult(ref _dataHead, newHead, currHead);

                added = currHead.Segment_TryAddItem(item);
            }
        }

        #region class Enumerator

        /// <summary>An enumerator implementation for a <see cref="GrowingCollectionBase{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        internal class Enumerator
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
                _headOffset = _currentSegmentOffset = head.Segment_LocalCount;
                _count = _headOffset + (_head.Segment_Next == null ? 0 : _head.Segment_Next.Segment_GlobalCount);
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

            /// <summary>Gets a reference to the current element.</summary>
            private protected ref T CurrentItemRef
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return ref _currentSegment.Segment_GetItemRef(_currentSegmentOffset);
                }
            }

            /// <summary>Gets the current element.</summary>
            private protected T CurrentItem
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return _currentSegment.Segment_GetItem(_currentSegmentOffset);
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
                    if (_currentSegment.Segment_Next == null)
                    {
                        return false;
                    }
                    else
                    {
                        _currentSegment = _currentSegment.Segment_Next;
                        _currentSegmentOffset = _currentSegment.Segment_LocalCount - 1;
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

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                }
            }
        }

        #endregion class Enumerator
    }

    #endregion class GrowingCollectionBase<T>

    #region class GrowingCollectionSegment<T>

    internal class GrowingCollectionSegment<T>
    {
        private readonly GrowingCollectionSegment<T> _nextSegment;
        private readonly int _segmentSize;
        private readonly int _nextSegmentGlobalCount;
        private readonly T[] _data;
        private int _localCount;

        /// <summary>Creates a new Growing Collection Segment and allocated the underlying memory.</summary>
        /// <param name="segmentSize">The capacity of this segment in terms of the number of collection items.
        /// Be very careful to avoid LOH allocations when chosing segment sizes for storing custom value-type items.
        /// Unless working with very large numbers of items, smaller segment sizes (order of a few dozen to a few thousand) yield better performance.
        /// See also: <seealso cref="GrowingCollectionBase{T}.SegmentSizes.Max" /></param>
        public GrowingCollectionSegment(int segmentSize)
        {
            if (segmentSize < 1 || GrowingCollectionBase<T>.SegmentSizes.Max < segmentSize)
            {
                throw new ArgumentOutOfRangeException(nameof(segmentSize),
                                                      $"{segmentSize} must be in range 1..{GrowingCollectionBase<T>.SegmentSizes.Max}"
                                                    + $" for items of type \"{typeof(T).FullName}\", but the specified value is {segmentSize}.");
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
                throw new ArgumentNullException(nameof(nextSegment),
                                                $"{nextSegment} may not be null; if there is no {nextSegment}, use another ctor overload.");
            }

            _segmentSize = nextSegment.Segment_Size;
            _nextSegment = nextSegment;
            _nextSegmentGlobalCount = nextSegment.Segment_GlobalCount;

            _data = new T[_segmentSize];
            _localCount = 0;
        }

        internal int Segment_Size
        {
            get
            {
                return _segmentSize;
            }
        }

        internal int Segment_LocalCount
        {
            get
            {
                int lc = _localCount;
                return (lc > _segmentSize) ? _segmentSize : lc;
            }
        }

        internal GrowingCollectionSegment<T> Segment_Next
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _nextSegment;
            }
        }

        internal int Segment_GlobalCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Segment_LocalCount + _nextSegmentGlobalCount;
            }
        }

        internal ref T Segment_GetItemRef(int index)
        {
            if (index < 0 || _localCount <= index || _segmentSize <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Invalid index ({index})");
            }

            return ref _data[index];
        }

        internal T Segment_GetItem(int index)
        {
            if (index < 0 || _localCount <= index || _segmentSize <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Invalid index ({index})");
            }

            return _data[index];
        }

        internal bool Segment_TryAddDefault(out int addedItemIndex)
        {
            int index = Interlocked.Increment(ref _localCount) - 1;
            if (index >= _segmentSize)
            {
                Interlocked.Decrement(ref _localCount);
                addedItemIndex = -1;
                return false;
            }

            addedItemIndex = index;
            return true;
        }

        internal bool Segment_TryAddItem(T item)
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
