// <copyright file="ResultValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal static class ResultValue
    {
        /// <summary>
        /// Converts a deserialized Crank result value to a double. Newtonsoft deserializes JSON numbers into boxed
        /// doubles and longs (or <see cref="JValue"/>s wrapping them), so those are converted directly. Other
        /// formattable values are formatted with the invariant culture, rather than round-tripped through the
        /// culture-sensitive <see cref="object.ToString"/>.
        /// </summary>
        /// <param name="value">The deserialized value</param>
        /// <param name="result">The converted value, or 0 if the conversion failed</param>
        /// <returns>true if <paramref name="value"/> was converted successfully; otherwise, false.</returns>
        public static bool TryGetDouble(object? value, out double result)
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case long l:
                    result = l;
                    return true;
                case int i:
                    result = i;
                    return true;
                case JValue { Value: { } inner }:
                    return TryGetDouble(inner, out result);
                case IFormattable formattable:
                    return double.TryParseInvariant(formattable.ToString(format: null, CultureInfo.InvariantCulture), out result);
                default:
                    return double.TryParseInvariant(value?.ToString(), out result);
            }
        }
    }
}
