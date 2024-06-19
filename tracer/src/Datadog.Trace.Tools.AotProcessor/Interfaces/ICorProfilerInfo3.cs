namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

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
    HResult EnumJITedFunctions(out void* ppEnum);

    HResult RequestProfilerDetach(int dwExpectedCompletionMilliseconds);

    HResult SetFunctionIDMapper2(
                delegate* unmanaged[Stdcall]<FunctionId, void*, int*, nint> pFunc,
                void* clientData);

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
                out uint pBufferOffset);

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
                void* pFuncTailcall3);

    HResult SetEnterLeaveFunctionHooks3WithInfo(
                void* pFuncEnter3WithInfo,
                void* pFuncLeave3WithInfo,
                void* pFuncTailcall3WithInfo);

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
                COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo);

    /*
     * The profiler can call GetFunctionLeave3Info to gather frame info and return value
     * in FunctionLeave3WithInfo callback.
     */
    HResult GetFunctionLeave3Info(
                FunctionId functionId,
                COR_PRF_ELT_INFO eltInfo,
                out COR_PRF_FRAME_INFO pFrameInfo,
                out COR_PRF_FUNCTION_ARGUMENT_RANGE pRetvalRange);

    /*
     * The profiler can call GetFunctionTailcall3Info to gather frame info in
     * FunctionTailcall3WithInfo callback.
     */
    HResult GetFunctionTailcall3Info(
                FunctionId functionId,
                COR_PRF_ELT_INFO eltInfo,
                out COR_PRF_FRAME_INFO pFrameInfo);

    HResult EnumModules(out void* ppEnum);

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
        char* szVersionString);

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
                    out void* ppAddress);

    /*
     * GetAppDomainsContainingModule returns the AppDomainIDs in which the
     * given module has been loaded
     */
    HResult GetAppDomainsContainingModule(
                ModuleId moduleId,
                uint cAppDomainIds,
                out uint pcAppDomainIds,
                AppDomainId* appDomainIds);

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
                out byte* ppBaseLoadAddress,
                uint cchName,
                out uint pcchName,
                char* szName,
                out AssemblyId pAssemblyId,
                out int pdwModuleFlags);
}
