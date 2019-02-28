using System;
using System.Globalization;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class StringExtensions
    {
        /// <summary>
        /// Removes the trailing occurrence of a substring from the current string.
        /// </summary>
        /// <param name="value">The original string.</param>
        /// <param name="suffix">The string to remove from the end of <paramref name="value"/>.</param>
        /// <param name="comparisonType">One of the enumeration values that determines how this string and <paramref name="suffix"/> are compared.</param>
        /// <returns>A new string with <paramref name="suffix"/> removed from the end, if found. Otherwise, <paramref name="value"/>.</returns>
        public static string TrimEnd(this string value, string suffix, StringComparison comparisonType)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            return !string.IsNullOrEmpty(suffix) && value.EndsWith(suffix, comparisonType)
                       ? value.Substring(0, value.Length - suffix.Length)
                       : value;
        }

        public static ulong? TryParseUInt64(this string value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            return ulong.TryParse(value, out var result)
                       ? result
                       : default;
        }

        public static ulong? TryParseUInt64(this string value, NumberStyles style, IFormatProvider provider)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            return ulong.TryParse(value, style, provider, out var result)
                       ? result
                       : default;
        }
    }
}
