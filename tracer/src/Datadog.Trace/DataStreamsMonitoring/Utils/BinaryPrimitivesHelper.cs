// <copyright file="BinaryPrimitivesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring.Utils;

internal static class BinaryPrimitivesHelper
{
    public static void WriteUInt64LittleEndian(byte[] bytes, ulong value)
    {
#if NETCOREAPP3_1_OR_GREATER
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
#else
        if (bytes.Length < 8)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(bytes), "Destination must be at least 8 bytes long");
        }

        // write the bytes one at a time
        if (BitConverter.IsLittleEndian)
        {
            bytes[0] = unchecked((byte)(value & 0xFF));
            bytes[1] = unchecked((byte)((value >> 8) & 0xFF));
            bytes[2] = unchecked((byte)((value >> 16) & 0xFF));
            bytes[3] = unchecked((byte)((value >> 24) & 0xFF));
            bytes[4] = unchecked((byte)((value >> 32) & 0xFF));
            bytes[5] = unchecked((byte)((value >> 40) & 0xFF));
            bytes[6] = unchecked((byte)((value >> 48) & 0xFF));
            bytes[7] = unchecked((byte)((value >> 56) & 0xFF));
        }
        else
        {
            bytes[7] = unchecked((byte)(value & 0xFF));
            bytes[6] = unchecked((byte)((value >> 8) & 0xFF));
            bytes[5] = unchecked((byte)((value >> 16) & 0xFF));
            bytes[4] = unchecked((byte)((value >> 24) & 0xFF));
            bytes[3] = unchecked((byte)((value >> 32) & 0xFF));
            bytes[2] = unchecked((byte)((value >> 40) & 0xFF));
            bytes[1] = unchecked((byte)((value >> 48) & 0xFF));
            bytes[0] = unchecked((byte)((value >> 56) & 0xFF));
        }

#endif
    }

    public static ulong ReadUInt64LittleEndian(byte[] bytes)
    {
#if NETCOREAPP3_1_OR_GREATER
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);
#else
        if (bytes.Length < 8)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(bytes), "Source must be at least 8 bytes long");
        }

        // read the bytes one at a time
        if (BitConverter.IsLittleEndian)
        {
            ulong value = bytes[7];
            value = unchecked((value << 8) | bytes[6]);
            value = unchecked((value << 8) | bytes[5]);
            value = unchecked((value << 8) | bytes[4]);
            value = unchecked((value << 8) | bytes[3]);
            value = unchecked((value << 8) | bytes[2]);
            value = unchecked((value << 8) | bytes[1]);
            value = unchecked((value << 8) | bytes[0]);
            return value;
        }
        else
        {
            ulong value = bytes[0];
            value = unchecked((value << 8) | bytes[1]);
            value = unchecked((value << 8) | bytes[2]);
            value = unchecked((value << 8) | bytes[3]);
            value = unchecked((value << 8) | bytes[4]);
            value = unchecked((value << 8) | bytes[5]);
            value = unchecked((value << 8) | bytes[6]);
            value = unchecked((value << 8) | bytes[7]);
            return value;
        }
#endif
    }

    // ReverseEndianness() from .NET repository.
    // https://github.com/dotnet/runtime/blob/f8ab5554091d2124dffa6002650d9a5ca0a7fba3/src/libraries/System.Private.CoreLib/src/System/Buffers/Binary/BinaryPrimitives.ReverseEndianness.cs

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Reverses a primitive value - performs an endianness swap
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReverseEndianness(ulong value)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value);
    }

    /// <summary>
    /// Reverses a primitive value - performs an endianness swap
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReverseEndianness(int value)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value);
    }

#else

    /// <summary>
    /// Reverses a primitive value - performs an endianness swap
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReverseEndianness(ulong value)
    {
        // Operations on 32-bit values have higher throughput than
        // operations on 64-bit values, so decompose.
        return ((ulong)ReverseEndianness((uint)value) << 32) + ReverseEndianness((uint)(value >> 32));
    }

    /// <summary>
    /// Reverses a primitive value - performs an endianness swap
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReverseEndianness(int value) => (int)ReverseEndianness((uint)value);

    /// <summary>
    /// Reverses a primitive value - performs an endianness swap
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReverseEndianness(uint value)
    {
        // This takes advantage of the fact that the JIT can detect
        // ROL32 / ROR32 patterns and output the correct intrinsic.
        //
        // Input: value = [ ww xx yy zz ]
        //
        // First line generates : [ ww xx yy zz ]
        //                      & [ 00 FF 00 FF ]
        //                      = [ 00 xx 00 zz ]
        //             ROR32(8) = [ zz 00 xx 00 ]
        //
        // Second line generates: [ ww xx yy zz ]
        //                      & [ FF 00 FF 00 ]
        //                      = [ ww 00 yy 00 ]
        //             ROL32(8) = [ 00 yy 00 ww ]
        //
        //                (sum) = [ zz yy xx ww ]
        //
        // Testing shows that throughput increases if the AND
        // is performed before the ROL / ROR.

        return RotateRight(value & 0x00FF00FFu, 8) // xx zz
              +
               RotateLeft(value & 0xFF00FF00u, 8); // ww yy
    }

    // RotateRight() and RotateLeft() from .NET repository.
    // https://github.com/dotnet/runtime/blob/f8ab5554091d2124dffa6002650d9a5ca0a7fba3/src/libraries/System.Private.CoreLib/src/System/Numerics/BitOperations.cs

    /// <summary>
    /// Rotates the specified value right by the specified number of bits.
    /// Similar in behavior to the x86 instruction ROR.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="offset">The number of bits to rotate by.
    /// Any value outside the range [0..31] is treated as congruent mod 32.</param>
    /// <returns>The rotated value.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateRight(uint value, int offset) => (value >> offset) | (value << (32 - offset));

    /// <summary>
    /// Rotates the specified value left by the specified number of bits.
    /// Similar in behavior to the x86 instruction ROL.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="offset">The number of bits to rotate by.
    /// Any value outside the range [0..31] is treated as congruent mod 32.</param>
    /// <returns>The rotated value.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));
#endif
}
