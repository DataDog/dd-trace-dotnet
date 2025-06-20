// <copyright file="FFIVec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct FFIVec
{
    internal readonly IntPtr Data;
    internal readonly nuint Length;
    internal readonly nuint Capacity;

    public string ToUtf8String()
    {
        unsafe
        {
#if NETCOREAPP
            var messageBytes = new ReadOnlySpan<byte>(Data.ToPointer(), (int)Length);
            return Encoding.UTF8.GetString(messageBytes);
#else
            return Encoding.UTF8.GetString((byte*)Data.ToPointer(), (int)Length);
#endif
        }
    }
}
