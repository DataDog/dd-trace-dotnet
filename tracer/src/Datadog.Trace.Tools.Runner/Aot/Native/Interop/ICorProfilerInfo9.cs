// <copyright file="ICorProfilerInfo9.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo9 : ICorProfilerInfo8
{
    public static new readonly Guid Guid = new("008170DB-F8CC-4796-9A51-DC8AA0B47012");

    // Given functionId + rejitId, enumerate the native code start address of all jitted versions of this code that currently exist
    HResult GetNativeCodeStartAddresses(FunctionId functionID, ReJITId reJitId, uint cCodeStartAddresses, uint* pcCodeStartAddresses, uint* codeStartAddresses)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo9.GetNativeCodeStartAddresses");
        return HResult.E_NOTIMPL;
    }

    // Given the native code start address, return the native->IL mapping information for this jitted version of the code
    HResult GetILToNativeMapping3(uint* pNativeCodeStartAddress, uint cMap, uint* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP* map)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo9.GetILToNativeMapping3");
        return HResult.E_NOTIMPL;
    }

    // Given the native code start address, return the blocks of virtual memory that store this code (method code is not necessarily stored in a single contiguous memory region)
    HResult GetCodeInfo4(uint* pNativeCodeStartAddress, uint cCodeInfos, uint* pcCodeInfos, COR_PRF_CODE_INFO* codeInfos)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo9.GetCodeInfo4");
        return HResult.E_NOTIMPL;
    }
}
#endif
