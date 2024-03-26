// <copyright file="StringHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

/// <summary>
/// Polyfills for working with <see cref="ReadOnlySpan{T}"/> methods that
/// are only available in .NET Core 3.1 and later.
/// </summary>
internal static class StringHelpers
{
#if NETCOREAPP3_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1) => string.Concat(str0, str1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2) => string.Concat(str0, str1, str2);

#else
    private const int MaxStackLimit = 256;

    public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
    {
        int length = checked(str0.Length + str1.Length);
        if (length == 0)
        {
            return string.Empty;
        }

        // If the string is too big, fallback to string builder
        if (length > MaxStackLimit)
        {
            return GetWithStringBuilder(str0, str1);
        }

        // we're small enough to stackalloc
        Span<char> buffer = (stackalloc char[MaxStackLimit]).Slice(0, length: length);
        str0.CopyTo(buffer);
        str1.CopyTo(buffer.Slice(str0.Length));

        return new string(buffer);
    }

    public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2)
    {
        int length = checked(str0.Length + str1.Length + str2.Length);
        if (length == 0)
        {
            return string.Empty;
        }

        // If the string is too big, fallback to string builder
        if (length > MaxStackLimit)
        {
            return GetWithStringBuilder(str0, str1, str2);
        }

        // we're small enough to stackalloc
        Span<char> fullBuffer = (stackalloc char[MaxStackLimit]).Slice(0, length: length);
        Span<char> buffer = fullBuffer;

        str0.CopyTo(buffer);
        buffer = buffer.Slice(str0.Length);

        str1.CopyTo(buffer);
        buffer = buffer.Slice(str1.Length);

        str2.CopyTo(buffer);
        return new string(fullBuffer);
    }

    private static string GetWithStringBuilder(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
    {
        var sb = StringBuilderCache.Acquire(str0.Length + str1.Length);
        sb.Append(str0).Append(str1);
        return StringBuilderCache.GetStringAndRelease(sb);
    }

    private static string GetWithStringBuilder(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2)
    {
        var sb = StringBuilderCache.Acquire(str0.Length + str1.Length + str2.Length);
        sb.Append(str0).Append(str1).Append(str2);
        return StringBuilderCache.GetStringAndRelease(sb);
    }
#endif
}
#endif
