// <copyright file="ICorProfilerCallback3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerCallback3 : ICorProfilerCallback2
{
    public static new readonly Guid Guid = Guid.Parse("4FD2ED52-7731-4b8d-9469-03D2CC3086C5");
    HResult InitializeForAttach(
        IntPtr pCorProfilerInfoUnk,
        IntPtr pvClientData,
        uint cbClientData)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback3.InitializeForAttach");
        return HResult.E_NOTIMPL;
    }

    HResult ProfilerAttachComplete()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback3.ProfilerAttachComplete");
        return HResult.E_NOTIMPL;
    }

    HResult ProfilerDetachSucceeded()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerCallback3.ProfilerDetachSucceeded");
        return HResult.E_NOTIMPL;
    }
}
#endif
