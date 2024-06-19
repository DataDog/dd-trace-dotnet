namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo : IUnknown
{
    public static readonly new Guid Guid = new("28B5557D-3F3F-48b4-90B2-5F9EEA2F6C48");

    /*
     * The code profiler calls GetClassFromObject to obtain the ClassId of an
     * object given its ObjectId.
     */
    HResult GetClassFromObject(ObjectId ObjectId, out ClassId pClassId);

    /*
     * V2 MIGRATION WARNING - DOES NOT WORK FOR GENERIC TYPES
     *
     * This function will be removed in a future release, so
     * use GetClassFromTokenAndTypeArgs for all types.
     */
    HResult GetClassFromToken(
                ModuleId ModuleId,
                MdTypeDef typeDef,
                out ClassId pClassId);

    /*
     * V2 MIGRATION WARNING - WILL NOT WORK WITH .NET FRAMEWORK
     * FUNCTIONS
     *
     * This function will be removed in a future release; use GetCodeInfo2
     * in all cases.
     */
    HResult GetCodeInfo(
                FunctionId FunctionId,
                out byte* pStart,
                out uint pcSize);

    /*
     * RECOMMENDATION: USE GetEventMask2 INSTEAD.  WHILE THIS METHOD CONTINUES TO
     * TO WORK, GetEventMask2 PROVIDES MORE FUNCTIONALITY.
     *
     * The code profiler calls GetEventMask to obtain the current event
     * categories for which it is to receive event notifications from the COM+
     * Runtime.
     */
    HResult GetEventMask(
                out int pdwEvents);

    /*
     * The code profiler calls GetFunctionFromIP to map an instruction pointer
     * in managed code to a FunctionId.
     */
    HResult GetFunctionFromIP(
                nint ip,
                out FunctionId pFunctionId);

    /*
     * V2 MIGRATION WARNING - WILL NOT WORK FOR GENERIC FUNCTIONS OR
     * FUNCTIONS ON GENERIC TYPES
     *
     * This function will be removed in a future release, so use
     * GetFunctionFromTokenAndTypeArgs for all functions.
     */
    HResult GetFunctionFromToken(
                ModuleId ModuleId,
                MdToken token,
                out FunctionId pFunctionId);

    /*
     * The code profiler calls GetHandleFromThread to map a ThreadId to a Win32
     * thread handle. The profiler must call DuplicateHandle on the handle
     * before using it.
     */
    HResult GetHandleFromThread(
                ThreadId ThreadId,
                out nint phThread);

    /*
     * The code profiler calls GetObjectSize to obtain the size of an object.
     * Note that types like arrays and strings may have a different size for each object.
     *
     * THIS API IS OBSOLETE. It does not work for objects >4GB on 64-bit platforms.
     * Use ICorProfilerInfo4::GetObjectSize2 instead.
     */
    HResult GetObjectSize(
                ObjectId ObjectId,
                out uint pcSize);

    /*
     * This will return S_OK if the ClassId provided is an array class, and will
     * fill out the information for any non-null out params.  S_FALSE will be
     * returned if the ClassId is not an array.
     *
     * ClassId       : the ClassId to return information about
     * pBaseElemType : the array's base element type
     * pBaseClassId  : the base ClassId if the element type == ELEMENT_TYPE_CLASS
     * pcRank        : the number of dimensions of the array
     */
    HResult IsArrayClass(
                ClassId ClassId,
                out CorElementType pBaseElemType,
                out ClassId pBaseClassId,
                out uint pcRank);

    /*
     * The code profiler calls GetThreadInfo to obtain the current Win32 thread ID for
     * the specified thread.
     */
    HResult GetThreadInfo(
                ThreadId ThreadId,
                out int pdwWin32ThreadId);

    /*
     * The code profiler calls GetCurrentThreadId to get the managed thread ID
     * for the current thread.
     *
     * NOTE: GetCurrentThreadId may return CORPROF_E_NOT_MANAGED_THREAD if the
     * current thread is an internal runtime thread, and the returned value of
     * pThreadId will be NULL.
     */
    HResult GetCurrentThreadId(
                out ThreadId pThreadId);

    /*
     * V2 MIGRATION NOTE - More information is available for generic types
     * from GetClassIdInfo2.
     *
     * Returns the parent module a class is defined in, along with the
     * metadata token for the class.  One can call GetModuleMetaData
     * to obtain the metadata interface for a given module.  The token
     * can then be used to access the metadata for this class.
     */
    HResult GetClassIdInfo(
                ClassId ClassId,
                out ModuleId pModuleId,
                out MdTypeDef pTypeDefToken);

    /*
     * Return the parent class for a given function.  Also return the metadata
     * token which can be used to read the metadata.
     *
     * V2 MIGRATION WARNING - LESS INFORMATION FOR GENERIC CLASSES
     * The ClassId of a function on a generic class may not be obtainable without
     * more context about the use of the function. In this case, *pClassId will be 0;
     * try using GetFunctionInfo2 with a COR_PRF_FRAME_INFO to give more context.
     */
    HResult GetFunctionInfo(
                FunctionId FunctionId,
                out ClassId pClassId,
                out ModuleId pModuleId,
                out MdToken pToken);

    /*
     * RECOMMENDATION: USE SetEventMask2 INSTEAD.  WHILE THIS METHOD CONTINUES TO
     * TO WORK, SetEventMask2 PROVIDES MORE FUNCTIONALITY.
     *
     * The code profiler calls SetEventMask to set the event categories for
     * which it is set to receive notification from the CLR.
     */
    HResult SetEventMask(CorPrfMonitor dwEvents);

    /*
     * The code profiler calls SetFunctionHooks to specify handlers
     * for FunctionEnter, FunctionLeave, and FunctionTailcall.
     *
     * Note that only one set of callbacks may be active at a time. Thus,
     * if a profiler calls SetEnterLeaveFunctionHooks, SetEnterLeaveFunctionHooks2
     * and SetEnterLeaveFunctionHooks3(WithInfo), then SetEnterLeaveFunctionHooks3(WithInfo)
     * wins.  SetEnterLeaveFunctionHooks2 takes precedence over SetEnterLeaveFunctionHooks
     * when both are set.
     *
     * Each function pointer may be null to disable that callback.
     *
     * SetEnterLeaveFunctionHooks may only be called from the
     * profiler's Initialize() callback.
     */
    HResult SetEnterLeaveFunctionHooks(
                void* pFuncEnter,
                void* pFuncLeave,
                void* pFuncTailcall);

    /*
     * This is used for mapping FunctionIds to alternative values that will be
     * passed to the callbacks
     */
    HResult SetFunctionIdMapper(
                void* pFunc);

    /*
     * For a given function, retrieve the token value and an instance of the
     * meta data interface which can be used against this token.
     */
    HResult GetTokenAndMetaDataFromFunction(
                FunctionId FunctionId,
                out Guid riid,
                out void* ppImport,
                out MdToken pToken);

    /*
     * Retrieve information about a given module.
     *
     * When the module is loaded from disk, the name returned will be the filename;
     * otherwise, the name will be the name from the metadata Module table (i.e.,
     * the same as the managed System.Reflection.Module.ScopeName).
     *
     * NOTE: While this function may be called as soon as the ModuleId is alive,
     * the AssemblyId of the containing assembly will not be available until the
     * ModuleAttachedToAssembly callback.
     *
     * NOTE: More information is available by using ICorProfilerInfo3::GetModuleInfo2 instead.
     */
    HResult GetModuleInfo(
                ModuleId ModuleId,
                out nint ppBaseLoadAddress,
                uint cchName,
                out uint pcchName,
                char* szName,
                out AssemblyId pAssemblyId);

    /*
     * Get a metadata interface instance which maps to the given module.
     * One may ask for the metadata to be opened in read+write mode, but
     * this will result in slower metadata execution of the program, because
     * changes made to the metadata cannot be optimized as they were from
     * the compiler.
     *
     * NOTE: Some modules (such as resource modules) have no metadata. In
     * those cases, GetModuleMetaData will return S_FALSE, and a NULL
     * IUnknown.
     *
     * NOTE: the only values valid for dwOpenFlags are ofRead and ofWrite.
     */
    HResult GetModuleMetaData(
                ModuleId ModuleId,
                CorOpenFlags dwOpenFlags,
                Guid riid,
                out IntPtr ppOut);

    /*
     * Retrieve a pointer to the body of a method starting at it's header.
     * A method is scoped by the module it lives in.  Because this function
     * is designed to give a tool access to IL before it has been loaded
     * by the Runtime, it uses the metadata token of the method to find
     * the instance desired.
     *
     * GetILFunctionBody can return CORPROF_E_FUNCTION_NOT_IL if the methodId
     * points to a method without any IL (such as an abstract method, or a
     * P/Invoke method).
     */
    HResult GetILFunctionBody(
                ModuleId ModuleId,
                MdMethodDef methodId,
                out byte* ppMethodHeader,
                out uint pcbMethodSize);

    /*
     * IL method bodies must be located as RVA's to the loaded module, which
     * means they come after the module within 4 gb.  In order to make it
     * easier for a tool to swap out the body of a method, this allocator
     * will ensure memory is allocated within that range.
     */
    HResult GetILFunctionBodyAllocator(
                ModuleId ModuleId,
                out void* ppMalloc);

    /*
     * Replaces the method body for a function in a module.  This will replace
     * the RVA of the method in the metadata to point to this new method body,
     * and adjust any internal data structures as required.  This function can
     * only be called on those methods which have never been compiled by a JITTER.
     * Please use the GetILFunctionAllocator to allocate space for the new method to
     * ensure the buffer is compatible.
     */
    HResult SetILFunctionBody(
                ModuleId ModuleId,
                MdMethodDef methodid,
                byte pbNewILMethodHeader);

    /*
     * Retrieve app domain information given its id.
     */
    HResult GetAppDomainInfo(
                AppDomainId appDomainId,
                uint cchName,
                out uint pcchName,
                char* szName,
                out ProcessId pProcessId);

    /*
     * Retrieve information about an assembly given its ID.
     */
    HResult GetAssemblyInfo(
                AssemblyId assemblyId,
                uint cchName,
                out uint pcchName,
                char* szName,
                out AppDomainId pAppDomainId,
                out ModuleId pModuleId);

    /*
     * V2 MIGRATION WARNING: DEPRECATED.  Returns E_NOTIMPL always.
     *
     * See ICorProfilerInfo4::RequestReJIT instead
     *
     */
    HResult SetFunctionReJIT(
                FunctionId functionId);

    /*
     * ForceGC forces a GC to occur within the runtime.
     *
     * NOTE: This method needs to be called from a thread that does not have any
     * profiler callbacks on its stack. The most convenient way to implement this is
     * to create a separate thread within the profiler and have it call ForceGC when
     * signalled.
     */
    HResult ForceGC();

    /*
     *
     * V2 MIGRATION NOTE - Calling SetILInstrumentedCodeMap on any one
     * of the multiple FunctionIds that represent a generic function in a given
     * AppDomain will affect all instantiations of that function in the AppDomain.
     *
     * fStartJit should be set to true the first time this function is called for
     * a given FunctionId, and false thereafter.
     *
     * The format of the map is as follows:
     *      The debugger will assume that each oldOffset refers to an IL offset
     *  within the original, unmodified IL code.  newOffset refers to the corresponding
     *  IL offset within the new, instrumented code.
     *
     * The map should be sorted in increasing order. For stepping to work properly:
     * - Instrumented IL should not be reordered (so both old & new are sorted)
     * - original IL should not be removed
     * - the map should include entries to map all of the sequence points from the pdb.
     *
     * The map does not interpolate missing entries. So given the following map:
     * (0 old, 0  new)
     * (5 old, 10 new)
     * (9 old, 20 new)
     * - An old offset of 0,1,2,3,4 will be mapped to a new offset of 0
     * - An old offset of 5,6,7, or 8 will be mapped to new offset 10.
     * - An old offset of 9 or higher will be mapped to new offset 20.
     * - A new offset of 0, 1,...8,9 will be mapped to old offset 0
     * - A new offset of 10,11,...18,19 will be mapped to old offset 5.
     * - A new offset of 20 or higher will be mapped to old offset 9.
     *
     */
    HResult SetILInstrumentedCodeMap(
                FunctionId FunctionId,
                int fStartJit,
                uint cILMapEntries,
                CorIlMap* rgILMapEntries);

    /*
     * DEPRECATED.
     */
    HResult GetInprocInspectionInterface(
                out void* ppicd);

    /*
     * DEPRECATED.
     */
    HResult GetInprocInspectionIThisThread(
                out void* ppicd);

    /*
     * This will return the ContextID currently associated with the calling
     * runtime thread.  This will set pContextId to NULL if the calling thread
     * is not a runtime thread.
     */
    HResult GetThreadContext(
                ThreadId ThreadId,
                out ContextId pContextId);

    /*
     * DEPRECATED.
     */
    HResult BeginInprocDebugging(
        int fThisThreadOnly,
        out int pdwProfilerContext);

    /*
     * DEPRECATED.
     */
    HResult EndInprocDebugging(
                int dwProfilerContext);

    /*
     * GetILToNativeMapping returns a map from IL offsets to native
     * offsets for this code. An array of COR_PROF_IL_TO_NATIVE_MAP
     * structs will be returned, and some of the ilOffsets in this array
     * may be the values specified in CorDebugIlToNativeMappingTypes.
     */
    HResult GetILToNativeMapping(
                FunctionId FunctionId,
                uint cMap,
                out uint pcMap,
                CorDebugIlToNativeMap* map);
}
