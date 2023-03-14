// <copyright file="MessagePackBinary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.Vendors.MessagePack;

/// <summary>
/// This is an extension for the original vendored MessagePackBinary file
/// </summary>
internal partial class MessagePackBinary
{
#if NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int WriteRaw(ref byte[] bytes, int offset, ReadOnlySpan<byte> rawMessagePackBlock)
    {
        var bytesCount = rawMessagePackBlock.Length;
        EnsureCapacity(ref bytes, offset, bytesCount);
        rawMessagePackBlock.CopyTo(new Span<byte>(bytes, offset, bytesCount));
        return bytesCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int UnsafeWriteStringBytes(ref byte[] bytes, int offset, ReadOnlySpan<byte> utf8StringBytes)
    {
        var byteCount = utf8StringBytes.Length;
        if (byteCount <= MessagePackRange.MaxFixStringLength)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 1);
            bytes.FastGetReference(offset) = (byte)(MessagePackCode.MinFixStr | byteCount);
            utf8StringBytes.CopyTo(new Span<byte>(bytes, offset + 1, byteCount));
            return byteCount + 1;
        }

        if (byteCount <= byte.MaxValue)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 2);
            bytes.FastGetReference(offset) = MessagePackCode.Str8;
            bytes.FastGetReference(offset + 1) = unchecked((byte)byteCount);
            utf8StringBytes.CopyTo(new Span<byte>(bytes, offset + 2, byteCount));
            return byteCount + 2;
        }

        if (byteCount <= ushort.MaxValue)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 3);
            bytes.FastGetReference(offset) = MessagePackCode.Str16;
            bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 8));
            bytes.FastGetReference(offset + 2) = unchecked((byte)byteCount);
            utf8StringBytes.CopyTo(new Span<byte>(bytes, offset + 3, byteCount));
            return byteCount + 3;
        }

        EnsureCapacity(ref bytes, offset, byteCount + 5);
        bytes.FastGetReference(offset) = MessagePackCode.Str32;
        bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 24));
        bytes.FastGetReference(offset + 2) = unchecked((byte)(byteCount >> 16));
        bytes.FastGetReference(offset + 3) = unchecked((byte)(byteCount >> 8));
        bytes.FastGetReference(offset + 4) = unchecked((byte)byteCount);
        utf8StringBytes.CopyTo(new Span<byte>(bytes, offset + 5, byteCount));
        return byteCount + 5;
    }
#endif

#if NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int UnsafeWriteStringBytes(ref byte[] bytes, int offset, byte[] utf8StringBytes)
    {
        var byteCount = utf8StringBytes.Length;
        if (byteCount <= MessagePackRange.MaxFixStringLength)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 1);
            bytes.FastGetReference(offset) = (byte)(MessagePackCode.MinFixStr | byteCount);
            Buffer.BlockCopy(utf8StringBytes, 0, bytes, offset + 1, byteCount);
            return byteCount + 1;
        }

        if (byteCount <= byte.MaxValue)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 2);
            bytes.FastGetReference(offset) = MessagePackCode.Str8;
            bytes.FastGetReference(offset + 1) = unchecked((byte)byteCount);
            Buffer.BlockCopy(utf8StringBytes, 0, bytes, offset + 2, byteCount);
            return byteCount + 2;
        }

        if (byteCount <= ushort.MaxValue)
        {
            EnsureCapacity(ref bytes, offset, byteCount + 3);
            bytes.FastGetReference(offset) = MessagePackCode.Str16;
            bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 8));
            bytes.FastGetReference(offset + 2) = unchecked((byte)byteCount);
            Buffer.BlockCopy(utf8StringBytes, 0, bytes, offset + 3, byteCount);
            return byteCount + 3;
        }

        EnsureCapacity(ref bytes, offset, byteCount + 5);
        bytes.FastGetReference(offset) = MessagePackCode.Str32;
        bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 24));
        bytes.FastGetReference(offset + 2) = unchecked((byte)(byteCount >> 16));
        bytes.FastGetReference(offset + 3) = unchecked((byte)(byteCount >> 8));
        bytes.FastGetReference(offset + 4) = unchecked((byte)byteCount);
        Buffer.BlockCopy(utf8StringBytes, 0, bytes, offset + 5, byteCount);
        return byteCount + 5;
    }

#if NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static int UnsafeWriteString(ref byte[] bytes, int offset, string value)
    {
        if (value == null)
        {
            return WriteNil(ref bytes, offset);
        }

        // MaxByteCount -> WritePrefix -> GetBytes has some overheads of `MaxByteCount`
        // solves heuristic length check

        // ensure buffer by MaxByteCount(faster than GetByteCount)
        EnsureCapacity(ref bytes, offset, StringEncoding.UTF8.GetMaxByteCount(value.Length) + 5);

        var useOffset = value.Length switch
        {
            <= MessagePackRange.MaxFixStringLength => 1,
            <= byte.MaxValue => 2,
            <= ushort.MaxValue => 3,
            _ => 5
        };

        // skip length area
        var writeBeginOffset = offset + useOffset;
        var byteCount = StringEncoding.UTF8.GetBytes(value, 0, value.Length, bytes, writeBeginOffset);

        // move body and write prefix
        if (byteCount <= MessagePackRange.MaxFixStringLength)
        {
            if (useOffset != 1)
            {
                Buffer.BlockCopy(bytes, writeBeginOffset, bytes, offset + 1, byteCount);
            }

            bytes.FastGetReference(offset) = (byte)(MessagePackCode.MinFixStr | byteCount);
            return byteCount + 1;
        }

        if (byteCount <= byte.MaxValue)
        {
            if (useOffset != 2)
            {
                Buffer.BlockCopy(bytes, writeBeginOffset, bytes, offset + 2, byteCount);
            }

            bytes.FastGetReference(offset) = MessagePackCode.Str8;
            bytes.FastGetReference(offset + 1) = unchecked((byte)byteCount);
            return byteCount + 2;
        }

        if (byteCount <= ushort.MaxValue)
        {
            if (useOffset != 3)
            {
                Buffer.BlockCopy(bytes, writeBeginOffset, bytes, offset + 3, byteCount);
            }

            bytes.FastGetReference(offset) = MessagePackCode.Str16;
            bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 8));
            bytes.FastGetReference(offset + 2) = unchecked((byte)byteCount);
            return byteCount + 3;
        }

        if (useOffset != 5)
        {
            Buffer.BlockCopy(bytes, writeBeginOffset, bytes, offset + 5, byteCount);
        }

        bytes.FastGetReference(offset) = MessagePackCode.Str32;
        bytes.FastGetReference(offset + 1) = unchecked((byte)(byteCount >> 24));
        bytes.FastGetReference(offset + 2) = unchecked((byte)(byteCount >> 16));
        bytes.FastGetReference(offset + 3) = unchecked((byte)(byteCount >> 8));
        bytes.FastGetReference(offset + 4) = unchecked((byte)byteCount);
        return byteCount + 5;
    }
}
