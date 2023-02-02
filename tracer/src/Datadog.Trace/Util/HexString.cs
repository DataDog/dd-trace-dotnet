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

#if !NETCOREAPP3_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetBuffer8()
    {
        return _buffer8 ??= new byte[8];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetBuffer1()
    {
        return _buffer1 ??= new byte[1];
    }
#endif

    /// <summary>
    /// Converts the specified <see cref="ulong"/> value into hexadecimal characters, two for each byte,
    /// and places the result into the specified buffer.
    /// </summary>
    /// <param name="bytes">The bytes to convert into hexadecimal characters.</param>
    /// <param name="chars">The buffer to place the output into.</param>
    /// <param name="lowerCase"><c>true</c> to generate lower-case characters, <c>false</c> otherwise.</param>
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

    /// <summary>
    /// Converts the specified bytes into a hexadecimal string.
    /// </summary>
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

    /// <summary>
    /// Converts the specified <see cref="ulong"/> value into a hexadecimal string.
    /// </summary>
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
    /// <summary>
    /// Tries to parse the specified hexadecimal string into the specified byte array.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain an even number of characters, two for each output byte.</param>
    /// <param name="bytes">The buffer to write the parsed bytes into. Must be half in length as <paramref name="chars"/></param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
    public static bool TryParseBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        if (chars.Length != bytes.Length * 2)
        {
            return false;
        }

        return HexConverter.TryDecodeFromUtf16(chars, bytes);
    }
#endif

    /// <summary>
    /// Tries to parse the specified hexadecimal string into the specified byte array.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain an even number of characters, two for each output byte.</param>
    /// <param name="bytes">The buffer to write the parsed bytes into. Must be half in length as <paramref name="chars"/></param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
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

    /// <summary>
    /// Tries to parse the specified hexadecimal string into a <see cref="ulong"/> value.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain exactly 16 characters, so it may need to be left-padded with zeros.</param>
    /// <param name="value">The integer value parsed out of the hexadecimal string.</param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
#if NETCOREAPP3_1_OR_GREATER
    [Pure]
    public static bool TryParseUInt64(ReadOnlySpan<char> chars, out ulong value)
    {
        value = default;

        if (chars.Length != 16)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[8];

        if (!TryParseBytes(chars, bytes))
        {
            return false;
        }

        var result = BitConverter.ToUInt64(bytes);
        value = ReverseIfLittleEndian(result);
        return true;
    }
#else
    [Pure]
    public static bool TryParseUInt64(string chars, out ulong value)
    {
        value = default;

        if (chars == null! || chars.Length != 16)
        {
            return false;
        }

        var bytes = GetBuffer8();

        if (!TryParseBytes(chars, bytes))
        {
            return false;
        }

        var result = BitConverter.ToUInt64(bytes, 0);
        value = ReverseIfLittleEndian(result);
        return true;
    }
#endif

    /// <summary>
    /// Tries to parse the specified hexadecimal string into a <see cref="byte"/> value.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain exactly 2 characters, so it may need to be left-padded with zeros.</param>
    /// <param name="value">The integer value parsed out of the hexadecimal string.</param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
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

        if (chars == null! || chars.Length != 2)
        {
            return false;
        }

        var bytes = GetBuffer1();

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
