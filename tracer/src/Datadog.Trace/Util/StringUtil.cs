// <copyright file="StringUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
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
}
