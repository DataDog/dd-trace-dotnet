// <copyright file="SpanTagHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging;

internal static class SpanTagHelper
{
    internal static bool IsValidTagName(
        string value,
        [NotNullWhen(returnValue: true)] out string? trimmedValue)
    {
        trimmedValue = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedTemp = value.Trim();

        if (!char.IsLetter(trimmedTemp[0]) || trimmedTemp.Length > 200)
        {
            return false;
        }

        trimmedValue = trimmedTemp;
        return true;
    }

    /// <summary>
    /// Datadog tag name requirements:
    /// 1. Tag must start with a letter.
    /// 2. Tag cannot exceed 200 characters.
    /// 3. If the first two requirements are met, then valid characters will be retained
    ///    while all other characters will be converted to underscores. Valid characters include:
    ///    - Alphanumerics
    ///    - Underscores
    ///    - Minuses
    ///    - Colons
    ///    - Slashes
    /// 4. Optionally, spaces can be replaced by underscores.
    /// Note: This method will trim leading/trailing whitespace before checking the requirements.
    /// </summary>
    /// <param name="value">Input string to convert into a tag name.</param>
    /// <param name="normalizeSpaces">
    ///     True to replace spaces with underscores.
    ///     Controlled by feature flag <c>DD_TRACE_HEADER_TAG_NORMALIZATION_FIX_ENABLED</c>.
    /// </param>
    /// <param name="normalizedTagName">If the method returns <c>true</c>, the normalized tag name. Otherwise, <c>null</c>.</param>
    /// <returns>Returns a value indicating whether the conversion was successful.</returns>
    internal static bool TryNormalizeTagName(
        string value,
        bool normalizeSpaces,
        [NotNullWhen(returnValue: true)] out string? normalizedTagName)
    {
        normalizedTagName = null;

        if (!IsValidTagName(value, out var trimmedValue))
        {
            return false;
        }

        var sb = StringBuilderCache.Acquire(trimmedValue.Length);
        sb.Append(trimmedValue.ToLowerInvariant());

        for (var x = 0; x < sb.Length; x++)
        {
            switch (sb[x])
            {
                case (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '/' or '-':
                    continue;
                case ' ' when !normalizeSpaces:
                    continue;
                default:
                    sb[x] = '_';
                    break;
            }
        }

        normalizedTagName = StringBuilderCache.GetStringAndRelease(sb);
        return true;
    }
}
