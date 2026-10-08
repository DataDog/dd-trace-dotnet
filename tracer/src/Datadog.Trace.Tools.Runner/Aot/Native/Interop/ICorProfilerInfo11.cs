// <copyright file="ICorProfilerInfo11.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo11 : ICorProfilerInfo10
{
    public static new readonly Guid Guid = new("06398876-8987-4154-B621-40A00D6E4D04");

    /*
     * Get environment variable for the running managed code.
     */
    HResult GetEnvironmentVariable(
        char* szName,
        uint cchValue,
        out uint pcchValue,
        char* szValue)
    {
        pcchValue = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo11.GetEnvironmentVariable");
        return HResult.E_NOTIMPL;
    }

    /*
     * Set environment variable for the running managed code.
     *
     * The code profiler calls this function to modify environment variables of the
     * current managed process. For example, it can be used in the profiler's Initialize()
     * or InitializeForAttach() callbacks.
     *
     * szName is the name of the environment variable, should not be NULL.
     *
     * szValue is the contents of the environment variable, or NULL if the variable should be deleted.
     */
    HResult SetEnvironmentVariable(
        char* szName,
        char* szValue)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo11.SetEnvironmentVariable");
        return HResult.E_NOTIMPL;
    }
}
#endif
