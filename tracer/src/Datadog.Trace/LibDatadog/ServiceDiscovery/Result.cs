// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Core;

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

    [FieldOffset(8)] // Ensure proper alignment
    public TracerMemfdHandle Ok;

    [FieldOffset(8)]
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
    public VecU8 ErrorMessage;

    internal static string ReadAndDrop(ref Error resultErr)
    {
        var message = resultErr.ErrorMessage;
        if (message.Length == UIntPtr.Zero)
        {
            return string.Empty;
        }

        var buffer = new byte[(int)resultErr.ErrorMessage.Length];
        Marshal.Copy(message.Ptr, buffer, 0, (int)message.Length);

        var errorMessage = Encoding.UTF8.GetString(buffer);
        NativeInterop.Common.DropError(ref resultErr);
        return errorMessage;
    }

// Clean up the error using the FFI function
    public void Dispose()
    {
        var uintPtr = new UIntPtr(34);
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
}

[StructLayout(LayoutKind.Sequential)]
public struct VecU8
{
    public IntPtr Ptr; // const uint8_t*

    public UIntPtr Length; // size_t

    public UIntPtr Capacity; // size_t
}
#pragma warning restore SA1401
#pragma warning restore SA1600
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
