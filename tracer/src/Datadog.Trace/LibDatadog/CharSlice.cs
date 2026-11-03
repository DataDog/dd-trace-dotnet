// <copyright file="CharSlice.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a slice of a UTF-8 encoded string in memory.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/ef6371243c020319e74e8531035f0a5a2fcb8858/libdd-common-ffi/src/slice.rs#L94
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CharSlice : IDisposable
{
    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal readonly nint Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal readonly nuint Len;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharSlice"/> struct.
    /// This can be further optimized if we can avoid copying the string to unmanaged memory.
    /// </summary>
    /// <param name="str">The string to copy into memory.</param>
    internal CharSlice(string? str)
    {
        if (str is null)
        {
            Ptr = IntPtr.Zero;
            Len = UIntPtr.Zero;
        }
        else
        {
            var encoding = StringEncoding.UTF8;
            var maxBytesCount = encoding.GetMaxByteCount(str.Length);
            Ptr = Marshal.AllocHGlobal(maxBytesCount);
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

        Marshal.FreeHGlobal(Ptr);
    }
}
