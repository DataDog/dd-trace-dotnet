// <copyright file="CharSlice.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a slice of a UTF-8 encoded string in memory.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/slice.rs#L51
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CharSlice : IDisposable
{
    private const int MaxBytesForMaxStringLength = (4096 * 2) + 1; // 4096 characters max, UTF-8 encoding can take up to 2 bytes per character, plus 1 for the null terminator
    private const int PoolSize = 100;

    /// <summary>
    /// Memory pool for managing unmanaged memory allocations for <see cref="CharSlice"/>.
    /// </summary>
    private static readonly UnmanagedMemoryPool UnmanagedPool = new(MaxBytesForMaxStringLength, PoolSize);

    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal readonly nint Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal readonly nuint Len;

    private readonly bool _fromPool;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharSlice"/> struct.
    /// This can be further optimized if we can avoid copying the string to unmanaged memory.
    /// </summary>
    /// <param name="str">The string to copy into memory.</param>
    /// <param name="forceAlloc">If true, forces allocation from the heap instead of the pool.</param>
    internal CharSlice(string? str, bool forceAlloc = false)
    {
        if (str == null)
        {
            Ptr = IntPtr.Zero;
            Len = UIntPtr.Zero;
            _fromPool = false;
        }
        else
        {
            var encoding = Encoding.UTF8;
            var maxBytesCount = encoding.GetMaxByteCount(str.Length);
            if (forceAlloc || maxBytesCount > MaxBytesForMaxStringLength)
            {
                Ptr = Marshal.AllocHGlobal(maxBytesCount);
                _fromPool = false;
            }
            else
            {
                Ptr = UnmanagedPool.Rent();
                _fromPool = true;
            }

            unsafe
            {
                fixed (char* strPtr = str)
                {
                    Len = (nuint)encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                }
            }
        }
    }

    public void Dispose()
    {
        if (Ptr == IntPtr.Zero)
        {
            return;
        }

        if (_fromPool)
        {
            UnmanagedPool.Return(Ptr);
            return;
        }

        Marshal.FreeHGlobal(Ptr);
    }
}
