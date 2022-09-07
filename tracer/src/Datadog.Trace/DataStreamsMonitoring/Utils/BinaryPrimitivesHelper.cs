// <copyright file="BinaryPrimitivesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
}
