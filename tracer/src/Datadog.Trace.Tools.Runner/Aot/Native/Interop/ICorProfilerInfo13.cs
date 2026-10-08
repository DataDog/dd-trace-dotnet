// <copyright file="ICorProfilerInfo13.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo13 : ICorProfilerInfo12
{
    public static new readonly Guid Guid = new("6E6C7EE2-0701-4EC2-9D29-2E8733B66934");

    HResult CreateHandle(
        ObjectId @object,
        COR_PRF_HANDLE_TYPE type,
        out ObjectHandleId pHandle)
    {
        pHandle = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo13.CreateHandle");
        return HResult.E_NOTIMPL;
    }

    HResult DestroyHandle(
        ObjectHandleId handle)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo13.DestroyHandle");
        return HResult.E_NOTIMPL;
    }

    HResult GetObjectIDFromHandle(
        ObjectHandleId handle,
        out ObjectId pObject)
    {
        pObject = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo13.GetObjectIDFromHandle");
        return HResult.E_NOTIMPL;
    }
}
#endif
