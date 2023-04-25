// <copyright file="MessagePackBinaryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;

namespace Datadog.Trace.Vendors.MessagePack
{
    internal static partial class MessagePackBinary
    {
        public static int WriteRaw(ref byte[] bytes, int offset, ReadOnlySpan<byte> rawMessagePackBlock)
        {
            EnsureCapacity(ref bytes, offset, rawMessagePackBlock.Length);
            rawMessagePackBlock.CopyTo(bytes.AsSpan(offset, rawMessagePackBlock.Length));
            return rawMessagePackBlock.Length;
        }

        public static int WriteBytes(ref byte[] bytes, int offset, ReadOnlySpan<byte> rawMessagePackBlock)
        {
            var count = rawMessagePackBlock.Length;
            if (count <= byte.MaxValue)
            {
                var size = count + 2;
                EnsureCapacity(ref bytes, offset, size);

                bytes[offset] = MessagePackCode.Bin8;
                bytes[offset + 1] = (byte)count;

                rawMessagePackBlock.CopyTo(bytes.AsSpan(offset + 2, count));
                return size;
            }
            else if (count <= ushort.MaxValue)
            {
                var size = count + 3;
                EnsureCapacity(ref bytes, offset, size);

                unchecked
                {
                    bytes[offset] = MessagePackCode.Bin16;
                    bytes[offset + 1] = (byte)(count >> 8);
                    bytes[offset + 2] = (byte)(count);
                }

                rawMessagePackBlock.CopyTo(bytes.AsSpan(offset + 3, count));
                return size;
            }
            else
            {
                var size = count + 5;
                EnsureCapacity(ref bytes, offset, size);

                unchecked
                {
                    bytes[offset] = MessagePackCode.Bin32;
                    bytes[offset + 1] = (byte)(count >> 24);
                    bytes[offset + 2] = (byte)(count >> 16);
                    bytes[offset + 3] = (byte)(count >> 8);
                    bytes[offset + 4] = (byte)(count);
                }

                rawMessagePackBlock.CopyTo(bytes.AsSpan(offset + 5, count));
                return size;
            }
        }

        public static int WriteStringBytes(ref byte[] bytes, int offset, ReadOnlySpan<byte> utf8stringBytes)
        {
            var byteCount = utf8stringBytes.Length;
            if (byteCount <= MessagePackRange.MaxFixStringLength)
            {
                EnsureCapacity(ref bytes, offset, byteCount + 1);
                bytes[offset] = (byte)(MessagePackCode.MinFixStr | byteCount);
                utf8stringBytes.CopyTo(bytes.AsSpan(offset + 1, byteCount));
                return byteCount + 1;
            }
            else if (byteCount <= byte.MaxValue)
            {
                EnsureCapacity(ref bytes, offset, byteCount + 2);
                bytes[offset] = MessagePackCode.Str8;
                bytes[offset + 1] = unchecked((byte)byteCount);
                utf8stringBytes.CopyTo(bytes.AsSpan(offset + 2, byteCount));
                return byteCount + 2;
            }
            else if (byteCount <= ushort.MaxValue)
            {
                EnsureCapacity(ref bytes, offset, byteCount + 3);
                bytes[offset] = MessagePackCode.Str16;
                bytes[offset + 1] = unchecked((byte)(byteCount >> 8));
                bytes[offset + 2] = unchecked((byte)byteCount);
                utf8stringBytes.CopyTo(bytes.AsSpan(offset + 3, byteCount));
                return byteCount + 3;
            }
            else
            {
                EnsureCapacity(ref bytes, offset, byteCount + 5);
                bytes[offset] = MessagePackCode.Str32;
                bytes[offset + 1] = unchecked((byte)(byteCount >> 24));
                bytes[offset + 2] = unchecked((byte)(byteCount >> 16));
                bytes[offset + 3] = unchecked((byte)(byteCount >> 8));
                bytes[offset + 4] = unchecked((byte)byteCount);
                utf8stringBytes.CopyTo(bytes.AsSpan(offset + 5, byteCount));
                return byteCount + 5;
            }
        }
    }
}
#endif
