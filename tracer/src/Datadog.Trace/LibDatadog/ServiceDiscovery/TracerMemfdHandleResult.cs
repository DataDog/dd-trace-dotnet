// <copyright file="TracerMemfdHandleResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

// Do not use this type in x86 to map with libdatadog
[StructLayout(LayoutKind.Explicit)]
internal struct TracerMemfdHandleResult
{
    [FieldOffset(0)]
    public ResultTag Tag;

    // beware that offset 8 is only valid on x64 and would cause a crash if read on x86.
    [FieldOffset(8)]
    public TracerMemfdHandle Ok;

    [FieldOffset(8)]
    public Error Err;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Error
{
    public VecU8 ErrorMessage;

    internal static string ReadAndDrop(ref Error resultErr)
    {
        var message = resultErr.ErrorMessage;
        if (message.Length == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[(int)resultErr.ErrorMessage.Length];
        Marshal.Copy(message.Ptr, buffer, 0, (int)message.Length);

        var errorMessage = Encoding.UTF8.GetString(buffer);
        NativeInterop.Common.DropError(ref resultErr);
        return errorMessage;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CharSlice
{
    public IntPtr Ptr; // const char*

    public uint Length; // size_t

    public static CharSlice CreateCharSlice(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new CharSlice
            {
                Ptr = IntPtr.Zero,
                Length = 0
            };
        }

        // Allocate with extra space for alignment
        var utf8Bytes = Encoding.UTF8.GetBytes(str); // No null-terminator
        var unmanagedPtr = Marshal.AllocHGlobal(utf8Bytes.Length);
        Marshal.Copy(utf8Bytes, 0, unmanagedPtr, utf8Bytes.Length);

        return new CharSlice
        {
            Ptr = unmanagedPtr,
            Length = (uint)utf8Bytes.Length
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

[StructLayout(LayoutKind.Sequential)]
internal struct VecU8
{
    public IntPtr Ptr; // const uint8_t*

    public uint Length; // size_t

    public uint Capacity; // size_t
}
