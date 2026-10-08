// <copyright file="ICorProfilerInfo3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface ICorProfilerInfo3 : ICorProfilerInfo2
{
    public static new readonly Guid Guid = new("B555ED4F-452A-4E54-8B39-B5360BAD32A0");

    /*
     * Returns an enumerator for all previously jitted functions. May overlap with
     * functions previously reported via CompilationStarted callbacks.
     * NOTE: The returned enumeration will only include '0' for the value of the
     * COR_PRF_FUNCTION::reJitId field.  If you require valid COR_PRF_FUNCTION::reJitId values, use
     * ICorProfilerInfo4::EnumJITedFunctions2.
     */
    HResult EnumJITedFunctions(out void* ppEnum)
    {
        ppEnum = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.EnumJITedFunctions");
        return HResult.E_NOTIMPL;
    }

    HResult RequestProfilerDetach(int dwExpectedCompletionMilliseconds)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.RequestProfilerDetach");
        return HResult.E_NOTIMPL;
    }

    HResult SetFunctionIDMapper2(
                delegate* unmanaged[Stdcall]<FunctionId, void*, int*, nint> pFunc,
                void* clientData)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.SetFunctionIDMapper2");
        return HResult.E_NOTIMPL;
    }

    /*
     * GetStringLayout2 returns detailed information about how string objects are stored.
     *
     * *pStringLengthOffset is the offset (from the ObjectID pointer) to a int that
     * stores the length of the string itself
     *
     * *pBufferOffset is the offset (from the ObjectID pointer) to the actual buffer
     * of wide characters
     *
     * Strings may or may not be null-terminated.
     */
    HResult GetStringLayout2(
                out uint pStringLengthOffset,
                out uint pBufferOffset)
    {
        pStringLengthOffset = default;
        pBufferOffset = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetStringLayout2");
        return HResult.E_NOTIMPL;
    }

    /*
     * The code profiler calls SetFunctionHooks3 to specify handlers
     * for FunctionEnter3, FunctionLeave3, and FunctionTailcall3, and calls
     * SetFunctionHooks3WithInfo to specify handlers for FunctionEnter3WithInfo,
     * FunctionLeave3WithInfo, and FunctionTailcall3WithInfo.
     *
     * Note that only one set of callbacks may be active at a time. Thus,
     * if a profiler calls SetEnterLeaveFunctionHooks, SetEnterLeaveFunctionHooks2
     * and SetEnterLeaveFunctionHooks3(WithInfo), then SetEnterLeaveFunctionHooks3(WithInfo)
     * wins.  SetEnterLeaveFunctionHooks2 takes precedence over SetEnterLeaveFunctionHooks
     * when both are set.
     *
     * Each function pointer may be null to disable that callback.
     *
     * SetEnterLeaveFunctionHooks3(WithInfo) may only be called from the
     * profiler's Initialize() callback.
     */
    HResult SetEnterLeaveFunctionHooks3(
                void* pFuncEnter3,
                void* pFuncLeave3,
                void* pFuncTailcall3)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.SetEnterLeaveFunctionHooks3");
        return HResult.E_NOTIMPL;
    }

    HResult SetEnterLeaveFunctionHooks3WithInfo(
                void* pFuncEnter3WithInfo,
                void* pFuncLeave3WithInfo,
                void* pFuncTailcall3WithInfo)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.SetEnterLeaveFunctionHooks3WithInfo");
        return HResult.E_NOTIMPL;
    }

    /*
     * The profiler can call GetFunctionEnter3Info to gather frame info and argument info
     * in FunctionEnter3WithInfo callback. The profiler needs to allocate sufficient space
     * for COR_PRF_FUNCTION_ARGUMENT_INFO of the function it's inspecting and indicate the
     * size in a ULONG pointed by pcbArgumentInfo.
     */
    HResult GetFunctionEnter3Info(
                FunctionId functionId,
                COR_PRF_ELT_INFO eltInfo,
                out COR_PRF_FRAME_INFO pFrameInfo,
                int* pcbArgumentInfo,
                COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo)
    {
        pFrameInfo = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetFunctionEnter3Info");
        return HResult.E_NOTIMPL;
    }

    /*
     * The profiler can call GetFunctionLeave3Info to gather frame info and return value
     * in FunctionLeave3WithInfo callback.
     */
    HResult GetFunctionLeave3Info(
                FunctionId functionId,
                COR_PRF_ELT_INFO eltInfo,
                out COR_PRF_FRAME_INFO pFrameInfo,
                out COR_PRF_FUNCTION_ARGUMENT_RANGE pRetvalRange)
    {
        pFrameInfo = default;
        pRetvalRange = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetFunctionLeave3Info");
        return HResult.E_NOTIMPL;
    }

    /*
     * The profiler can call GetFunctionTailcall3Info to gather frame info in
     * FunctionTailcall3WithInfo callback.
     */
    HResult GetFunctionTailcall3Info(
                FunctionId functionId,
                COR_PRF_ELT_INFO eltInfo,
                out COR_PRF_FRAME_INFO pFrameInfo)
    {
        pFrameInfo = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetFunctionTailcall3Info");
        return HResult.E_NOTIMPL;
    }

    HResult EnumModules(out void* ppEnum)
    {
        ppEnum = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.EnumModules");
        return HResult.E_NOTIMPL;
    }

    /*
     * The profiler can call GetRuntimeInformation to query CLR version information.
     * Passing NULL to any parameter is acceptable except pcchVersionString cannot
     * be NULL if szVersionString is not NULL.
     */
    HResult GetRuntimeInformation(
        out ushort pClrInstanceId,
        out COR_PRF_RUNTIME_TYPE pRuntimeType,
        out ushort pMajorVersion,
        out ushort pMinorVersion,
        out ushort pBuildNumber,
        out ushort pQFEVersion,
        uint cchVersionString,
        out uint pcchVersionString,
        char* szVersionString)
    {
        pClrInstanceId = default;
        pRuntimeType = default;
        pMajorVersion = default;
        pMinorVersion = default;
        pBuildNumber = default;
        pQFEVersion = default;
        pcchVersionString = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetRuntimeInformation");
        return HResult.E_NOTIMPL;
    }

    /*
     * GetThreadStaticAddress2 gets the address of the home for the given
     * Thread static in the given Thread.
     *
     * This function may return CORPROF_E_DATAINCOMPLETE if the given static
     * has not been assigned a home in the given Thread.
     */
    HResult GetThreadStaticAddress2(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    AppDomainId appDomainId,
                    ThreadId threadId,
                    out void* ppAddress)
    {
        ppAddress = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetThreadStaticAddress2");
        return HResult.E_NOTIMPL;
    }

    /*
     * GetAppDomainsContainingModule returns the AppDomainIDs in which the
     * given module has been loaded
     */
    HResult GetAppDomainsContainingModule(
                ModuleId moduleId,
                uint cAppDomainIds,
                out uint pcAppDomainIds,
                AppDomainId* appDomainIds)
    {
        pcAppDomainIds = default;
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetAppDomainsContainingModule");
        return HResult.E_NOTIMPL;
    }

    /*
     * Retrieve information about a given module.
     *
     * When the module is loaded from disk, the name returned will be the filename;
     * otherwise, the name will be the name from the metadata Module table (i.e.,
     * the same as the managed System.Reflection.Module.ScopeName).
     *
     * *pdwModuleFlags will be filled in with a bitmask of values from COR_PRF_MODULE_FLAGS
     * that specify some properties of the module.
     *
     * NOTE: While this function may be called as soon as the moduleId is alive,
     * the AssemblyID of the containing assembly will not be available until the
     * ModuleAttachedToAssembly callback.
     *
     */
    HResult GetModuleInfo2(
                ModuleId moduleId,
                nint* ppBaseLoadAddress,
                uint cchName,
                uint* pcchName,
                char* szName,
                AssemblyId* pAssemblyId,
                int* pdwModuleFlags)
    {
        NativeStubDiagnostics.NotImplemented("ICorProfilerInfo3.GetModuleInfo2");
        return HResult.E_NOTIMPL;
    }
}
#endif
