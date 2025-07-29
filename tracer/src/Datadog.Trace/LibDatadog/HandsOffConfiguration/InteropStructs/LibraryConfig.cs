// <copyright file="LibraryConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

[StructLayout(LayoutKind.Sequential)]
internal struct LibraryConfig
{
    public CString Name;      // ffi::CString
    public CString Value;     // ffi::CString
    public LibraryConfigSource Source;
    public CString ConfigId; // ffi::CString
}
