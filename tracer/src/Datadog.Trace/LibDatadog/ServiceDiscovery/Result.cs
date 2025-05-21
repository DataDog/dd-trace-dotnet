// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1201 // Elements should be documented
#pragma warning disable SA1602 // Elements should be documented
#pragma warning disable SA1649 // Elements should be documented
[StructLayout(LayoutKind.Explicit)]
public struct TracerMemfdHandleResult
{
    [FieldOffset(0)]
    public ResultTag Tag;

    [FieldOffset(4)] // Ensure proper alignment
    public TracerMemfdHandle Ok;

    [FieldOffset(4)]
    public Error Err;
}

public enum ResultTag
{
    Ok,
    Err
}

[StructLayout(LayoutKind.Sequential)]
public struct Error
{
    public CharSlice ErrorMessage;

    public string Message()
    {
        if (ErrorMessage.Ptr == IntPtr.Zero)
        {
            return string.Empty;
        }

        var bytes = new byte[ErrorMessage.Length.ToUInt32()];
        Marshal.Copy(ErrorMessage.Ptr, bytes, 0, bytes.Length);
        var errorMessage = Encoding.UTF8.GetString(bytes);
        // NativeInterop.Ddcommon.DropCharSlice(ref message);
        return errorMessage;
    }

    // Clean up the error using the FFI function
    public void Dispose()
    {
        // NativeMethods.ddog_Error_drop(ref this);
    }
}

[StructLayout(LayoutKind.Sequential)]
#pragma warning disable SA1401
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public struct CharSlice
{
    public IntPtr Ptr; // const char*

    public UIntPtr Length; // size_t

    public static CharSlice CreateCharSlice(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new CharSlice
            {
                Ptr = IntPtr.Zero,
                Length = UIntPtr.Zero
            };
        }

        // Allocate with extra space for alignment
        var utf8Bytes = Encoding.UTF8.GetBytes(str); // No null-terminator
        var unmanagedPtr = Marshal.AllocHGlobal(utf8Bytes.Length);
        Marshal.Copy(utf8Bytes, 0, unmanagedPtr, utf8Bytes.Length);

        return new CharSlice
        {
            Ptr = unmanagedPtr,
            Length = new UIntPtr((uint)utf8Bytes.Length)
        };
    }

    public static void FreeCharSlice(CharSlice slice)
    {
        if (slice.Ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(slice.Ptr);
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1600
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
#pragma warning restore SA1401
