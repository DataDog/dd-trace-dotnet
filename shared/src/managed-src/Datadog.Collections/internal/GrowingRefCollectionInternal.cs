using System;
using System.Runtime.CompilerServices;

namespace Datadog.Collections
{
    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.
    /// Access to items is provided by reference:
    /// If collection items are of a value type, boxing/unboxing is always avoided during access.</summary>
    /// <remarks>The name suffix <c>Internal</c> indicates that this class implements only internal interfaces.
    /// There is a subclass without that suffix (<see cref="GrowingRefCollection{T}" />) that implements the
    /// equivalent public interfaces.
    /// Application can choose which to use in the context of allowing vs. avoiding adding any public types.
    /// <para>See <see cref="GrowingCollectionBase{T}" /> for an overview of different Growing-Collection-Style collection types.</para>
    /// </remarks>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingRefCollectionInternal<T> : GrowingCollectionBase<T>, IRefEnumerableInternal<T>, IReadOnlyRefCollectionInternal<T>
    {
        /// <summary>Initializes a new instance of the <see cref="GrowingRefCollectionInternal{T}"/> class.
        /// </summary>
        public GrowingRefCollectionInternal()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingRefCollectionInternal{T}"/> class.</summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.
        /// Be very careful to avoid LOH allocations when chosing segment sizes for storing custom value-type items.
        /// Unless working with very large numbers of items, smaller segment sizes (order of a few dozen to a few thousand) yield better performance.
        /// See also: <seealso cref="GrowingCollectionBase{T}.SegmentSizes.Max" /></param>
        public GrowingRefCollectionInternal(int segmentSize)
            : base(segmentSize)
        {
        }

        /// <summary>Adds an item that equals to <c>default(T)</c> to the collection.</summary>
        /// <param name="item">Reference to the item that was added.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Add()
        {
            return ref base.AddDefault();
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual GrowingRefCollectionInternal<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingRefCollectionInternal<T>.Enumerator(DataHead);
            return enumerator;
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IRefEnumeratorInternal<T> IRefEnumerableInternal<T>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>An enumerator implementation for a <see cref="GrowingRefCollectionInternal{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        public new class Enumerator : GrowingCollectionBase<T>.Enumerator, IRefEnumeratorInternal<T>
        {
            internal Enumerator(GrowingCollectionSegment<T> head)
                : base(head)
            {
            }

            /// <summary>Gets the current element.</summary>
            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return ref base.CurrentItemRef;
                }
            }
        }
    }
}
