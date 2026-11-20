// <copyright file="IntStringCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Caches the invariant decimal representation of small non-negative integers.
    /// </summary>
    /// <remarks>
    /// Tags backed by an <c>int</c> value have to be formatted
    /// as a string every time a span is serialized, matched against a regex trace filter, or read
    /// back through <c>GetTag</c>. Those values come from a small, bounded set, so caching them
    /// keeps those paths allocation-free after the first occurrence of each value.
    /// </remarks>
    internal static class IntStringCache
    {
        // Comfortably covers the HTTP status code range (100-599) with room for other small int tags.
        private const int MaxCachedExclusive = 1024;

        private static readonly string?[] Cache = new string?[MaxCachedExclusive];

        /// <summary>
        /// Gets the invariant decimal representation of <paramref name="value"/>, using a cached
        /// string when <paramref name="value"/> is small and non-negative.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The invariant decimal representation of <paramref name="value"/>.</returns>
        public static string ToInvariantString(int value)
        {
            if ((uint)value >= MaxCachedExclusive)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            // The race here is benign: two threads can only ever produce equal, immutable strings.
            return Cache[value] ??= value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
