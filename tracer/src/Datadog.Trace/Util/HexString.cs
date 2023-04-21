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
    /// <summary>
    /// Thread static buffer used to avoid allocating a new byte array on each conversion.
    /// </summary>
    [ThreadStatic]
    private static byte[]? _buffer;
#endif

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReverseIfLittleEndian(ulong value)
    {
        return BitConverter.IsLittleEndian ? BinaryPrimitivesHelper.ReverseEndianness(value) : value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TraceId ReverseIfLittleEndian(TraceId value)
    {
        if (BitConverter.IsLittleEndian)
        {
            var upper = BinaryPrimitivesHelper.ReverseEndianness(value.Upper);
            var lower = BinaryPrimitivesHelper.ReverseEndianness(value.Lower);

            // We're intentionally not flipping upper/lower around. This struct doesn't act like a UInt128,
            // where the order of the field needs to be reversed. Instead, Upper is always Upper
            // and Lower is always Lower. We may need to revisit this if we ever use the UInt128 added in .NET 7.
            return new TraceId(upper, lower);
        }

        return value;
    }

#if !NETCOREAPP3_1_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArraySegment<byte> GetBuffer(int size)
    {
        if (_buffer == null || _buffer.Length < size)
        {
            _buffer = new byte[size];
        }

        return new ArraySegment<byte>(_buffer, 0, size);
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
    {
        if (chars.Length < bytes.Length * 2)
        {
            ThrowHelper.ThrowArgumentException("Target buffer is too small for the provided bytes.", nameof(chars));
        }
#else
    public static void ToHexChars(ArraySegment<byte> bytes, char[] chars, bool lowerCase = true)
    {
        if (chars.Length < bytes.Count * 2)
        {
            ThrowHelper.ThrowArgumentException("Target buffer is too small for the provided bytes.", nameof(chars));
        }
#endif

        var casing = lowerCase ? HexConverter.Casing.Lower : HexConverter.Casing.Upper;
        HexConverter.EncodeToUtf16(bytes, chars, casing);
    }

    /// <summary>
    /// Converts the specified bytes into a hexadecimal string.
    /// </summary>
    [Pure]
#if NETCOREAPP3_1_OR_GREATER
    public static string ToHexString(ReadOnlySpan<byte> bytes, bool lowerCase = true)
    {
        var casing = lowerCase ? HexConverter.Casing.Lower : HexConverter.Casing.Upper;
        return HexConverter.ToString(bytes, casing);
    }
#else
    public static string ToHexString(byte[] bytes, bool lowerCase = true)
    {
        return ToHexString(new ArraySegment<byte>(bytes, offset: 0, count: bytes.Length), lowerCase);
    }

    public static string ToHexString(ArraySegment<byte> bytes, bool lowerCase = true)
    {
        var casing = lowerCase ? HexConverter.Casing.Lower : HexConverter.Casing.Upper;
        return HexConverter.ToString(bytes, casing);
    }
#endif

    /// <summary>
    /// Converts the specified <see cref="ulong"/> value into a hexadecimal string.
    /// </summary>
    [Pure]
    public static string ToHexString(ulong value, bool lowerCase = true)
    {
        if (value == 0)
        {
            return "0000000000000000";
        }

        value = ReverseIfLittleEndian(value);

#if NETCOREAPP3_1_OR_GREATER
        Span<byte> bytes = stackalloc byte[8];
        System.Runtime.InteropServices.MemoryMarshal.Write(bytes, ref value);
#else
        var bytes = BitConverter.GetBytes(value);
#endif

        return ToHexString(bytes, lowerCase);
    }

    /// <summary>
    /// Converts the specified <see cref="TraceId"/> value into a hexadecimal string using network byte order
    /// (aka big endian), with the most significant byte first.
    /// </summary>
    [Pure]
    public static string ToHexString(TraceId value, bool pad16To32 = true, bool lowerCase = true)
    {
        if (!pad16To32 && value.Upper == 0)
        {
            // this trace id fits in 16 hex characters and padding to 32 characters was not requested
            return ToHexString(value.Lower);
        }

        if (value == TraceId.Zero)
        {
            return "00000000000000000000000000000000";
        }

        var (upper, lower) = ReverseIfLittleEndian(value);

#if NETCOREAPP3_1_OR_GREATER
        // NOTE: don't use MemoryMarshal.Write() with the entire TraceId because .NET will
        // flip upper/lower around on little-endian architectures. Instead, call MemoryMarshal.Write()
        // for each field so we can control the order ourselves. Trace id hex strings should
        // always use network byte order, aka big endian.
        Span<byte> bytes = stackalloc byte[TraceId.Size];
        System.Runtime.InteropServices.MemoryMarshal.Write(bytes, ref upper);
        System.Runtime.InteropServices.MemoryMarshal.Write(bytes[sizeof(ulong)..], ref lower);

        return ToHexString(bytes, lowerCase);
#else

        var bytes = GetBuffer(TraceId.Size);
        BitConverter.GetBytes(upper).CopyTo(bytes.Array, bytes.Offset);
        BitConverter.GetBytes(lower).CopyTo(bytes.Array, bytes.Offset + sizeof(ulong));

        return ToHexString(bytes, lowerCase);
#endif
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
        return TryParseBytes(chars, new ArraySegment<byte>(bytes, offset: 0, count: bytes.Length));
    }

    /// <summary>
    /// Tries to parse the specified hexadecimal string into the specified byte array.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain an even number of characters, two for each output byte.</param>
    /// <param name="bytes">The buffer to write the parsed bytes into. Must be half in length as <paramref name="chars"/></param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
    public static bool TryParseBytes(string chars, ArraySegment<byte> bytes)
    {
        // this overload exists in NETCOREAPP3_1_OR_GREATER so we can catch null strings,
        // otherwise we can't distinguish them from empty ReadOnlySpan<char>
        if (chars == null! || chars.Length != bytes.Count * 2)
        {
            return false;
        }

        return HexConverter.TryDecodeFromUtf16(chars, bytes);
    }

    /// <summary>
    /// Tries to parse the specified hexadecimal string into a <see cref="TraceId"/> value.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse. Must contain exactly 16 or 32 characters, so it may need to be left-padded with zeros.</param>
    /// <param name="value">The TraceId parsed out of the hexadecimal string.</param>
    /// <returns><c>true</c> if it parsed successfully, <c>false</c> otherwise.</returns>
#if NETCOREAPP3_1_OR_GREATER
    [Pure]
    public static bool TryParseTraceId(ReadOnlySpan<char> chars, out TraceId value)
    {
        value = default;
        ulong lower;

        if (chars.Length == 16)
        {
            // allow parsing a 64-bit trace id
            var success = TryParseUInt64(chars, out lower);
            value = new TraceId(0, lower);
            return success;
        }

        if (chars.Length != 32)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[16];

        if (!TryParseBytes(chars, bytes))
        {
            return false;
        }

        var upper = BitConverter.ToUInt64(bytes);
        lower = BitConverter.ToUInt64(bytes[8..]);
        value = ReverseIfLittleEndian(new TraceId(upper, lower));
        return true;
    }
#else
    [Pure]
    public static bool TryParseTraceId(string chars, out TraceId value)
    {
        value = default;
        ulong lower;

        if (chars == null!)
        {
            return false;
        }

        if (chars.Length == 16)
        {
            // allow parsing a 64-bit trace id
            var success = TryParseUInt64(chars, out lower);
            value = new TraceId(0, lower);
            return success;
        }

        if (chars.Length != 32)
        {
            return false;
        }

        var bytes = GetBuffer(TraceId.Size);

        if (!TryParseBytes(chars, bytes))
        {
            return false;
        }

        var upper = BitConverter.ToUInt64(bytes.Array!, bytes.Offset);
        lower = BitConverter.ToUInt64(bytes.Array!, bytes.Offset + sizeof(ulong));
        value = ReverseIfLittleEndian(new TraceId(upper, lower));
        return true;
    }
#endif

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

        var bytes = GetBuffer(sizeof(ulong));

        if (!TryParseBytes(chars, bytes))
        {
            return false;
        }

        var result = BitConverter.ToUInt64(bytes.Array!, bytes.Offset);
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

        var bytes = GetBuffer(size: 1);

        if (TryParseBytes(chars, bytes))
        {
            // no need to reverse endianness on a single byte
            value = bytes.Array![bytes.Offset];
            return true;
        }

        return false;
    }
#endif
}
