// <copyright file="CString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog;

[StructLayout(LayoutKind.Sequential)]
internal struct CString
{
    public nint Ptr;       // char*
    public nuint Length;   // size of the string, excluding null terminator

    public string ToUtf8String() => NativeStringHelper.GetString(Ptr, Length);
}
