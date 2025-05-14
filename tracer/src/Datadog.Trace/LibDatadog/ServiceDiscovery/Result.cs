// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

[StructLayout(LayoutKind.Sequential)]
#pragma warning disable SA1649
internal struct TracerMemfdHandle
#pragma warning restore SA1649
{
    internal int Fd;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Result
{
    internal bool IsOk;
    internal TracerMemfdHandle Value;
    internal IntPtr Error; // Pointer to error message if IsOk is false
}

[StructLayout(LayoutKind.Sequential)]
internal struct CharSlice
{
    internal IntPtr Ptr; // const char*
    internal UIntPtr Length; // size_t

    public static CharSlice CreateCharSlice(string str)
    {
        if (str == null)
        {
            return new CharSlice { Ptr = IntPtr.Zero, Length = UIntPtr.Zero };
        }

        var utf8Bytes = Encoding.UTF8.GetBytes(str); // No null-terminator
        var unmanagedPtr = Marshal.AllocHGlobal(utf8Bytes.Length);
        Marshal.Copy(utf8Bytes, 0, unmanagedPtr, utf8Bytes.Length);

        return new CharSlice
        {
            Ptr = unmanagedPtr,
            Length = (UIntPtr)utf8Bytes.Length
        };
    }

    public static void FreeCharSlice(CharSlice slice)
    {
        if (slice.Ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(slice.Ptr);
        }
    }
}
