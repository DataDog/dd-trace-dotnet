// <copyright file="ICorProfilerCallback8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback8 : ICorProfilerCallback7
{
    public static new readonly Guid Guid = Guid.Parse("5BED9B15-C079-4D47-BFE2-215A140C07E0");

    // This event is triggered whenever a dynamic method is jit compiled.
    // These include various IL Stubs and LCG Methods.
    // The goal is to provide profiler writers with enough information to identify
    // it to users as beyond unknown code addresses.
    // Note: FunctionID's provided here cannot be used to resolve to their metadata
    //       tokens since dynamic methods have no metadata.
    //
    // Documentation Note: pILHeader is only valid during the callback

    HResult DynamicMethodJITCompilationStarted(
        FunctionId functionId,
        int fIsSafeToBlock,
        byte* pILHeader,
        uint cbILHeader)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback8.DynamicMethodJITCompilationStarted");
        return HResult.E_NOTIMPL;
    }

    HResult DynamicMethodJITCompilationFinished(
        FunctionId functionId,
        HResult hrStatus,
        int fIsSafeToBlock)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback8.DynamicMethodJITCompilationFinished");
        return HResult.E_NOTIMPL;
    }
}
#endif
