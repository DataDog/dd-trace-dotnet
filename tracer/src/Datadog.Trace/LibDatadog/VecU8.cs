// <copyright file="VecU8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/vec.rs#L19
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct VecU8
{
    public readonly nint Ptr; // const uint8_t*
    public readonly nuint Length; // size_t
    public readonly nuint Capacity; // size_t

    public string ToUtf8String()
    {
        unsafe
        {
#if NETCOREAPP
            var messageBytes = new ReadOnlySpan<byte>((void*)Ptr, (int)Length);
            return StringEncoding.UTF8.GetString(messageBytes);
#else
            return StringEncoding.UTF8.GetString((byte*)Ptr, (int)Length);
#endif
        }
    }
}
