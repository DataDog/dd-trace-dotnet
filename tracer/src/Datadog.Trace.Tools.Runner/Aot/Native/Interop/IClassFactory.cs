// <copyright file="IClassFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal interface IClassFactory : IUnknown
{
    public static new readonly Guid Guid = new("00000001-0000-0000-C000-000000000046");

    HResult CreateInstance(IntPtr outer, in Guid guid, out IntPtr instance)
    {
        instance = default;
        NativeStubDiagnostics.NotImplemented("IClassFactory.CreateInstance");
        return HResult.E_NOTIMPL;
    }

    HResult LockServer(bool @lock)
    {
        NativeStubDiagnostics.NotImplemented("IClassFactory.LockServer");
        return HResult.E_NOTIMPL;
    }
}
#endif
