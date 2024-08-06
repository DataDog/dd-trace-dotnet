// <copyright file="ArrayExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Ci.Coverage.Util;

internal static class ArrayExtensions
{
    /// <summary>
    /// Gets the item value reference of an array avoiding bound checks
    /// WARNING: This method tries to avoid bound checks. This completely unsafe, use only if you know what you are doing.
    /// </summary>
    /// <param name="array">Array instance</param>
    /// <param name="index">Index of the item</param>
    /// <typeparam name="T">Type of the array</typeparam>
    /// <returns>Index value reference of the array</returns>
#if NETCOREAPP3_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static ref T FastGetReference<T>(this T[] array, int index)
    {
#if NET5_0_OR_GREATER
        // Avoid bound checks
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
#elif NETCOREAPP3_0_OR_GREATER
        // Avoid bound checks
        return ref Unsafe.Add(ref Unsafe.As<byte, T>(ref Unsafe.As<RawArrayData>(array).Data), index);
#else
        return ref array[index];
#endif
    }

#if NETCOREAPP3_0_OR_GREATER
    [StructLayout(LayoutKind.Sequential)]
    private sealed class RawArrayData
    {
#pragma warning disable CS0649 // Unassigned fields
#pragma warning disable SA1401 // Fields should be private
        public IntPtr Length;
        public byte Data;
#pragma warning restore CS0649
#pragma warning restore SA1401
    }
#endif
}
