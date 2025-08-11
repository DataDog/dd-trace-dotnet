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
        if (string.IsNullOrEmpty(str))
        {
            Ptr = IntPtr.Zero;
            Length = UIntPtr.Zero;
        }
        else
        {
            var encoding = StringEncoding.UTF8;
            var maxBytesCount = encoding.GetMaxByteCount(str!.Length);
            Ptr = Marshal.AllocHGlobal(maxBytesCount);
            unsafe
            {
                fixed (char* strPtr = str)
                {
                    try
                    {
                        Length = (nuint)encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                    }
                    catch
                    {
                        Marshal.FreeHGlobal(Ptr);
                        Ptr = IntPtr.Zero;
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
