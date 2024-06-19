namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerInfo2 : ICorProfilerInfo
{
    public static new readonly Guid Guid = new("CC0935CD-A518-487d-B0BB-A93214E65478");

    /*
     * The code profiler calls DoStackSnapshot to do sparse one-off stack snapshots.
     *
     * Passing NULL for thread yields a snapshot of the current thread. If a ThreadID
     * of a different thread is passed, the runtime will suspend that thread, perform
     * the snapshot, and resume.
     *
     * infoFlags come from the COR_PRF_SNAPSHOT_INFO enum.
     *
     * context is a platform-dependent CONTEXT structure, representing the complete
     * register context that the profiling API will use to seed the stack walk.  If this
     * is non-NULL, it must point to JITd or NGENd code, or else DoStackSnapshot
     * will return CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX.  Contexts are
     * only provided by profilers that hijack threads to force them to walk their
     * own stacks; profilers should not attempt to provide a context when walking
     * another thread's stack. If context is NULL, the stack walk will begin at the
     * last available managed frame for the target thread.
     *
     * See the definition of StackSnapshotCallback for more information.
     */
    HResult DoStackSnapshot(
                ThreadId thread,
                delegate* unmanaged[Stdcall]<FunctionId, nint, COR_PRF_FRAME_INFO, uint, byte*, void*, HResult> callback,
                uint infoFlags,
                void* clientData,
                byte* context,
                uint contextSize);

    /*
     * The code profiler calls SetFunctionHooks2 to specify handlers
     * for FunctionEnter2, FunctionLeave2, and FunctionTailcall2
     * callbacks.
     *
     * Note that only one set of callbacks may be active at a time. Thus,
     * if a profiler calls SetEnterLeaveFunctionHooks, SetEnterLeaveFunctionHooks2
     * and SetEnterLeaveFunctionHooks3(WithInfo), then SetEnterLeaveFunctionHooks3(WithInfo)
     * wins.  SetEnterLeaveFunctionHooks2 takes precedence over SetEnterLeaveFunctionHooks
     * when both are set.
     *
     * Each pointer may be null to disable that particular callback.
     *
     * SetEnterLeaveFunctionHooks2 may only be called from the
     * profiler's Initialize() callback.
     */
    HResult SetEnterLeaveFunctionHooks2(
                void* pFuncEnter,
                void* pFuncLeave,
                void* pFuncTailcall);

    /*
     * GetFunctionInfo2 returns the parent class of a function, plus the
     * function's metadata token and the ClassIDs of its type arguments
     * (if any).
     *
     * When a COR_PRF_FRAME_INFO obtained from a FunctionEnter2
     * callback is passed, the ClassID and all type arguments will be exact.
     *
     * When a COR_PRF_FRAME_INFO from any other source is passed, or
     * when 0 is passed as the frameInfo argument, exact ClassID and type
     * arguments cannot always be determined.  The value returned in pClassId
     * may be NULL and some type args will come back as System.Object.
     *
     */
    HResult GetFunctionInfo2(
                FunctionId funcId,
                COR_PRF_FRAME_INFO frameInfo,
                out ClassId pClassId,
                out ModuleId pModuleId,
                out MdToken pToken,
                uint cTypeArgs,
                out uint pcTypeArgs,
                out ClassId* typeArgs);

    /*
     * GetStringLayout returns detailed information about how string objects are stored.
     *
     * *pBufferLengthOffset is the offset (from the ObjectID pointer) to a DWORD that
     * stores the length of the string's buffer
     *
     * *pStringLengthOffset is the offset (from the ObjectID pointer) to a DWORD that
     * stores the length of the string itself
     *
     * *pBufferOffset is the offset (from the ObjectID pointer) to the actual buffer
     * of wide characters
     *
     * Strings may or may not be null-terminated.
     */
    HResult GetStringLayout(
                out uint pBufferLengthOffset,
                out uint pStringLengthOffset,
                out uint pBufferOffset);

    /*
     * GetClassLayout returns detailed information how a specific class is stored.
     * It only returns the fields defined by the class itself; if the parent class
     * defined fields as well, the profiler must call GetClassLayout on the parent class
     * to obtain those fields.
     *
     * It will fail with E_INVALIDARG for string and array classes.
     */
    HResult GetClassLayout(
                ClassId classID,
                out COR_FIELD_OFFSET* rFieldOffset,
                uint cFieldOffset,
                out uint pcFieldOffset,
                out uint pulClassSize);

    /*
     * Returns the parent module a class is defined in, along with the
     * metadata token for the class, the ClassID of its parent class, and the
     * ClassIDs of its type arguments (if any).
     *
     * One can call GetModuleMetaData to obtain the metadata interface for
     * a given module.  The token can then be used to access the metadata for this
     * class.
     */
    HResult GetClassIDInfo2(
                ClassId classId,
                out ModuleId pModuleId,
                out MdTypeDef pTypeDefToken,
                out ClassId pParentClassId,
                uint cNumTypeArgs,
                out uint pcNumTypeArgs,
                out ClassId* typeArgs);

    /*
     * GetCodeInfo2 returns the extents of native code associated with the
     * given FunctionID. These extents are returned sorted in order of increasing
     * IL offset.
     */
    HResult GetCodeInfo2(
                FunctionId functionID,
                uint cCodeInfos,
                out uint pcCodeInfos,
                out COR_PRF_CODE_INFO* codeInfos);

    /*
     * GetClassFromTokenAndTypeArgs returns the ClassID of a type given its metadata
     * token (typedef) and the ClassIDs of its type arguments (if any).
     *
     *      cTypeArgs must be equal to the number of type parameters for the given type
     *          (0 for non-generic types)
     *      typeArgs may be NULL if cTypeArgs == 0
     *
     * Calling this function with a TypeRef token can have unpredictable results; callers
     * should resolve the TypeRef to a TypeDef and use that.
     *
     * If the type is not already loaded, calling this function will cause it to be.
     * Loading is a dangerous operation in many contexts. For example, calling
     * this function during loading of modules or other types could lead to an infinite
     * loop as the runtime attempts to circularly load things.
     *
     * In general, use of this function is discouraged. If profilers are interested in
     * events for a particular type, they should store the ModuleID and TypeDef of that type,
     * and use GetClassIDInfo2 to check whether a given ClassID is the desired type.
     */
    HResult GetClassFromTokenAndTypeArgs(
                    ModuleId moduleID,
                    MdTypeDef typeDef,
                    uint cTypeArgs,
                    ClassId* typeArgs,
                    out ClassId pClassID);

    /*
     * GetFunctionFromTokenAndTypeArgs returns the FunctionID of a function given
     * its metadata token (methoddef), containing class, and type args (if any).
     *
     *      classID may be 0 if the containing class is not generic
     *      typeArgs may be NULL if cTypeArgs == 0
     *
     * Calling this function with a MethodRef token can have unpredictable results; callers
     * should resolve the MethodRef to a MethodDef and use that.
     *
     * If the function is not already loaded, calling this function will cause it to be.
     * Loading is a dangerous operation in many contexts. For example, calling
     * this function during loading of modules or types could lead to an infinite
     * loop as the runtime attempts to circularly load things.
     *
     * In general, use of this function is discouraged. If profilers are interested in
     * events for a particular function, they should store the ModuleID and MethodDef of that function,
     * and use GetFunctionInfo2 to check whether a given FunctionID is the desired function.
     */
    HResult GetFunctionFromTokenAndTypeArgs(
                    ModuleId moduleID,
                    MdMethodDef funcDef,
                    ClassId classId,
                    uint cTypeArgs,
                    ClassId* typeArgs,
                    out FunctionId pFunctionID);

    /*
     * Returns an enumerator over all frozen objects in the given module.
     */
    HResult EnumModuleFrozenObjects(
                ModuleId moduleID,
                out void* ppEnum);

    /*
     * GetArrayObjectInfo returns detailed information about an array object.
     * objectId is a valid array object.
     * cDimensions is the rank (# of dimensions).
     * On success:
     *   pDimensionSizes, pDimensionLowerBounds are parallel arrays describing the size and lower bound for each dimension.
     *   (*ppData) is a pointer to the raw buffer for the array, which is laid out according to the C++
     *   convention
     */
    HResult GetArrayObjectInfo(
                    ObjectId objectId,
                    uint cDimensions,
                    out uint* pDimensionSizes,
                    out int* pDimensionLowerBounds,
                    out byte* ppData);

    /*
     * GetBoxClassLayout returns information about how a particular value type is laid out
     * when boxed.
     *
     *  *pBufferOffset is the offset (from the ObjectID pointer) to where the value type
     *  is stored within the box. The value type's class layout may then be used to
     *  interpret it.
     */
    HResult GetBoxClassLayout(
                    ClassId classId,
                    out uint pBufferOffset);

    /*
     * GetThreadAppDomain returns the AppDomainID currently associated with\
     * the given ThreadID
     */
    HResult GetThreadAppDomain(
                    ThreadId threadId,
                    out AppDomainId pAppDomainId);

    /*
     * GetRVAStaticAddress gets the address of the home for the given
     * RVA static. It must be called from a managed thread.  Otherwise,
     * it will return CORPROF_E_NOT_MANAGED_THREAD.
     */
    HResult GetRVAStaticAddress(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    out void* ppAddress);

    /*
     * GetAppDomainStaticAddress gets the address of the home for the given
     * AppDomain static in the given AppDomain.
     *
     * This function may return CORPROF_E_DATAINCOMPLETE if the given static
     * has not been assigned a home in the given AppDomain.
     */
    HResult GetAppDomainStaticAddress(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    AppDomainId appDomainId,
                    out void* ppAddress);

    /*
     * GetThreadStaticAddress gets the address of the home for the given
     * Thread static in the given Thread. threadId must be the current thread
     * ID or NULL, which means using curernt thread ID.
     *
     * This function may return CORPROF_E_DATAINCOMPLETE if the given static
     * has not been assigned a home in the given Thread.
     */
    HResult GetThreadStaticAddress(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    ThreadId threadId,
                    out void* ppAddress);

    /*
     * GetContextStaticAddress gets the address of the home for the given
     * Context static in the given context.  It must be called from a managed
     * thread.  Otherwise, it will return CORPROF_E_NOT_MANAGED_THREAD.
     *
     * This function may return CORPROF_E_DATAINCOMPLETE if the given static
     * has not been assigned a home in the given Context.
     */
    HResult GetContextStaticAddress(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    ContextId contextId,
                    out void* ppAddress);

    /*
     * GetStaticFieldInfo gets COR_PRF_STATIC_TYPE for a specific
     * field in a class. This information can be used to decide which
     * function to call to get the address of the static.
     *
     * NOTE: One should still check the metadata for a static to ensure
     * it is actually going to have an address. Statics that are literals
     * (aka constants) exist only in the metadata and do not have an address.
     *
     */
    HResult GetStaticFieldInfo(
                    ClassId classId,
                    MdFieldDef fieldToken,
                    out COR_PRF_STATIC_TYPE pFieldInfo);

    /*
     * GetGenerationBounds returns the memory regions that make up a given
     * GC generation in memory. It may be called from any profiler callback as long
     * as a GC is not in progress. (To be exact, it may be called from any callback
     * except for those that occur between GarbageCollectionStarted and GarbageCollectionFinished.)
     *
     * Most shifting of generations takes place during garbage collections; between
     * collections generations may grow, but generally do not move around. Therefore
     * the most interesting places to call this function are in GarbageCollectionStarted
     * and Finished.
     *
     * During program startup, some objects are allocated by the CLR itself, generally
     * in generations 3 and 0. So by the time managed code starts executing, these
     * generations will already contain objects. Generations 1 and 2 will be normally
     * empty, except for dummy objects generated by the garbage collector (of size 12
     * bytes in 32-bit implementations of the CLR, larger in 64-bit implementaions).
     * You may also see generation 2 ranges that are inside modules generated by ngen.
     * These are "frozen objects" generated at ngen time rather than allocated by the
     * garbage collector.
     *
     * cObjectRanges is a count of the number of elements allocated by the caller for
     *      the ranges array
     * pcObjectRanges is an out param for the number of ranges in the given generation
     * ranges is an array of elements of type COR_PRF_GC_GENERATION_RANGE, each of which
     *      describes a range of memory used by the garbage collector
     */

    HResult GetGenerationBounds(
                    uint cObjectRanges,
                    out uint pcObjectRanges,
                    out COR_PRF_GC_GENERATION_RANGE* ranges);

    /*
     * GetObjectGeneration returns which generation the given object is currently in, along
     * with the start and length of the segment containing the object. It may be called
     * at any time as long as a GC is not in progress.
     */

    HResult GetObjectGeneration(
                    ObjectId objectId,
                    out COR_PRF_GC_GENERATION_RANGE range);

    /*
      * When an exception notification is received, GetNotifiedExceptionClauseInfo() may be used
      * to get the native address and frame information for the exception clause (catch/finally/filter)
      * that is about to be run (ExceptionCatchEnter, ExceptionUnwindFinallyEnter, ExceptionFilterEnter)
      * or has just been run (ExceptionCatchLeave, ExceptionUnwindFinallyLeave, ExceptionFilterLeave).
      *
      * This call may be made at any time after one of the Enter calls above until either the matching
      * Leave call is received or until a nested exception throws out of the current clause in which case
      * there will be no Leave notification for that clause.  Note it is not possible for a throw to escape
      * a Filter so there is always a Leave in that case.
      *
      * Return values:
      *   S_OK indicates success
      *   S_FALSE indicates that no exception clause is active
      *   CORPROF_E_NOT_MANAGED_THREAD indicates an unmanaged thread.
      */

    HResult GetNotifiedExceptionClauseInfo(
                    out COR_PRF_EX_CLAUSE_INFO pinfo);
}
