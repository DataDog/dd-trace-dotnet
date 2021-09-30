using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Datadog.Collections
{
    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.    
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingCollection<T> : GrowingCollectionBase<T>, IEnumerable, IEnumerable<T>, IReadOnlyCollection<T>
    {
        /// <summary>Initializes a new instance of the <see cref="GrowingCollection{T}"/> class.
        /// </summary>
        public GrowingCollection()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingCollection{T}"/> class.
        /// </summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.</param>
        public GrowingCollection(int segmentSize)
            : base(segmentSize)
        {
        }

        /// <summary>Adds an item to the collection.</summary>
        /// <param name="item">Item to be added.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            base.AddItem(item);
        }

        /// <summary>Gets an enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual GrowingCollection<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingCollection<T>.Enumerator(DataHead);
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

        /// <summary>An enumerator implementation for a <see cref="GrowingCollection{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        public new class Enumerator : GrowingCollectionBase<T>.Enumerator, IEnumerator<T>, IEnumerator
        {
            internal Enumerator(GrowingCollectionSegment<T> head)
                : base(head)
            {
            }

            /// <summary>Gets the current element.</summary>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return base.CurrentItem;
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
        }
    }
}
