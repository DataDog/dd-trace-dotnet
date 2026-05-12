// <copyright file="EnumerableExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class EnumerableExtensions
    {
        public static bool NullableSequentialEquals<T>(this IEnumerable<T> @this, IEnumerable<T> other)
        {
            if (ReferenceEquals(null, @this))
            {
                return ReferenceEquals(null, other);
            }

            if (ReferenceEquals(null, other))
            {
                return false;
            }

            return @this.SequenceEqual(other);
        }

        // Array overload preferred by overload resolution when the static type is T[].
        // Avoids the IEnumerator<T> heap allocation and interface dispatch that the IEnumerable<T>
        // overload incurs on arrays, which matters because every current caller passes an array
        // (Tags, AdditionalIds, Lines, Decorations, Segments, PackagePrefixes, Classes...).
        public static int NullableSequentialHashCode<T>(this T[] @this)
        {
            if (@this is null)
            {
                return 0;
            }

            var hashCode = new HashCode();
            for (var i = 0; i < @this.Length; i++)
            {
                hashCode.Add(@this[i]);
            }

            return hashCode.ToHashCode();
        }

        public static int NullableSequentialHashCode<T>(this IEnumerable<T> @this)
        {
            if (ReferenceEquals(null, @this))
            {
                return 0;
            }

            var hashCode = new HashCode();
            foreach (var item in @this)
            {
                hashCode.Add(item);
            }

            return hashCode.ToHashCode();
        }
    }
}
