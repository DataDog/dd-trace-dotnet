// <copyright file="PathStringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.Util.Http;

/// <summary>
/// PathString extensions using the StringBuilderCache based on:
/// https://github.com/dotnet/aspnetcore/blob/v6.0.20/src/Http/Http.Abstractions/src/PathString.cs
/// and
/// https://github.com/dotnet/aspnetcore/blob/v6.0.20/src/Http/Http.Abstractions/src/Internal/PathStringHelper.cs
/// </summary>
internal static class PathStringExtensions
{
    /// <summary>
    /// Provides the path string escaped in a way which is correct for combining into the URI representation.
    /// </summary>
    public static void FillBufferWithUriComponent(this PathString pathString, StringBuilder buffer)
    {
        var value = pathString.Value;
        var i = 0;
        for (; i < value.Length; i++)
        {
            if (!PathStringHelper.IsValidPathChar(value[i]) || PathStringHelper.IsPercentEncodedChar(value, i))
            {
                break;
            }
        }

        if (i < value.Length)
        {
            FillBufferWithEscapedUriComponent(value, i, buffer);
            return;
        }

        buffer.Append(value);
    }

    private static void FillBufferWithEscapedUriComponent(string value, int i, StringBuilder buffer)
    {
        var start = 0;
        var count = i;
        var requiresEscaping = false;

        while (i < value.Length)
        {
            var isPercentEncodedChar = PathStringHelper.IsPercentEncodedChar(value, i);
            if (PathStringHelper.IsValidPathChar(value[i]) || isPercentEncodedChar)
            {
                if (requiresEscaping)
                {
                    // the current segment requires escape
                    buffer.Append(Uri.EscapeDataString(value.Substring(start, count)));

                    requiresEscaping = false;
                    start = i;
                    count = 0;
                }

                if (isPercentEncodedChar)
                {
                    count += 3;
                    i += 3;
                }
                else
                {
                    count++;
                    i++;
                }
            }
            else
            {
                if (!requiresEscaping)
                {
                    // the current segment doesn't require escape
                    buffer.Append(value, start, count);

                    requiresEscaping = true;
                    start = i;
                    count = 0;
                }

                count++;
                i++;
            }
        }

        if (count == value.Length && !requiresEscaping)
        {
            return;
        }

        if (count > 0)
        {
            if (requiresEscaping)
            {
                buffer.Append(Uri.EscapeDataString(value.Substring(start, count)));
            }
            else
            {
                buffer.Append(value, start, count);
            }
        }
    }

    internal static class PathStringHelper
    {
        // uint[] bits uses 1 cache line (Array info + 16 bytes)
        // bool[] would use 3 cache lines (Array info + 128 bytes)
        // So we use 128 bits rather than 128 bytes/bools
        private static readonly uint[] ValidPathChars =
        {
            0b_0000_0000__0000_0000__0000_0000__0000_0000, // 0x00 - 0x1F
            0b_0010_1111__1111_1111__1111_1111__1101_0010, // 0x20 - 0x3F
            0b_1000_0111__1111_1111__1111_1111__1111_1111, // 0x40 - 0x5F
            0b_0100_0111__1111_1111__1111_1111__1111_1110, // 0x60 - 0x7F
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPathChar(char c)
        {
            // Use local array and uint .Length compare to elide the bounds check on array access
            var validChars = ValidPathChars;
            var i = (int)c;

            // Array is in chunks of 32 bits, so get offset by dividing by 32
            var offset = i >> 5; // i / 32;
            // Significant bit position is the remainder of the above calc; i % 32 => i & 31
            var significantBit = 1u << (i & 31);

            // Check offset in bounds and check if significant bit set
            return (uint)offset < (uint)validChars.Length &&
                   ((validChars[offset] & significantBit) != 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPercentEncodedChar(string str, int index)
        {
            var len = (uint)str.Length;
            if (str[index] == '%' && index < len - 2)
            {
                return AreFollowingTwoCharsHex(str, index);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool AreFollowingTwoCharsHex(string str, int index)
        {
            var c1 = str[index + 1];
            var c2 = str[index + 2];
            return IsHexadecimalChar(c1) && IsHexadecimalChar(c2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexadecimalChar(char c)
        {
            // Between 0 - 9 or uppercased between A - F
            return (uint)(c - '0') <= 9 || (uint)((c & ~0x20) - 'A') <= ('F' - 'A');
        }
    }
}

#endif
