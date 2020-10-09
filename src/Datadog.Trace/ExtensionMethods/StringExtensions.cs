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
        /// such as "True", "yes", "T", "Y", or "1" for <c>true</c> and "False", "no", "F", "N", or "0" for <c>false</c>. Case-insensitive.
        /// Defaults to <c>null</c> if string is not recognized.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <returns><c>true</c> or <c>false</c> if <paramref name="value"/> is one of the accepted values; <c>null</c> otherwise.</returns>
        public static bool? ToBoolean(this string value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            if (value.Length == 0)
            {
                return null;
            }

            if (value.Length == 1)
            {
                if (value[0] == 'T' || value[0] == 't' ||
                    value[0] == 'Y' || value[0] == 'y' ||
                    value[0] == '1')
                {
                    return true;
                }

                if (value[0] == 'F' || value[0] == 'f' ||
                    value[0] == 'N' || value[0] == 'n' ||
                    value[0] == '0')
                {
                    return false;
                }

                return null;
            }

            if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }
    }
}
