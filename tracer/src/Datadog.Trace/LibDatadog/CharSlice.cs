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
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/slice.rs#L51
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
        if (str == null)
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

    public override string ToString()
    {
        unsafe
        {
            return StringEncoding.UTF8.GetString((byte*)Ptr, (int)Len);
        }
    }
}
