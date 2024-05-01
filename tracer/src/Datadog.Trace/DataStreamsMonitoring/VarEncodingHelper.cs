// <copyright file="VarEncodingHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;

namespace Datadog.Trace.DataStreamsMonitoring;

internal static class VarEncodingHelper
{
    /// <summary>
    /// The maximum number of bytes used to encode a ulong
    /// </summary>
    private const int MaxVarLen64 = 9;

    /// <summary>
    /// Stores the number of bytes a <c>long</c> with a
    /// given number of leading zeros will be encoded as
    /// </summary>
    private static readonly int[] VarLongLengths;

    static VarEncodingHelper()
    {
        VarLongLengths = new int[65];
        VarLongLengths[0] = MaxVarLen64; // special case (extra bit for encoding)
        VarLongLengths[64] = 1; // special case (always need at least 1 byte)

        for (var i = 1; i < 64; i++)
        {
            var value = (70 - i) / 7;
            VarLongLengths[i] = value;
        }
    }

    private interface IByteWriter
    {
        void WriteByte(int offset, byte value);
    }

    /// <summary>
    /// Serializes 64-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 9
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (8*7+8 = 64).
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLong(byte[] bytes, int offset, ulong value)
        => WriteVarLong(value, new ByteArrayByteWriter(bytes, offset));

    /// <summary>
    /// Serializes 64-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 9
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (8*7+8 = 64).
    /// </summary>
    /// <param name="writer">The writer to use to write the bytes</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLong(BinaryWriter writer, ulong value)
        => WriteVarLong(value, new BinaryWriterByteWriter(writer));

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Serializes 64-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 9
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (8*7+8 = 64).
    /// </summary>
    /// <param name="bytes">The buffer to write the encoded value to</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLong(Span<byte> bytes, ulong value)
    {
        // duplicate implementation as can't include Span<T> in IByteWriter implementations
        var length = VarLongLength(value);
        for (var i = 0; i < length - 1; i++)
        {
            bytes[i] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        bytes[length - 1] = (byte)(value);
        return length;
    }
#endif

    /// <summary>
    /// Serializes 64-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLongZigZag(byte[] bytes, int offset, long value)
        => WriteVarLong(bytes, offset, ZigZag(value));

    /// <summary>
    /// Serializes 64-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="writer">The writer to use to write the bytes</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLongZigZag(BinaryWriter writer, long value)
        => WriteVarLong(writer, ZigZag(value));

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Serializes 64-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="bytes">The span to write the buffer into</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarLongZigZag(Span<byte> bytes, long value)
        => WriteVarLong(bytes, ZigZag(value));
#endif

    /// <summary>
    /// Serializes 32-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 5
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (5*7+8 = 64).
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarInt(byte[] bytes, int offset, uint value)
        => WriteVarLong(value, new ByteArrayByteWriter(bytes, offset));

    /// <summary>
    /// Serializes 32-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 5
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (5*7+8 = 64).
    /// </summary>
    /// <param name="writer">The writer to use to write the bytes</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarInt(BinaryWriter writer, uint value)
        => WriteVarLong(value, new BinaryWriterByteWriter(writer));

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Serializes 32-bit unsigned integers 7 bits at a time,
    /// starting with the least significant bits. The most significant bit in each
    /// output byte is the continuation bit and indicates whether there are
    /// additional non-zero bits encoded in following bytes. There are at most 5
    /// output bytes and the last one does not have a continuation bit, allowing for
    /// it to encode 8 bits (5*7+8 = 64).
    /// </summary>
    /// <param name="bytes">The buffer to write the encoded value to</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarInt(Span<byte> bytes, uint value)
        => WriteVarLong(bytes, value);
#endif

    /// <summary>
    /// Serializes 32-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarIntZigZag(byte[] bytes, int offset, int value)
        => WriteVarLongZigZag(bytes, offset, value);

    /// <summary>
    /// Serializes 32-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="writer">The writer to use to write the bytes</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarIntZigZag(BinaryWriter writer, int value)
        => WriteVarLongZigZag(writer, value);

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Serializes 32-bit signed integers using zig-zag encoding,
    /// which ensures small-scale integers are turned into unsigned integers
    /// that have leading zeros, whether they are positive or negative,
    /// hence allows for space-efficient encoding of those values.
    /// </summary>
    /// <param name="bytes">The span to write the buffer into</param>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes written</returns>
    public static int WriteVarIntZigZag(Span<byte> bytes, int value)
        => WriteVarLongZigZag(bytes, value);
#endif

    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarLong(byte[],int,ulong)"/>
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static ulong? ReadVarLong(byte[] bytes, int offset, out int bytesRead)
    {
        ulong value = 0;
        var i = 0;
        var shift = 0;
        while ((i + offset) < bytes.Length)
        {
            var next = bytes[i + offset];
            if (next < 0x80 || i == MaxVarLen64 - 1)
            {
                bytesRead = i + 1;
                return value | ((ulong)next << shift);
            }

            value |= ((ulong)next & 0x7FL) << shift;
            i++;
            shift += 7;
        }

        // something went wrong, invalid encoding
        bytesRead = default;
        return null;
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarLong(byte[],int,ulong)"/>
    /// </summary>
    /// <param name="bytes">The buffer to read the encoded value from</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static ulong? ReadVarLong(Span<byte> bytes, out int bytesRead)
    {
        // duplicate implementation
        ulong value = 0;
        var i = 0;
        var shift = 0;
        while (i < bytes.Length)
        {
            var next = bytes[i];
            if (next < 0x80 || i == MaxVarLen64 - 1)
            {
                bytesRead = i + 1;
                return value | ((ulong)next << shift);
            }

            value |= ((ulong)next & 0x7FL) << shift;
            i++;
            shift += 7;
        }

        // something went wrong, invalid encoding
        bytesRead = default;
        return null;
    }
#endif

    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarLongZigZag(byte[],int,long)"/>
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static long? ReadVarLongZigZag(byte[] bytes, int offset, out int bytesRead)
        => ReadVarLong(bytes, offset, out bytesRead) is { } decoded ? UnZigZag(decoded) : null;

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarLongZigZag(byte[],int,long)"/>
    /// </summary>
    /// <param name="bytes">The buffer to read the encoded value from</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static long? ReadVarLongZigZag(Span<byte> bytes, out int bytesRead)
        => ReadVarLong(bytes, out bytesRead) is { } decoded ? UnZigZag(decoded) : null;
#endif

    /// <summary>
    /// Deserializes 32-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarInt(byte[],int,uint)" />
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static uint? ReadVarInt(byte[] bytes, int offset, out int bytesRead)
        => ReadVarLong(bytes, offset, out bytesRead) is { } decoded and <= int.MaxValue
            ? (uint)decoded
            : null;

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarInt(byte[],int,uint)" />
    /// </summary>
    /// <param name="bytes">The buffer to read the encoded value from</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static uint? ReadVarInt(Span<byte> bytes, out int bytesRead)
        => ReadVarLong(bytes, out bytesRead) is { } decoded and <= int.MaxValue
            ? (uint)decoded
            : null;
#endif

    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarIntZigZag(byte[],int,int)" />
    /// </summary>
    /// <param name="bytes">The byte array to write the buffer into</param>
    /// <param name="offset">The offset within the array to start writing</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static int? ReadVarIntZigZag(byte[] bytes, int offset, out int bytesRead)
        => ReadVarLongZigZag(bytes, offset, out bytesRead) is { } decoded and >= int.MinValue and <= int.MaxValue
            ? (int)decoded
            : null;

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Deserializes 64-bit unsigned integers that have been encoded using
    /// <see cref="WriteVarIntZigZag(byte[],int,int)" />
    /// </summary>
    /// <param name="bytes">The buffer to read the encoded value from</param>
    /// <param name="bytesRead">The number of bytes read when successfully decoded</param>
    /// <returns>The decoded value, or null if decoding failed</returns>
    public static int? ReadVarIntZigZag(Span<byte> bytes, out int bytesRead)
        => ReadVarLongZigZag(bytes, out bytesRead) is { } decoded and >= int.MinValue and <= int.MaxValue
            ? (int)decoded
            : null;
#endif

    /// <summary>
    /// Returns the number of bytes that <see cref="WriteVarIntZigZag(byte[],int,int)"/> encodes a value into.
    /// </summary>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes used to encode</returns>
    public static int VarIntZigZagLength(int value) => VarLongLength(ZigZag(value));

    /// <summary>
    /// Returns the number of bytes that <see cref="WriteVarInt(byte[],int,uint)"/> encodes a value into.
    /// </summary>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes used to encode</returns>
    public static int VarIntLength(uint value) => VarLongLength(value);

    /// <summary>
    /// Returns the number of bytes that <see cref="WriteVarLongZigZag(byte[],int,long)"/> encodes a value into.
    /// </summary>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes used to encode</returns>
    public static int VarLongZigZagLength(long value) => VarLongLength(ZigZag(value));

    /// <summary>
    /// Returns the number of bytes that <see cref="WriteVarLong(byte[],int,ulong)"/> encodes a value into.
    /// </summary>
    /// <param name="value">The value to encode</param>
    /// <returns>The number of bytes used to encode</returns>
    public static int VarLongLength(ulong value) => VarLongLengths[NumberOfLeadingZerosLong(value)];

    /// <summary>
    /// C# implementation of Java Long.numberOfLeadingZeros
    /// </summary>
    private static uint NumberOfLeadingZerosLong(ulong x)
    {
#if NETCOREAPP3_1_OR_GREATER
            return (uint)System.Numerics.BitOperations.LeadingZeroCount(x);
#else
        // Based on: https://stackoverflow.com/a/10439333/869621
        // adjusted for long
        const int numLongBits = sizeof(long) * 8; // compile time constant

        // Do the smearing which turns (for example)
        // this: 0000 0101 0011
        // into: 0000 0111 1111
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x |= x >> 32;

        // Count the ones
        x -= x >> 1 & 0x5555555555555555;
        x = (x >> 2 & 0x3333333333333333) + (x & 0x3333333333333333);
        x = (x >> 4) + x & 0x0f0f0f0f0f0f0f0f;
        x += x >> 8;
        x += x >> 16;
        x += x >> 32;

        return numLongBits - (uint)(x & 0x0000007f); // subtract # of 1s from 64
#endif
    }

    private static int WriteVarLong<T>(ulong value, in T writer)
        where T : IByteWriter
    {
        var length = VarLongLength(value);
        for (var i = 0; i < length - 1; i++)
        {
            writer.WriteByte(i, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        writer.WriteByte(length - 1, (byte)value);
        return length;
    }

    private static ulong ZigZag(long signed) => unchecked((ulong)((signed << 1) ^ (signed >> 63)));

    private static long UnZigZag(ulong unsigned) => unchecked((long)((unsigned >> 1) ^ (0 - (unsigned & 1))));

    private readonly struct ByteArrayByteWriter : IByteWriter
    {
        private readonly byte[] _bytes;
        private readonly int _offset;

        public ByteArrayByteWriter(byte[] bytes, int offset)
        {
            _bytes = bytes;
            _offset = offset;
        }

        public void WriteByte(int offset, byte value)
        {
            _bytes[offset + _offset] = value;
        }
    }

    private readonly struct BinaryWriterByteWriter : IByteWriter
    {
        private readonly BinaryWriter _writer;

        public BinaryWriterByteWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void WriteByte(int offset, byte value)
        {
            _writer.Write(value);
        }
    }
}
