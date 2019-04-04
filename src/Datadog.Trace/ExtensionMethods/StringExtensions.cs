using System;

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

        /// <summary>
        /// Converts a <see cref="string"/> into a <see cref="bool"/> by comparing it to commonly used values
        /// such as "True", "yes", or "1". Case-insensitive. Defaults to <c>false</c> if string is not recognized.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is one of the accepted values for <c>true</c>; <c>false</c> otherwise.</returns>
        public static bool? ToBoolean(this string value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            switch (value.ToUpperInvariant())
            {
                case "TRUE":
                case "YES":
                case "1":
                    return true;
                case "FALSE":
                case "NO":
                case "0":
                    return false;
                default:
                    return null;
            }
        }
    }
}
