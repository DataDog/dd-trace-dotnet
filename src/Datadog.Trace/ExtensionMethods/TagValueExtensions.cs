using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class TagValueExtensions
    {
        /// <summary>
        /// Converts a <see cref="TagValue"/> into a <see cref="bool"/> by comparing it to commonly used values
        /// such as "True", "yes", "T", "Y", or "1" for <c>true</c> and "False", "no", "F", "N", or "0" for <c>false</c>
        /// when the <see cref="TagValue"/> is a <see cref="string"/>.
        /// Returns <c>true</c> if <see cref="TagValue"/> is a <see cref="double"/> equal to 1.
        /// Case-insensitive. Defaults to <c>null</c> if <see cref="TagValue"/> is not recognized.
        /// </summary>
        /// <param name="value">The TagValue to convert.</param>
        /// <returns><c>true</c> if is one of the accepted values for <c>true</c>, <c>false</c> if is one of the accepted values for <c>false</c>; <c>null</c> otherwise.</returns>
        public static bool? ToBoolean(this TagValue? value)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            TagValue tagValue = value.Value;
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
                return false;
            }

            return null;
        }
    }
}
