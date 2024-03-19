// <copyright file="FileBitmap.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
#if NET6_0
using System.Runtime.Intrinsics.Arm;
#endif
using System.Text;
using Datadog.Trace.Util;
using Unsafe = Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe;

namespace Datadog.Trace.Ci.Coverage.Util;

/// <summary>
/// Represents a memory-efficient, modifiable file bitmap, optimized for high performance using unsafe code and SIMD instructions when available.
/// </summary>
internal unsafe ref struct FileBitmap
{
    /// <summary>
    /// Size of the bitmap in bytes.
    /// </summary>
    private readonly int _size;

    /// <summary>
    /// Handle to the pinned array if the bitmap is created from a managed byte array.
    /// </summary>
    private readonly GCHandle? _handle;

    /// <summary>
    /// Pointer to the start of the bitmap data in memory.
    /// </summary>
    private readonly byte* _bitmap;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBitmap"/> struct with a specified buffer pointer and size.
    /// </summary>
    /// <param name="buffer">Pointer to the buffer.</param>
    /// <param name="size">Size of the buffer.</param>
    public FileBitmap(byte* buffer, int size)
    {
        _handle = null;
        _bitmap = buffer;
        if (buffer is null)
        {
            _size = 0;
        }
        else
        {
            _size = size;
            Unsafe.InitBlockUnaligned(buffer, 0, (uint)_size);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBitmap"/> struct from a byte array.
    /// </summary>
    /// <param name="bitmapArray">The byte array to read or create the bitmap from.</param>
    public FileBitmap(byte[] bitmapArray)
    {
        if (bitmapArray is null)
        {
            ThrowHelper.ThrowArgumentException("Bitmap array source is null", nameof(bitmapArray));
        }

        _size = bitmapArray.Length;
        if (_size == 0)
        {
            _handle = null;
            _bitmap = null;
        }
        else
        {
            _handle = GCHandle.Alloc(bitmapArray, GCHandleType.Pinned);
            _bitmap = (byte*)Unsafe.AsPointer(ref bitmapArray.FastGetReference(0));
        }
    }

    /// <summary>
    /// Gets the size of the bitmap.
    /// </summary>
    public int Size => _size;

    /// <summary>
    /// Performs a bitwise OR operation on two <see cref="FileBitmap"/> instances.
    /// </summary>
    /// <param name="fileBitmapA">The first operand.</param>
    /// <param name="fileBitmapB">The second operand.</param>
    /// <returns>The result of the bitwise OR operation.</returns>
    public static FileBitmap operator |(FileBitmap fileBitmapA, FileBitmap fileBitmapB)
        => Or(fileBitmapA, fileBitmapB, false);

    /// <summary>
    /// Performs a bitwise AND operation on two <see cref="FileBitmap"/> instances.
    /// </summary>
    /// <param name="fileBitmapA">The first operand.</param>
    /// <param name="fileBitmapB">The second operand.</param>
    /// <returns>The result of the bitwise AND operation.</returns>
    public static FileBitmap operator &(FileBitmap fileBitmapA, FileBitmap fileBitmapB)
        => And(fileBitmapA, fileBitmapB, false);

    /// <summary>
    /// Performs a bitwise NOT operation on a <see cref="FileBitmap"/> instance.
    /// </summary>
    /// <param name="fileBitmap">The operand.</param>
    /// <returns>The result of the bitwise NOT operation.</returns>
    public static FileBitmap operator ~(FileBitmap fileBitmap)
        => Not(fileBitmap, false);

    /// <summary>
    /// Performs a bitwise OR operation on two <see cref="FileBitmap"/> instances.
    /// </summary>
    /// <param name="fileBitmapA">The first operand.</param>
    /// <param name="fileBitmapB">The second operand.</param>
    /// <param name="reuseBufferFromBitmapA">True if the buffer of the bitmap 'a' should be used for the response operation</param>
    /// <returns>The result of the bitwise OR operation.</returns>
    public static FileBitmap Or(FileBitmap fileBitmapA, FileBitmap fileBitmapB, bool reuseBufferFromBitmapA)
    {
        var minSize = fileBitmapA._size;
        var maxSize = fileBitmapB._size;
        var resBitmap = fileBitmapA;

        if (!reuseBufferFromBitmapA)
        {
            // Determine the minimum and maximum sizes of the two bitmaps to handle bitmaps of different lengths
            minSize = Math.Min(fileBitmapA._size, fileBitmapB._size);
            maxSize = Math.Max(fileBitmapA._size, fileBitmapB._size);

            // Create a new bitmap to store the result of the OR operation, initializing it to the size of the larger bitmap
            resBitmap = new FileBitmap(new byte[maxSize]);
        }

        // Start index for iterating over the bitmaps
        var index = 0;

#if NET8_0_OR_GREATER
        // Use SIMD operations for efficient bitwise OR operations when the target framework supports it
        // This section is optimized for .NET 8.0 or greater, utilizing larger vector sizes when available
        for (; minSize - index >= 64; index += 64)
        {
            // Read 64 bytes (512 bits) at once from each bitmap and perform a bitwise OR
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }
#endif

#if NET7_0_OR_GREATER
        // Continue with smaller vector sizes if .NET 7.0 or greater is being used
        for (; minSize - index >= 32; index += 32)
        {
            // Perform bitwise OR on 32 bytes (256 bits) at a time
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; minSize - index >= 16; index += 16)
        {
            // Perform bitwise OR on 16 bytes (128 bits) at a time
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }
#else

#if NET6_0
        if (AdvSimd.IsSupported)
        {
            // ARM64 SIMD operations
            for (; minSize - index >= 16; index += 16)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = AdvSimd.LoadVector128(fileBitmapA._bitmap + index);
                var b = AdvSimd.LoadVector128(fileBitmapB._bitmap + index);
                AdvSimd.Store(resBitmap._bitmap + index, AdvSimd.Or(a, b));
            }
        }
#endif

#if NETCOREAPP3_0_OR_GREATER
        // Use 256-bit (32-byte) vectors if available
        if (Avx.IsSupported)
        {
            // X86 SIMD operations
            for (; minSize - index >= 32; index += 32)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = Avx.LoadVector256(fileBitmapA._bitmap + index);
                var b = Avx.LoadVector256(fileBitmapB._bitmap + index);
                Avx.Store(resBitmap._bitmap + index, Avx2.Or(a, b));
            }
        }

        // Use 128-bit (16-byte) vectors if available
        if (Sse2.IsSupported)
        {
            // X86 SIMD operations
            for (; minSize - index >= 16; index += 16)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = Sse2.LoadVector128(fileBitmapA._bitmap + index);
                var b = Sse2.LoadVector128(fileBitmapB._bitmap + index);
                Sse2.Store(resBitmap._bitmap + index, Sse2.Or(a, b));
            }
        }
#endif

#endif

        for (; minSize - index >= 8; index += 8)
        {
            // Read and OR 8 bytes (64 bits) at once
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; minSize - index >= 4; index += 4)
        {
            // Process 4 bytes (32 bits) at a time
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; minSize - index >= 2; index += 2)
        {
            // Process 2 bytes (16 bits) at a time
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)(a | b));
        }

        // Handle any remaining bytes one at a time
        for (; index < minSize; index++)
        {
            // Perform bitwise OR on individual bytes
            resBitmap._bitmap[index] = (byte)(fileBitmapA._bitmap[index] | fileBitmapB._bitmap[index]);
        }

        // If the sizes of the original bitmaps differ, fill in the remaining bits from the larger bitmap
        if (minSize != maxSize)
        {
            var bitmap = fileBitmapA._size == maxSize ? fileBitmapA._bitmap : fileBitmapB._bitmap;
            for (; index < maxSize; index++)
            {
                // Copy the remaining bytes from the larger bitmap
                resBitmap._bitmap[index] = bitmap[index];
            }
        }

        // Return the resulting bitmap, which contains the bitwise OR of the two input bitmaps
        return resBitmap;
    }

    /// <summary>
    /// Performs a bitwise AND operation on two <see cref="FileBitmap"/> instances.
    /// </summary>
    /// <param name="fileBitmapA">The first operand.</param>
    /// <param name="fileBitmapB">The second operand.</param>
    /// <param name="reuseBufferFromBitmapA">True if the buffer of the bitmap 'a' should be used for the response operation</param>
    /// <returns>The result of the bitwise AND operation.</returns>
    public static FileBitmap And(FileBitmap fileBitmapA, FileBitmap fileBitmapB, bool reuseBufferFromBitmapA)
    {
        var minSize = fileBitmapA._size;
        var maxSize = fileBitmapB._size;
        var resBitmap = fileBitmapA;

        if (!reuseBufferFromBitmapA)
        {
            // Determine the minimum and maximum sizes of the two bitmaps to handle bitmaps of different lengths
            minSize = Math.Min(fileBitmapA._size, fileBitmapB._size);
            maxSize = Math.Max(fileBitmapA._size, fileBitmapB._size);

            // Create a new bitmap to store the result of the AND operation, initializing it to the size of the larger bitmap
            resBitmap = new FileBitmap(new byte[maxSize]);
        }

        // Start index for iterating over the bitmaps
        var index = 0;

#if NET8_0_OR_GREATER
        // Use SIMD operations for efficient bitwise AND operations when the target framework supports it
        // This section is optimized for .NET 8.0 or greater, utilizing larger vector sizes when available
        for (; minSize - index >= 64; index += 64)
        {
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }
#endif

#if NET7_0_OR_GREATER
        // Continue with smaller vector sizes if .NET 7.0 or greater is being used
        for (; minSize - index >= 32; index += 32)
        {
            // Perform bitwise AND on 32 bytes (256 bits) at a time
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; minSize - index >= 16; index += 16)
        {
            // Perform bitwise AND on 16 bytes (128 bits) at a time
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }
#else

#if NET6_0
        if (AdvSimd.IsSupported)
        {
            // ARM64 SIMD operations
            for (; minSize - index >= 16; index += 16)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = AdvSimd.LoadVector128(fileBitmapA._bitmap + index);
                var b = AdvSimd.LoadVector128(fileBitmapB._bitmap + index);
                AdvSimd.Store(resBitmap._bitmap + index, AdvSimd.And(a, b));
            }
        }
#endif

#if NETCOREAPP3_0_OR_GREATER
        // Use 256-bit (32-byte) vectors if available
        if (Avx.IsSupported)
        {
            // X86 SIMD operations
            for (; minSize - index >= 32; index += 32)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = Avx.LoadVector256(fileBitmapA._bitmap + index);
                var b = Avx.LoadVector256(fileBitmapB._bitmap + index);
                Avx.Store(resBitmap._bitmap + index, Avx2.And(a, b));
            }
        }

        // Use 128-bit (16-byte) vectors if available
        if (Sse2.IsSupported)
        {
            // X86 SIMD operations
            for (; minSize - index >= 16; index += 16)
            {
                // Perform bitwise OR on 16 bytes (128 bits) at a time
                // Load 128 bits (16 bytes) from each bitmap into SIMD registers
                var a = Sse2.LoadVector128(fileBitmapA._bitmap + index);
                var b = Sse2.LoadVector128(fileBitmapB._bitmap + index);
                Sse2.Store(resBitmap._bitmap + index, Sse2.And(a, b));
            }
        }
#endif

#endif

        for (; minSize - index >= 8; index += 8)
        {
            // Read and AND 8 bytes (64 bits) at once
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; minSize - index >= 4; index += 4)
        {
            // Process 4 bytes (32 bits) at a time
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; minSize - index >= 2; index += 2)
        {
            // Process 2 bytes (16 bits) at a time
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)(a & b));
        }

        // Handle any remaining bytes one at a time
        for (; index < minSize; index++)
        {
            // Perform bitwise AND on individual bytes
            resBitmap._bitmap[index] = (byte)(fileBitmapA._bitmap[index] & fileBitmapB._bitmap[index]);
        }

        // If the sizes of the original bitmaps differ, fill in the extra space in the result with zeros.
        if (minSize != maxSize)
        {
            // Since we're performing an AND operation, any bits beyond the size of the smaller bitmap should be 0.
            for (; index < maxSize; index++)
            {
                resBitmap._bitmap[index] = 0;
            }
        }

        // Return the resulting bitmap, which contains the bitwise AND of the two input bitmaps.
        return resBitmap;
    }

    /// <summary>
    /// Performs a bitwise NOT operation on a <see cref="FileBitmap"/> instance.
    /// </summary>
    /// <param name="fileBitmap">The operand.</param>
    /// <param name="reuseBufferFromBitmap">True if the buffer of the bitmap should be used for the response operation</param>
    /// <returns>The result of the bitwise NOT operation.</returns>
    public static FileBitmap Not(FileBitmap fileBitmap, bool reuseBufferFromBitmap)
    {
        var resBitmap = fileBitmap;
        if (!reuseBufferFromBitmap)
        {
            // Create a new bitmap to store the result of the NOT operation, with the same size as the input bitmap.
            resBitmap = new FileBitmap(new byte[fileBitmap._size]);
        }

        // Start index for iterating over the bitmap.
        var index = 0;

        // Size of the bitmap in bytes
        var size = fileBitmap._size;

        // Pointer to the bitmap data
        var bitmap = fileBitmap._bitmap;

#if NET8_0_OR_GREATER
        // Use SIMD operations for efficient bitwise NOT operations when the target framework supports it.
        // This section is optimized for .NET 8.0 or greater, utilizing larger vector sizes when available.
        for (; size - index >= 64; index += 64)
        {
            // Read 64 bytes (512 bits) at once from the bitmap and perform a bitwise NOT.
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }
#endif

#if NET7_0_OR_GREATER
        // Continue with smaller vector sizes if .NET 7.0 or greater is being used.
        for (; size - index >= 32; index += 32)
        {
            // Perform bitwise NOT on 32 bytes (256 bits) at a time.
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 16; index += 16)
        {
            // Perform bitwise NOT on 16 bytes (128 bits) at a time.
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }
#else

#if NET6_0
        if (AdvSimd.IsSupported)
        {
            for (; size - index >= 16; index += 16)
            {
                // Perform bitwise NOT on 16 bytes (128 bits) at a time.
                var a = AdvSimd.LoadVector128(bitmap + index);
                AdvSimd.Store(resBitmap._bitmap + index, AdvSimd.Not(a));
            }
        }
#endif

#if NETCOREAPP3_0_OR_GREATER
        if (Avx.IsSupported)
        {
            var allOnesVector = Vector256.Create((byte)0xFF); // Create a vector with all bits set to 1
            for (; size - index >= 32; index += 32)
            {
                // Perform bitwise NOT on 32 bytes (256 bits) at a time.
                var a = Avx.LoadVector256(bitmap + index);
                Avx.Store(resBitmap._bitmap + index, Avx2.Xor(a, allOnesVector));
            }
        }

        if (Sse2.IsSupported)
        {
            var allOnesVector = Vector128.Create((byte)0xFF); // Create a vector with all bits set to 1
            for (; size - index >= 16; index += 16)
            {
                // Perform bitwise NOT on 16 bytes (128 bits) at a time.
                var a = Sse2.LoadVector128(bitmap + index);
                Sse2.Store(resBitmap._bitmap + index, Sse2.Xor(a, allOnesVector));
            }
        }
#endif

#endif

        for (; size - index >= 8; index += 8)
        {
            // Read and NOT 8 bytes (64 bits) at once.
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 4; index += 4)
        {
            // Process 4 bytes (32 bits) at a time.
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 2; index += 2)
        {
            // Process 2 bytes (16 bits) at a time.
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)~a);
        }

        // Handle any remaining bytes one at a time.
        for (; index < size; index++)
        {
            // Perform bitwise NOT on individual bytes.
            resBitmap._bitmap[index] = (byte)~bitmap[index];
        }

        // Return the resulting bitmap, which contains the inverted bits of the input bitmap.
        return resBitmap;
    }

    /// <summary>
    /// Calculates the required storage size for a given number of lines.
    /// </summary>
    /// <param name="numOfLines">The number of lines.</param>
    /// <returns>The required storage size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSize(int numOfLines) => (numOfLines + 7) / 8;

#if !NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Hamming weight algorithm for ulong
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SlowLongPopCount(ulong value)
    {
        // Constants for the Hamming weight algorithm
        const ulong c1 = 0x_55555555_55555555ul; // Pattern: 01010101...
        const ulong c2 = 0x_33333333_33333333ul; // Pattern: 00110011...
        const ulong c3 = 0x_0F0F0F0F_0F0F0F0Ful; // Pattern: 00001111...
        const ulong c4 = 0x_01010101_01010101ul; // Pattern: 00000001...

        // Step 1: Pairwise reduction to count bits in each pair of bits
        // This step turns every two bits into a single bit (1 if either of the two was 1)
        // Subtract pairwise reduced values from original
        value -= (value >> 1) & c1;

        // Step 2: Nibble-wise reduction to count bits in each nibble (4 bits)
        // This step combines every four bits into a sum of those bits
        // Combine nibble pairs
        value = (value & c2) + ((value >> 2) & c2);

        // Step 3: Byte-wise reduction to count bits in each byte
        // This step adds up the bits in each byte to produce a sum per byte
        // Combine sums within each byte
        // Step 4: Multiplication and right shift to aggregate the counts
        // This step aggregates the counts from all bytes into the most significant byte
        // Multiply and shift to aggregate bit counts
        value = (((value + (value >> 4)) & c3) * c4) >> 56;

        // The final result is the sum of bits set to 1 in the original ulong value
        return (int)value;
    }

    /// <summary>
    /// Hamming weight algorithm for uint
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SlowIntPopCount(uint value)
    {
        // Constants for the Hamming weight algorithm tailored for 32-bit integers
        const uint c1 = 0x_55555555u; // Pattern: 01010101...
        const uint c2 = 0x_33333333u; // Pattern: 00110011...
        const uint c3 = 0x_0F0F0F0Fu; // Pattern: 00001111...
        const uint c4 = 0x_01010101u; // Pattern: 00000001...

        // Step 1: Pairwise reduction to count bits in each pair
        // This subtracts from each pair of bits, effectively halving the number of bits to consider
        // Apply mask and subtract
        value -= (value >> 1) & c1;

        // Step 2: Nibble-wise reduction to count bits in each 4-bit set (nibble)
        // This combines counts from adjacent nibbles into a single nibble
        // Combine counts within nibbles
        value = (value & c2) + ((value >> 2) & c2);

        // Step 3: Byte-wise reduction to sum bits in each byte
        // This step accumulates the bit counts from each byte into that byte's value
        // Sum counts within each byte
        // Step 4: Aggregation of counts
        // This multiplies the byte-wise counts by a specific pattern and shifts the result
        // to aggregate all counts into the least significant byte of the result
        // Aggregate counts into a single sum
        value = (((value + (value >> 4)) & c3) * c4) >> 24;

        // The final value is now the total count of bits set to 1 in the original uint value
        return (int)value;
    }
#endif

    /// <summary>
    /// Sets a bit at a specified line to 1.
    /// </summary>
    /// <param name="line">The line number of the bit to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int line)
    {
        // Check if the pointer is valid
        if (_bitmap == null)
        {
            return;
        }

        // Decrements the line number to align with zero-based index
        var idx = (uint)line - 1;

        // Determines the byte index in the bitmap array by dividing the bit index by 8 (since there are 8 bits in a byte)
        // This effectively shifts the bit index right by 3 places, equivalent to dividing by 8 but faster.
        var byteIndex = idx >> 3;

        // Calculates the bit position within the target byte by using the modulus operation with 7 (idx & 7)
        // This finds the remainder of idx / 8, giving the exact bit position within the byte where the bit needs to be set.
        // 128 >> (int)(idx & 7) creates a mask by shifting the bit '1' into the correct position within the byte.
        // For example, if idx & 7 results in 2, it shifts '10000000' right by 2, resulting in '00100000'.
        var bitMask = (byte)(128 >> (int)(idx & 7));

        // ORs the target byte with the bitmask, setting the specified bit to 1 without altering the other bits.
        // This is done by directly accessing the byte in the bitmap at the calculated byte index and applying the bitmask.
        // The |= operator ensures that if the bit was already set to 1, it remains as 1, effectively setting only the intended bit.
        _bitmap[byteIndex] |= bitMask;
    }

    /// <summary>
    /// Gets the value of a bit at a specified line.
    /// </summary>
    /// <param name="line">The line number of the bit to get.</param>
    /// <returns>True if the bit is set to 1, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int line)
    {
        // Check if the pointer is valid
        if (_bitmap == null)
        {
            return false;
        }

        // Decrements the line number to align with zero-based index
        var idx = (uint)line - 1;

        // Determines the byte index in the bitmap array by dividing the bit index by 8 (since there are 8 bits in a byte)
        // This effectively shifts the bit index right by 3 places, equivalent to dividing by 8 but faster.
        var byteIndex = idx >> 3;

        // Calculates the bit position within the target byte by using the modulus operation with 7 (idx & 7)
        // This finds the remainder of idx / 8, giving the exact bit position within the byte where the bit needs to be set.
        // 128 >> (int)(idx & 7) creates a mask by shifting the bit '1' into the correct position within the byte.
        // For example, if idx & 7 results in 2, it shifts '10000000' right by 2, resulting in '00100000'.
        var bitMask = (byte)(128 >> (int)(idx & 7));

        // Use the bitmask to isolate the desired bit within the byte
        // The & operation masks out all other bits but the one we're interested in.
        // If the result is not 0, it means the bit was set; otherwise, it was clear.
        return (_bitmap[byteIndex] & bitMask) != 0;
    }

    /// <summary>
    /// Counts the number of bits set to 1 in the bitmap.
    /// </summary>
    /// <returns>The number of active bits.</returns>
    public int CountActiveBits()
    {
        var count = 0; // Initialize a counter for the active bits

        // Iterate over each byte of the bitmap
        for (var i = 0; i < _size; i++)
        {
            // Attempt to count bits 8 bytes at a time for efficiency
            // Ensure there's at least 8 bytes left to read as ulong (64 bits)
            if (i + 7 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // If supported, use the BitOperations.PopCount method for fast population count of bits set to 1
                // This method utilizes hardware acceleration (if available) to count the bits efficiently
                count += BitOperations.PopCount(*(ulong*)(_bitmap + i));
#else
                // For platforms without BitOperations.PopCount, fall back to a manual method
                // SlowLongPopCount is a custom method implementing the hamming weight algorithm for ulong
                count += SlowLongPopCount(*(ulong*)(_bitmap + i));
#endif
                // Skip ahead by 7 bytes as we've read 8 bytes in total
                i += 7;
                continue;
            }

            // Attempt to count bits 4 bytes at a time
            // Ensure there's at least 4 bytes left to read as uint (32 bits)
            if (i + 3 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // Use BitOperations.PopCount for a uint, counting bits in 4 bytes
                count += BitOperations.PopCount(*(uint*)(_bitmap + i));
#else
                // Fall back to a manual method for uint
                count += SlowIntPopCount(*(uint*)(_bitmap + i));
#endif
                // Skip ahead by 3 bytes as we've read 4 bytes in total
                i += 3;
                continue;
            }

            // Count remaining bits one byte at a time
#if NETCOREAPP3_0_OR_GREATER
            // Use BitOperations.PopCount for a single byte
            count += BitOperations.PopCount(_bitmap[i]);
#else
            // Fall back to a manual method for a single byte
            count += SlowIntPopCount(_bitmap[i]);
#endif
        }

        return count;
    }

    /// <summary>
    /// Gets if the bitmap has at least 1 bit set to 1
    /// </summary>
    /// <returns>True if the bitmap has at least 1 bit set to 1; otherwise, false..</returns>
    public bool HasActiveBits()
    {
        // Iterate over each byte of the bitmap
        for (var i = 0; i < _size; i++)
        {
            // Attempt to count bits 8 bytes at a time for efficiency
            // Ensure there's at least 8 bytes left to read as ulong (64 bits)
            if (i + 7 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // If supported, use the BitOperations.PopCount method for fast population count of bits set to 1
                // This method utilizes hardware acceleration (if available) to count the bits efficiently
                if (BitOperations.PopCount(*(ulong*)(_bitmap + i)) > 0)
                {
                    return true;
                }
#else
                // For platforms without BitOperations.PopCount, fall back to a manual method
                // SlowLongPopCount is a custom method implementing the hamming weight algorithm for ulong
                if (SlowLongPopCount(*(ulong*)(_bitmap + i)) > 0)
                {
                    return true;
                }
#endif
                // Skip ahead by 7 bytes as we've read 8 bytes in total
                i += 7;
                continue;
            }

            // Attempt to count bits 4 bytes at a time
            // Ensure there's at least 4 bytes left to read as uint (32 bits)
            if (i + 3 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // Use BitOperations.PopCount for a uint, counting bits in 4 bytes
                if (BitOperations.PopCount(*(uint*)(_bitmap + i)) > 0)
                {
                    return true;
                }
#else
                // Fall back to a manual method for uint
                if (SlowIntPopCount(*(uint*)(_bitmap + i)) > 0)
                {
                    return true;
                }
#endif
                // Skip ahead by 3 bytes as we've read 4 bytes in total
                i += 3;
                continue;
            }

            // Count remaining bits one byte at a time
#if NETCOREAPP3_0_OR_GREATER
            // Use BitOperations.PopCount for a single byte
            if (BitOperations.PopCount(_bitmap[i]) > 0)
            {
                return true;
            }
#else
            // Fall back to a manual method for a single byte
            if (SlowIntPopCount(_bitmap[i]) > 0)
            {
                return true;
            }
#endif
        }

        return false;
    }

    /// <summary>
    /// Releases any resources if the bitmap is disposable.
    /// </summary>
    public void Dispose()
    {
        if (_handle is { IsAllocated: true } handle)
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Writes the bitmap data to a byte array.
    /// </summary>
    /// <param name="array">The array to write to.</param>
    public void Write(byte[] array)
    {
        fixed (byte* arrayPtr = array)
        {
            Buffer.MemoryCopy(_bitmap, arrayPtr, array.Length, _size);
        }
    }

    /// <summary>
    /// Converts the bitmap to a byte array.
    /// </summary>
    /// <returns>A new byte array containing the bitmap data.</returns>
    public byte[] ToArray()
    {
        var array = new byte[_size];
        Write(array);
        return array;
    }

    /// <summary>
    /// Returns the internal array if available, otherwise creates a new array and disposes the bitmap.
    /// </summary>
    /// <returns>The internal array or a new array containing the bitmap data.</returns>
    internal byte[] GetInternalArrayOrToArrayAndDispose()
    {
        if (_handle is { } handle)
        {
            try
            {
                return handle.IsAllocated ? (byte[])(handle.Target ?? Array.Empty<byte>()) : [];
            }
            finally
            {
                Dispose();
            }
        }

        return ToArray();
    }

    /// <summary>
    /// Returns a string representation of the bitmap.
    /// </summary>
    /// <returns>A string showing the bitmap in binary form.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _size; i++)
        {
            sb.Append(Convert.ToString(_bitmap[i], 2).PadLeft(8, '0'));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Provides enumeration over the bits of the bitmap.
    /// </summary>
    /// <returns>An enumerator for the bits of the bitmap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_bitmap, _size);

    /// <summary>
    /// Enumerator for iterating over the bits of a <see cref="FileBitmap"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<byte>
    {
        private int _index = -1;
        private byte* _bitMap;
        private int _size;

        internal Enumerator(byte* bitMap, int size)
        {
            _bitMap = bitMap;
            _size = size;
        }

        public byte Current => _bitMap[_index];

        object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _size;

        public void Reset()
        {
            _index = 0;
        }

        public void Dispose()
        {
            _bitMap = null;
            _size = 0;
            _index = 0;
        }
    }
}
