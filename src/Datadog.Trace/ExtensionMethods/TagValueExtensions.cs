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
        public static bool? ToBoolean(this TagValue value)
        {
            if (value.IsMetrics)
            {
                return value.DoubleValue == 1d;
            }

            if (value.StringValue == null)
            {
                return null;
            }

            if (string.Compare("TRUE", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("YES", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("T", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("Y", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("1", value.StringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            if (string.Compare("FALSE", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("NO", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("F", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("N", value.StringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("0", value.StringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            return null;
        }
    }
}
