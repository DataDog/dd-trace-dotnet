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
            Ptr = Marshal.AllocHGlobal(maxBytesCount + 1); // Make space for null character.
            unsafe
            {
                fixed (char* strPtr = str)
                {
                    try
                    {
                        int byteCount = encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                        ((byte*)Ptr)[byteCount] = 0;
                        Length = (nuint)byteCount;
                    }
                    catch
                    {
                        Marshal.FreeHGlobal(Ptr);
                        Ptr = IntPtr.Zero;
                        Length = UIntPtr.Zero;
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
