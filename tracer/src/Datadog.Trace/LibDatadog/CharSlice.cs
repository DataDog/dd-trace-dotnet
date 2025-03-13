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
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CharSlice
{
    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal IntPtr Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal UIntPtr Len;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharSlice"/> struct.
    /// This can be further optimized if we can avoid copying the string to unmanaged memory.
    /// </summary>
    /// <param name="str">The string to copy into memory.</param>
    internal CharSlice(string? str)
    {
        // copy over str to unmanaged memory
        if (str == null)
        {
            Ptr = IntPtr.Zero;
            Len = UIntPtr.Zero;
        }
        else
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            Ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Ptr, bytes.Length);
            Len = (UIntPtr)bytes.Length;
        }
    }
}
