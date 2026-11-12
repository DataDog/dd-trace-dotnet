// <copyright file="ICorProfilerModuleEnum.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerModuleEnum : IUnknown
{
    HResult Skip(uint celt)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerModuleEnum.Skip");
        return HResult.E_NOTIMPL;
    }

    HResult Reset()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerModuleEnum.Reset");
        return HResult.E_NOTIMPL;
    }

    HResult Clone(out void* ppEnum)
    {
        ppEnum = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerModuleEnum.Clone");
        return HResult.E_NOTIMPL;
    }

    HResult GetCount(out uint pcelt)
    {
        pcelt = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerModuleEnum.GetCount");
        return HResult.E_NOTIMPL;
    }

    HResult Next(uint celt, ModuleId* ids, out uint pceltFetched)
    {
        pceltFetched = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerModuleEnum.Next");
        return HResult.E_NOTIMPL;
    }
}
#endif
