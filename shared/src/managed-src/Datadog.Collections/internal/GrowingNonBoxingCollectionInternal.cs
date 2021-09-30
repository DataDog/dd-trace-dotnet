using System;
using System.Runtime.CompilerServices;

namespace Datadog.Collections
{
    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.
    /// Access to items is provided by reference:
    /// If collection items are of a value type, boxing/unboxing is always avoided during access.</summary>
    /// <remarks>The name suffix <c>Internal</c> indicates that this class implements only internal interfaces.
    /// There is a subclass without that suffix (<see cref="GrowingNonBoxingCollection{T}" />) that implements the
    /// equivalent public interfaces.
    /// Application can choose which to use in the context of allowing vs. avoiding adding any public types.
    /// </remarks>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingNonBoxingCollectionInternal<T>
                                        : GrowingCollectionBase<T>, IRefEnumerableInternal<T>, IReadOnlyRefCollectionInternal<T>
    {
        /// <summary>Initializes a new instance of the <see cref="GrowingNonBoxingCollectionInternal{T}"/> class.
        /// </summary>
        public GrowingNonBoxingCollectionInternal()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingNonBoxingCollectionInternal{T}"/> class.
        /// </summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.</param>
        public GrowingNonBoxingCollectionInternal(int segmentSize)
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
        public virtual GrowingNonBoxingCollectionInternal<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingNonBoxingCollectionInternal<T>.Enumerator(DataHead);
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

        /// <summary>An enumerator implementation for a <see cref="GrowingNonBoxingCollectionInternal{T}"/>.
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
