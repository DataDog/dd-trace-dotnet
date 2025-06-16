// <copyright file="CharSlice.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a slice of a UTF-8 encoded string in memory.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/slice.rs#L51
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CharSlice : IDisposable
{
    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal nint Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal nuint Len;

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
            // copy over str to unmanaged memory
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            Ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Ptr, bytes.Length);
            Len = (nuint)bytes.Length;
        }
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(Ptr);
    }
}
