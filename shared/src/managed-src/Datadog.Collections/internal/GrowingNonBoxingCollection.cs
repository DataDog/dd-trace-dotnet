using System;
using System.Runtime.CompilerServices;

namespace Datadog.Collections
{
    /// <summary>A very fast, lock free, unordered collection to which items can be added, but never removed.
    /// Access to items is provided by reference:
    /// If collection items are of a value type, boxing/unboxing is always avoided during access.</summary>
    /// <remarks>This class subclasses <seealso cref="GrowingNonBoxingCollectionInternal{T}" />. The only difference is that
    /// it additionally implements public iteration ifaces (<seealso cref="IRefEnumerable{T}" />, <seealso cref="IReadOnlyRefCollection{T}" />).
    /// That allows exposing instances of this collection via those ifaces.</remarks>
    /// <typeparam name="T">Type of collection elements.</typeparam>
    internal class GrowingNonBoxingCollection<T>
                                        : GrowingNonBoxingCollectionInternal<T>, IRefEnumerable<T>, IReadOnlyRefCollection<T>
    {
        /// <summary>Initializes a new instance of the <see cref="GrowingNonBoxingCollection{T}"/> class.
        /// </summary>
        public GrowingNonBoxingCollection()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="GrowingNonBoxingCollection{T}"/> class.
        /// </summary>
        /// <param name="segmentSize">The collection will grow in chunks of <c>segmentSize</c> items.</param>
        public GrowingNonBoxingCollection(int segmentSize)
            : base(segmentSize)
        {
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>The returned enumerator has the static type <seealso cref="GrowingNonBoxingCollectionInternal{T}.Enumerator" />, 
        /// but its runtime type will always be <seealso cref="GrowingNonBoxingCollection{T}.Enumerator" /></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override GrowingNonBoxingCollectionInternal<T>.Enumerator GetEnumerator()
        {
            var enumerator = new GrowingNonBoxingCollection<T>.Enumerator(DataHead);
            return enumerator;
        }

        /// <summary>Gets a ref enumerator over this colletion. No particular element order is guaranteed.
        /// The enumerator is resilient to concurrent additions to the collection.</summary>
        /// <returns>A new enumerator that will cover all items already in the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IRefEnumerator<T> IRefEnumerable<T>.GetEnumerator()
        {
            return (GrowingNonBoxingCollection<T>.Enumerator) this.GetEnumerator();
        }

        /// <summary>An enumerator implementation for a <see cref="GrowingNonBoxingCollection{T}"/>.
        /// The enumerator is resilient to concurrent additions to the collection.
        /// No particular element order is guaranteed.</summary>
        public new class Enumerator : GrowingNonBoxingCollectionInternal<T>.Enumerator, IRefEnumerator<T>
        {
            internal Enumerator(GrowingCollectionSegment<T> head)
                : base(head)
            {
            }
        }
    }
}
