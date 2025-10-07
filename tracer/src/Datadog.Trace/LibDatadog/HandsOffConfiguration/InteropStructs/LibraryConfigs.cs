// <copyright file="LibraryConfigs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

[StructLayout(LayoutKind.Sequential)]
internal struct LibraryConfigs
{
    public nint Ptr;       // const LibraryConfig*
    public nuint Length;
    public nuint Capacity;
    public CString Logs; // ffi::CString
}
