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

        // Array-only by design: every caller currently passes a T[] property.
        // An IEnumerable<T> overload would allocate an enumerator and dispatch through the
        // interface on every iteration, so we deliberately don't provide one.
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
    }
}
