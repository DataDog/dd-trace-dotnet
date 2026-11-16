// <copyright file="StringUtil.Culture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.Runtime.CompilerServices;
using Meziantou.Analyzer.Annotations;

// ReSharper disable once CheckNamespace - Putting this in system so the helpers are available everywhere
namespace System;

/// <summary>
/// Ordinal string comparison and culture-invariant formatting helpers, so callers don't need to pass
/// <see cref="StringComparison.Ordinal"/> or <see cref="CultureInfo.InvariantCulture"/> at every call site.
/// This file is also linked into projects that don't reference Datadog.Trace.
/// </summary>
internal static partial class StringUtil
{
    extension(string value)
    {
        /// <summary>
        /// Determines whether the string starts with <paramref name="prefix"/>, using an ordinal comparison.
        /// </summary>
        /// <param name="prefix">The string to compare</param>
        /// <returns>true if the string starts with <paramref name="prefix"/>; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWithOrdinal(string prefix) => value.StartsWith(prefix, StringComparison.Ordinal);

        /// <summary>
        /// Determines whether the string ends with <paramref name="suffix"/>, using an ordinal comparison.
        /// </summary>
        /// <param name="suffix">The string to compare</param>
        /// <returns>true if the string ends with <paramref name="suffix"/>; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWithOrdinal(string suffix) => value.EndsWith(suffix, StringComparison.Ordinal);

        /// <summary>
        /// Reports the zero-based index of the first occurrence of <paramref name="search"/> in the string,
        /// using an ordinal comparison.
        /// </summary>
        /// <param name="search">The string to seek</param>
        /// <returns>The index of <paramref name="search"/> if found; otherwise, -1.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfOrdinal(string search) => value.IndexOf(search, StringComparison.Ordinal);
    }

    extension(int)
    {
        /// <summary>
        /// Converts the string representation of a number to an <see cref="int"/>, using the invariant culture,
        /// i.e. <c>int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)</c>.
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <param name="result">The converted value, or 0 if the conversion failed</param>
        /// <returns>true if <paramref name="s"/> was converted successfully; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseInvariant(string? s, out int result) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

#if NETCOREAPP2_1_OR_GREATER
        /// <summary>
        /// Converts the span representation of a number to an <see cref="int"/>, using the invariant culture,
        /// i.e. <c>int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)</c>.
        /// </summary>
        /// <param name="s">The span to convert</param>
        /// <param name="result">The converted value, or 0 if the conversion failed</param>
        /// <returns>true if <paramref name="s"/> was converted successfully; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseInvariant(ReadOnlySpan<char> s, out int result) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#endif

        /// <summary>
        /// Converts the string representation of a number to an <see cref="int"/>, using the invariant culture,
        /// i.e. <c>int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture)</c>.
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <returns>The converted value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ParseInvariant(string s) => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    extension(long)
    {
        /// <summary>
        /// Converts the string representation of a number to a <see cref="long"/>, using the invariant culture,
        /// i.e. <c>long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)</c>.
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <param name="result">The converted value, or 0 if the conversion failed</param>
        /// <returns>true if <paramref name="s"/> was converted successfully; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseInvariant(string? s, out long result) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    extension(double)
    {
        /// <summary>
        /// Converts the string representation of a number to a <see cref="double"/>, using the invariant culture,
        /// i.e. <c>double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result)</c>.
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <param name="result">The converted value, or 0 if the conversion failed</param>
        /// <returns>true if <paramref name="s"/> was converted successfully; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseInvariant(string? s, out double result) => double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
    }

    extension<T>(T value)
        where T : IFormattable
    {
        /// <summary>
        /// Formats the value using the invariant culture, i.e. <c>value.ToString(null, CultureInfo.InvariantCulture)</c>.
        /// </summary>
        /// <returns>The value formatted using the invariant culture</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToStringInvariant() => value.ToString(null, CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats the value using the specified format and the invariant culture,
        /// i.e. <c>value.ToString(format, CultureInfo.InvariantCulture)</c>.
        /// </summary>
        /// <param name="format">The format to use</param>
        /// <returns>The value formatted using the invariant culture</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToStringInvariant(string? format) => value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Creates a string from an interpolated string, formatting every hole using the invariant culture, e.g.
    /// <c>StringUtil.Invariant($"http://{host}:{port}")</c>. This is equivalent to
    /// <c>string.Create(CultureInfo.InvariantCulture, $"...")</c>, which is only available on .NET 6+.
    /// </summary>
    /// <param name="handler">The interpolated string</param>
    /// <returns>The formatted string</returns>
    public static string Invariant([CultureInsensitive] ref InvariantInterpolatedStringHandler handler)
        => handler.ToStringAndClear();
}
