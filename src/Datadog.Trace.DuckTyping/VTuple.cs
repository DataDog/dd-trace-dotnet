using System;
using System.Collections.Generic;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Value Tuple struct
    /// </summary>
    /// <typeparam name="T1">Type of the item 1</typeparam>
    /// <typeparam name="T2">Type of the item 2</typeparam>
    internal readonly struct VTuple<T1, T2> : IEquatable<VTuple<T1, T2>>
    {
        /// <summary>
        /// Item 1
        /// </summary>
        public readonly T1 Item1;

        /// <summary>
        /// Item 2
        /// </summary>
        public readonly T2 Item2;

        /// <summary>
        /// Initializes a new instance of the <see cref="VTuple{T1, T2}"/> struct.
        /// </summary>
        /// <param name="item1">Item 1</param>
        /// <param name="item2">Item 2</param>
        public VTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        /// <summary>
        /// Gets the struct hashcode
        /// </summary>
        /// <returns>Hashcode</returns>
        public override int GetHashCode()
        {
            return (Item1?.GetHashCode() ?? 0) + (Item2?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// Gets if the struct is equal to other object or struct
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if both are equals; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return obj is VTuple<T1, T2> vTuple &&
                   EqualityComparer<T1>.Default.Equals(Item1, vTuple.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, vTuple.Item2);
        }

        /// <inheritdoc />
        public bool Equals(VTuple<T1, T2> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, other.Item2);
        }
    }
}
