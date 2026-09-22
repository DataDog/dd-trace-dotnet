// <copyright file="BitConverterShim.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETCOREAPP3_1_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Util;

internal static class BitConverterShim
{
    public static bool IsLittleEndian => BitConverter.IsLittleEndian;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToUInt64(ReadOnlySpan<byte> value)
    {
        if (value.Length < sizeof(ulong))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value));
        }

        return Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(value));
    }
}

#endif
