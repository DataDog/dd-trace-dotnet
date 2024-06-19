namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo4 : ICorProfilerInfo3
{
    public static new readonly Guid Guid = new("0d8fdcaa-6257-47bf-b1bf-94dac88466ee");

    HResult EnumThreads(out IntPtr ppEnum);
    HResult InitializeCurrentThread();

    /*
     * Call RequestReJIT to have the runtime re-JIT a particular set of methods.
     * A code profiler can then adjust the code generated when the method is
     * re-JITed through the ICorProfilerFunctionControl interface.  This does
     * not impact currently executing methods, only future invocations.
     *
     * A return code of S_OK indicates that all of the requested methods were
     * attempted to be rejitted.   However, the profiler must implement
     * ICorProfilerCallback4::ReJITError to determine which of the methods were
     * successfully re-JITed.
     *
     * A failure return value (E_*) indicates some failure that prevents any
     * re-JITs.
     */
    HResult RequestReJIT(
                uint cFunctions,
                ModuleId* moduleIds,
                MdMethodDef* methodIds);

    /*
     * RequestRevert will instruct the runtime to revert to using/calling the
     * original method (original IL and flags) rather than whatever was
     * ReJITed.  This does not change any currently active methods, only future
     * invocations.
     *
     */
    HResult RequestRevert(
                uint cFunctions,
                ModuleId* moduleIds,
                MdMethodDef* methodIds,
                HResult* status);

    /*
     * Same as GetCodeInfo2, except instead of always returning the code info
     * associated with the original IL/function, you can request the code info
     * for a particular re-JITed version of a function.
     */
    HResult GetCodeInfo3(
                FunctionId functionID,
                ReJITId reJitId,
                uint cCodeInfos,
                out uint pcCodeInfos,
                COR_PRF_CODE_INFO* codeInfos);

    /*
     * Same as GetFunctionFromIP, but also returns which re-JITed version is
     * associated with the IP address.
     */
    HResult GetFunctionFromIP2(
                byte* ip,
                out FunctionId pFunctionId,
                out ReJITId pReJitId);

    /*
     * GetReJITIDs can be used to find all of the re-JITed versions of the
     * given function.
     */
    HResult GetReJITIDs(
                FunctionId functionId,
                uint cReJitIds,
                uint* pcReJitIds,
                ReJITId* reJitIds);

    /*
     * Same as GetILToNativeMapping, but allows the code profiler to specify
     * which re-JITed version it applies to.
     */
    HResult GetILToNativeMapping2(
                FunctionId functionId,
                ReJITId reJitId,
                uint cMap,
                uint* pcMap,
                COR_DEBUG_IL_TO_NATIVE_MAP* map);

    /*
     * Returns an enumerator for all previously jitted functions. May overlap with
     * functions previously reported via CompilationStarted callbacks.  The returned
     * enumeration will include values for the COR_PRF_FUNCTION::reJitId field
     */
    HResult EnumJITedFunctions2(out IntPtr ppEnum);

    /*
     * The code profiler calls GetObjectSize to obtain the size of an object.
     * Note that types like arrays and strings may have a different size for each object.
     */
    HResult GetObjectSize2(
                ObjectId objectId,
                out nint pcSize);
}
