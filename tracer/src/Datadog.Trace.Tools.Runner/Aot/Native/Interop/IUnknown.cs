// <copyright file="IUnknown.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal interface IUnknown
{
    public static readonly Guid Guid = new("00000000-0000-0000-C000-000000000046");

    HResult QueryInterface(in Guid guid, out IntPtr ptr);
    int AddRef();
    int Release();
}
#endif
