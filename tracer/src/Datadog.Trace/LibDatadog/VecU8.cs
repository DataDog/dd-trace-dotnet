// <copyright file="VecU8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/vec.rs#L19
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VecU8
{
    public nint Ptr; // const uint8_t*

    public nuint Length; // size_t

    public nuint Capacity; // size_t
}
