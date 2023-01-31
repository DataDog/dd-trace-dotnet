// <copyright file="HexString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Datadog.Trace.DataStreamsMonitoring.Utils;

namespace Datadog.Trace.Util;

/// <summary>
/// Utility class with helpers method that wrap HexConverter to convert values to and from hexadecimal strings.
/// </summary>
internal static class HexString
{
#if !NETCOREAPP3_1_OR_GREATER
    [ThreadStatic]
    private static byte[]? _buffer8; // always 8 bytes for ulong

    [ThreadStatic]
    private static byte[]? _buffer1; // always 1 byte
#endif

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReverseIfLittleEndian(ulong value)
    {
        return BitConverter.IsLittleEndian ? BinaryPrimitivesHelper.ReverseEndianness(value) : value;
    }

#if NETCOREAPP3_1_OR_GREATER
    public static void ToHexChars(ReadOnlySpan<byte> bytes, Span<char> chars, bool lowerCase = true)
#else
    public static void ToHexChars(byte[] bytes, char[] chars, bool lowerCase = true)
#endif
    {
        if (chars.Length < bytes.Length * 2)
        {
            ThrowHelper.ThrowArgumentException("Target buffer is too small for the provided bytes.", nameof(chars));
        }

        var casing = lowerCase ? HexConverter.Casing.Lower : HexConverter.Casing.Upper;
        HexConverter.EncodeToUtf16(bytes, chars, casing);
    }

    [Pure]
#if NETCOREAPP3_1_OR_GREATER
    public static string ToHexString(ReadOnlySpan<byte> bytes, bool lowerCase = true)
#else
    public static string ToHexString(byte[] bytes, bool lowerCase = true)
#endif
    {
        var casing = lowerCase ? HexConverter.Casing.Lower : HexConverter.Casing.Upper;
        return HexConverter.ToString(bytes, casing);
    }

    [Pure]
    public static string ToHexString(ulong value, bool lowerCase = true)
    {
        value = ReverseIfLittleEndian(value);

#if NETCOREAPP3_1_OR_GREATER
        Span<byte> bytes = stackalloc byte[8];
        System.Runtime.InteropServices.MemoryMarshal.Write(bytes, ref value);
#else
        var bytes = BitConverter.GetBytes(value);
#endif

        return ToHexString(bytes, lowerCase);
    }

#if NETCOREAPP3_1_OR_GREATER
    public static bool TryParseBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        if (chars.Length != bytes.Length * 2)
        {
            return false;
        }

        return HexConverter.TryDecodeFromUtf16(chars, bytes);
    }
#endif

    public static bool TryParseBytes(string chars, byte[] bytes)
    {
        // this overload exists in NETCOREAPP3_1_OR_GREATER so we can catch null strings,
        // otherwise we can't distinguish them from empty ReadOnlySpan<char>
        if (chars == null! || chars.Length != bytes.Length * 2)
        {
            return false;
        }

        return HexConverter.TryDecodeFromUtf16(chars, bytes);
    }

#if NETCOREAPP3_1_OR_GREATER
    [Pure]
    public static bool TryParseUInt64(ReadOnlySpan<char> chars, out ulong value)
    {
        value = default;

        if (chars.Length != 16)
        {
            return false;
        }

        // benchmarks show that UInt64.TryParse() is faster than
        // HexString.TryParseBytes() + BitConverter.ToUInt64() on .NET Core 3.1+
        return ulong.TryParse(
            chars,
            System.Globalization.NumberStyles.AllowHexSpecifier,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }
#else
    [Pure]
    public static bool TryParseUInt64(string chars, out ulong value)
    {
        value = default;

        if (chars?.Length != 16)
        {
            return false;
        }

        var bytes = _buffer8 ??= new byte[8];
        ulong result;

        if (TryParseBytes(chars, bytes))
        {
            result = BitConverter.ToUInt64(bytes, 0);
        }
        else
        {
            return false;
        }

        value = ReverseIfLittleEndian(result);
        return true;
    }
#endif

    [Pure]
#if NETCOREAPP3_1_OR_GREATER
    public static bool TryParseByte(ReadOnlySpan<char> chars, out byte value)
    {
        value = default;

        if (chars.Length != 2)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[1];

        if (TryParseBytes(chars, bytes))
        {
            // no need to reverse endianness on a single byte
            value = bytes[0];
            return true;
        }

        return false;
    }
#else
    public static bool TryParseByte(string chars, out byte value)
    {
        value = default;

        if (chars?.Length != 2)
        {
            return false;
        }

        var bytes = _buffer1 ??= new byte[1];

        if (TryParseBytes(chars, bytes))
        {
            // no need to reverse endianness on a single byte
            value = bytes[0];
            return true;
        }

        return false;
    }
#endif
}
