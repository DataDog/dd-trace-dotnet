// <copyright file="CString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.LibDatadog;

[StructLayout(LayoutKind.Sequential)]
internal struct CString : IDisposable
{
    public nint Ptr; // char*
    public nuint Length; // size of the string, excluding null terminator

    internal CString(string? str)
    {
        if (StringUtil.IsNullOrEmpty(str))
        {
            Ptr = IntPtr.Zero;
            Length = UIntPtr.Zero;
        }
        else
        {
            var encoding = StringEncoding.UTF8;
            var maxBytesCount = encoding.GetMaxByteCount(str.Length);
            Ptr = Marshal.AllocHGlobal(maxBytesCount + 1); // +1 for null terminator
            unsafe
            {
                fixed (char* strPtr = str)
                {
                    try
                    {
                        int bytesWritten = (nuint)encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                        if (bytesWritten < 0 || bytesWritten > maxBytesCount)
                        {
                            Marshal.FreeHGlobal(Ptr);
                            Ptr = IntPtr.Zero;
                            Length = 0;
                            return;
                        }

                        Length = (nuint)bytesWritten
                        *((byte*)Ptr + Length) = 0; // Add null terminator
                    }
                    catch
                    {
                        Marshal.FreeHGlobal(Ptr);
                        Ptr = IntPtr.Zero;
                        Length = 0;
                    }
                }
            }
        }
    }

    public unsafe string ToUtf8String() => StringEncoding.UTF8.GetString((byte*)Ptr, (int)Length);

    public void Dispose()
    {
        if (Ptr == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(Ptr);
    }
}
