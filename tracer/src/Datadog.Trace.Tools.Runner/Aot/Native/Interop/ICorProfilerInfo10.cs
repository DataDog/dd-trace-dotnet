// <copyright file="ICorProfilerInfo10.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo10 : ICorProfilerInfo9
{
    public static new readonly Guid Guid = new("2F1B5152-C869-40C9-AA5F-3ABE026BD720");

    // Given an ObjectID, callback and clientData, enumerates each object reference (if any).
    HResult EnumerateObjectReferences(ObjectId objectId, delegate* unmanaged<ObjectId, ObjectId*, void*, int> callback, void* clientData)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.EnumerateObjectReferences");
        return HResult.E_NOTIMPL;
    }

    // Given an ObjectID, determines whether it is in a read only segment.
    HResult IsFrozenObject(ObjectId objectId, out int pbFrozen)
    {
        pbFrozen = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.IsFrozenObject");
        return HResult.E_NOTIMPL;
    }

    // Gets the value of the configured LOH Threshold.
    HResult GetLOHObjectSizeThreshold(out int pThreshold)
    {
        pThreshold = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.GetLOHObjectSizeThreshold");
        return HResult.E_NOTIMPL;
    }

    /*
     * This method will ReJIT the methods requested, as well as any inliners
     * of the methods requested.
     *
     * RequestReJIT does not do any tracking of inlined methods. The profiler
     * was expected to track inlining and call RequestReJIT for all inliners
     * to make sure every instance of an inlined method was ReJITted.
     * This poses a problem with ReJIT on attach, since the profiler was
     * not present to monitor inlining. This method can be called to guarantee
     * that the full set of inliners will be ReJITted as well.
     */
    HResult RequestReJITWithInliners(
        int dwRejitFlags,
        uint cFunctions,
        ModuleId* moduleIds,
        MdMethodDef* methodIds)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.RequestReJITWithInliners");
        return HResult.E_NOTIMPL;
    }

    // Suspend the runtime without performing a GC.
    HResult SuspendRuntime()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.SuspendRuntime");
        return HResult.E_NOTIMPL;
    }

    // Restart the runtime from a previous suspension.
    HResult ResumeRuntime()
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo10.ResumeRuntime");
        return HResult.E_NOTIMPL;
    }
}
#endif
