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
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ByteSlice
{
    /// <summary>
    /// Pointer to the start of the slice.
    /// </summary>
    internal IntPtr Ptr;

    /// <summary>
    /// Length of the slice.
    /// </summary>
    internal UIntPtr Len;
}
