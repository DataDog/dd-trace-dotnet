// <copyright file="StringUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETCOREAPP
using System.Collections.Generic;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace - Putting this in system so we can do simple drop-in replacement
namespace System;

/// <summary>
/// Provides some simple wrappers around string operations, primarily to provide nullable annotations for frameworks
/// that don't support them i.e. .NET FX, .NET Standard 2.0. Also polyfills the string char overloads
/// (<c>StartsWith(char)</c>, <c>EndsWith(char)</c> and <c>string.Join(char, ...)</c>) on those frameworks.
/// </summary>
internal static partial class StringUtil
{
#if !NETCOREAPP
    // Polyfills for the string char overloads added in .NET Core 2.0, so callers can use them on every
    // target framework. On .NET Core the built-in overloads are used instead.

    extension(string value)
    {
        /// <summary>
        /// Polyfill for <c>string.StartsWith(char)</c> on .NET Framework and .NET Standard 2.0.
        /// </summary>
        /// <param name="c">The character to compare</param>
        /// <returns>true if the string starts with <paramref name="c"/>; otherwise, false.</returns>
        public bool StartsWith(char c) => value.Length != 0 && value[0] == c;

        /// <summary>
        /// Polyfill for <c>string.EndsWith(char)</c> on .NET Framework and .NET Standard 2.0.
        /// </summary>
        /// <param name="c">The character to compare</param>
        /// <returns>true if the string ends with <paramref name="c"/>; otherwise, false.</returns>
        public bool EndsWith(char c) => value.Length != 0 && value[value.Length - 1] == c;
    }

    extension(string)
    {
        /// <summary>
        /// Polyfill for <c>string.Join(char, params string[])</c> on .NET Framework and .NET Standard 2.0.
        /// </summary>
        /// <param name="separator">The separator character</param>
        /// <param name="value">The values to join</param>
        /// <returns>The joined string</returns>
        public static string Join(char separator, params string?[] value) => string.Join(SeparatorToString(separator), value);

        /// <summary>
        /// Polyfill for <c>string.Join&lt;T&gt;(char, IEnumerable&lt;T&gt;)</c> on .NET Framework and .NET Standard 2.0.
        /// </summary>
        /// <param name="separator">The separator character</param>
        /// <param name="values">The values to join</param>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <returns>The joined string</returns>
        public static string Join<T>(char separator, IEnumerable<T> values) => string.Join(SeparatorToString(separator), values);
    }

    // Returns interned literals for the separators we use, so .NET Framework and .NET Standard 2.0 don't allocate a string per call
    private static string SeparatorToString(char separator) => separator switch
    {
        ',' => ",",
        ';' => ";",
        '|' => "|",
        ':' => ":",
        ' ' => " ",
        '\n' => "\n",
        _ => separator.ToString(),
    };
#endif

    /// <summary>
    /// Indicates whether the specified string is null or an empty string ("").
    /// A nullable-annotation wrapper for <see cref="string.IsNullOrEmpty"/>
    /// that works on .NET Framework and .NET Standard 2.0.
    /// </summary>
    /// <param name="value">The string to test</param>
    /// <returns>true if the value parameter is null or an empty string (""); otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty([NotNullWhen(false)] string? value)
        => string.IsNullOrEmpty(value);

    /// <summary>
    /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
    /// A nullable-annotation wrapper for <see cref="string.IsNullOrWhiteSpace"/>
    /// that works on .NET Framework and .NET Standard 2.0.
    /// </summary>
    /// <param name="value">The string to test</param>
    /// <returns>true if the value parameter is null or Empty, or if value consists exclusively of white-space characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? value)
        => string.IsNullOrWhiteSpace(value);

#if NETFRAMEWORK
    /// <summary>
    /// Non-allocating alternative to <paramref name="value"/>.ToUpperInvariant(). May return the same
    /// instance (instead of allocating) when no character in <paramref name="value"/> actually needs to change.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? ToUpperInvariant(string? value)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var digit in value)
        {
            if (digit > '\x7F' || char.IsBetween(digit, 'a', 'z'))
            {
                // Note: we don't call string.ToUpperInvariant() here to avoid potential accidental recursion
                return CultureInfo.InvariantCulture.TextInfo.ToUpper(value);
            }
        }

        return value;
    }

    /// <summary>
    /// Non-allocating alternative to <paramref name="value"/>.ToLowerInvariant(). May return the same
    /// instance (instead of allocating) when no character in <paramref name="value"/> actually needs to change.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? ToLowerInvariant(string? value)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var digit in value)
        {
            if (digit > '\x7F' || char.IsBetween(digit, 'A', 'Z'))
            {
                // Note: we don't call string.ToLowerInvariant() here to avoid potential accidental recursion
                return CultureInfo.InvariantCulture.TextInfo.ToLower(value);
            }
        }

        return value;
    }
#else
    [return: NotNullIfNotNull(nameof(value))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? ToUpperInvariant(string? value)
        => value?.ToUpperInvariant();

    [return: NotNullIfNotNull(nameof(value))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? ToLowerInvariant(string? value)
        => value?.ToLowerInvariant();
#endif
}
