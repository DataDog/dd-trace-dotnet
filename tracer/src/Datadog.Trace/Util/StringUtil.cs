// <copyright file="StringUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace - Putting this in system so we can do simple drop-in replacement
namespace System;

/// <summary>
/// Provides some simple wrappers around string operations, primarily to provide nullable annotations for frameworks
/// that don't support them i.e. .NET FX, .NET Standard 2.0.
/// </summary>
internal static class StringUtil
{
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

        foreach (var c in value)
        {
            if (c > '\x7F' || (uint)(c - 'a') <= 'z' - 'a')
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

        foreach (var c in value)
        {
            if (c > '\x7F' || (uint)(c - 'A') <= 'Z' - 'A')
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
