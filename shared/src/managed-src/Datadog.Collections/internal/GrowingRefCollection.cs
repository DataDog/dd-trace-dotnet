using System;
using System.Runtime.CompilerServices;

namespace Datadog.Collections
{
    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.
    /// Access to items is provided by reference:
    /// If collection items are of a value type, boxing/unboxing is always avoided during access.</summary>
    /// <remarks>This class subclasses <seealso cref="GrowingRefCollectionInternal{T}" />. The only difference is that
    /// it additionally implements public iteration ifaces (<seealso cref="IRefEnumerable{T}" />, <seealso cref="IReadOnlyRefCollection{T}" />).
    /// That allows exposing instances of this collection via those ifaces.</remarks>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingRefCollection<T> : GrowingRefCollectionInternal<T>, IRefEnumerable<T>, IReadOnlyRefCollection<T>
    {
        /// <summary>Initializes a new instance of the <see cref="GrowingRefCollection{T}"/> class.
        /// </summary>
        public GrowingRefCollection()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingRefCollection{T}"/> class.</summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.
        /// Be very careful to avoid LOH allocations when chosing segment sizes for storing custom value-type items.
        /// Unless working with very large numbers of items, smaller segment sizes (order of a few dozen to a few thousand) yield better performance.
        /// See also: <seealso cref="GrowingCollectionBase{T}.SegmentSizes.Max" /></param>
        public GrowingRefCollection(int segmentSize)
            : base(segmentSize)
        {
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>The returned enumerator has the static type <seealso cref="GrowingRefCollectionInternal{T}.Enumerator" />, 
        /// but its runtime type will always be <seealso cref="GrowingRefCollection{T}.Enumerator" /></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override GrowingRefCollectionInternal<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingRefCollection<T>.Enumerator(DataHead);
            return enumerator;
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IRefEnumerator<T> IRefEnumerable<T>.GetEnumerator()
        {
            return (GrowingRefCollection<T>.Enumerator) this.GetEnumerator();
        }

        /// <summary>An enumerator implementation for a <see cref="GrowingRefCollection{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        public new class Enumerator : GrowingRefCollectionInternal<T>.Enumerator, IRefEnumerator<T>
        {
            internal Enumerator(GrowingCollectionSegment<T> head)
                : base(head)
            {
            }
        }
    }
}
