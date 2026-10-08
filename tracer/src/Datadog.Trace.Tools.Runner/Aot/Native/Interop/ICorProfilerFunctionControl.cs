// <copyright file="ICorProfilerFunctionControl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerFunctionControl : IUnknown
{
    public static new readonly Guid Guid = new("F0963021-E1EA-4732-8581-E01B0BD3C0C6");

    HResult SetCodegenFlags(int flags)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerFunctionControl.SetCodegenFlags");
        return HResult.E_NOTIMPL;
    }

    HResult SetILFunctionBody(uint cbNewILMethodHeader, IntPtr pbNewILMethodHeader)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerFunctionControl.SetILFunctionBody");
        return HResult.E_NOTIMPL;
    }

    HResult SetILInstrumentedCodeMap(uint cILMapEntries, CorIlMap* rgILMapEntries)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerFunctionControl.SetILInstrumentedCodeMap");
        return HResult.E_NOTIMPL;
    }
}
#endif
