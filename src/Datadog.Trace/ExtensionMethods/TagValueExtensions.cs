using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class TagValueExtensions
    {
        /// <summary>
        /// Converts a <see cref="TagValue"/> into a <see cref="bool"/> by comparing it to commonly used values
        /// such as "True", "yes", or "1". Case-insensitive. Defaults to <c>false</c> if string is not recognized.
        /// </summary>
        /// <param name="value">The TagValue to convert.</param>
        /// <returns><c>true</c> if is one of the accepted values for <c>true</c>; <c>false</c> otherwise.</returns>
        public static bool? ToBoolean(this TagValue? value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            var tagValue = value.Value;
            if (tagValue.IsMetrics)
            {
                return tagValue.DoubleValue == 1d;
            }

            if (tagValue.StringValue == null)
            {
                return null;
            }

            if (string.Compare("TRUE", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("YES", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("T", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("Y", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("1", tagValue.StringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            if (string.Compare("FALSE", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("NO", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("F", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("N", tagValue.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("0", tagValue.StringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            return null;
        }
    }
}
