// <copyright file="ByteSlice.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a slice of a byte array in memory.
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/slice.rs#L54
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ByteSlice
{
    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal nint Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal nuint Len;
}
