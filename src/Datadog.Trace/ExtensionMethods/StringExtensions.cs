using System;
using System.Globalization;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class StringExtensions
    {
        public static ulong? TryParseUInt64(this string value)
        {
            return ulong.TryParse(value, out var result)
                       ? result
                       : default;
        }

        public static ulong? TryParseUInt64(this string value, NumberStyles style, IFormatProvider provider)
        {
            return ulong.TryParse(value, style, provider, out var result)
                       ? result
                       : default;
        }
    }
}
