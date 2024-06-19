namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface ICorProfilerCallback : IUnknown
{
    public static new readonly Guid Guid = Guid.Parse("8a8cc829-ccf2-49fe-bbae-0f022228071a");

    HResult Initialize(IntPtr pICorProfilerInfoUnk);

    HResult Shutdown();

    HResult AppDomainCreationStarted(AppDomainId appDomainId);
    HResult AppDomainCreationFinished(AppDomainId appDomainId, HResult hrStatus);

    HResult AppDomainShutdownStarted(AppDomainId appDomainId);
    HResult AppDomainShutdownFinished(AppDomainId appDomainId, HResult hrStatus);

    HResult AssemblyLoadStarted(AssemblyId assemblyId);
    HResult AssemblyLoadFinished(AssemblyId assemblyId, HResult hrStatus);

    HResult AssemblyUnloadStarted(AssemblyId assemblyId);
    HResult AssemblyUnloadFinished(AssemblyId assemblyId, HResult hrStatus);

    HResult ModuleLoadStarted(ModuleId moduleId);
    HResult ModuleLoadFinished(ModuleId moduleId, HResult hrStatus);

    HResult ModuleUnloadStarted(ModuleId moduleId);
    HResult ModuleUnloadFinished(ModuleId moduleId, HResult hrStatus);

    HResult ModuleAttachedToAssembly(ModuleId moduleId, AssemblyId assemblyId);

    HResult ClassLoadStarted(ClassId classId);
    HResult ClassLoadFinished(ClassId classId, HResult hrStatus);

    HResult ClassUnloadStarted(ClassId classId);
    HResult ClassUnloadFinished(ClassId classId, HResult hrStatus);

    HResult FunctionUnloadStarted(FunctionId functionId);

    HResult JITCompilationStarted(FunctionId functionId, int fIsSafeToBlock);
    HResult JITCompilationFinished(FunctionId functionId, HResult hrStatus, int fIsSafeToBlock);

    HResult JITCachedFunctionSearchStarted(FunctionId functionId, out int pbUseCachedFunction);
    HResult JITCachedFunctionSearchFinished(FunctionId functionId, COR_PRF_JIT_CACHE result);

    HResult JITFunctionPitched(FunctionId functionId);

    HResult JITInlining(FunctionId callerId, FunctionId calleeId, out int pfShouldInline);

    HResult ThreadCreated(ThreadId threadId);
    HResult ThreadDestroyed(ThreadId threadId);
    HResult ThreadAssignedToOSThread(ThreadId managedThreadId, int osThreadId);

    HResult RemotingClientInvocationStarted();
    HResult RemotingClientSendingMessage(in Guid pCookie, int fIsAsync);
    HResult RemotingClientReceivingReply(in Guid pCookie, int fIsAsync);
    HResult RemotingClientInvocationFinished();

    HResult RemotingServerReceivingMessage(in Guid pCookie, int fIsAsync);
    HResult RemotingServerInvocationStarted();
    HResult RemotingServerInvocationReturned();
    HResult RemotingServerSendingReply(in Guid pCookie, int fIsAsync);

    HResult UnmanagedToManagedTransition(FunctionId functionId, COR_PRF_TRANSITION_REASON reason);
    HResult ManagedToUnmanagedTransition(FunctionId functionId, COR_PRF_TRANSITION_REASON reason);

    HResult RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason);
    HResult RuntimeSuspendFinished();
    HResult RuntimeSuspendAborted();

    HResult RuntimeResumeStarted();
    HResult RuntimeResumeFinished();

    HResult RuntimeThreadSuspended(ThreadId threadId);
    HResult RuntimeThreadResumed(ThreadId threadId);

    HResult MovedReferences(
        uint cMovedObjectIDRanges,
        ObjectId* oldObjectIDRangeStart,
        ObjectId* newObjectIDRangeStart,
        uint* cObjectIDRangeLength);

    HResult ObjectAllocated(ObjectId objectId, ClassId classId);

    HResult ObjectsAllocatedByClass(uint cClassCount, ClassId* classIds, uint* cObjects);

    HResult ObjectReferences(
        ObjectId objectId,
        ClassId classId,
        uint cObjectRefs,
        ObjectId* objectRefIds);

    HResult RootReferences(uint cRootRefs, ObjectId* rootRefIds);

    HResult ExceptionThrown(ObjectId thrownObjectId);

    HResult ExceptionSearchFunctionEnter(FunctionId functionId);
    HResult ExceptionSearchFunctionLeave();

    HResult ExceptionSearchFilterEnter(FunctionId functionId);
    HResult ExceptionSearchFilterLeave();

    HResult ExceptionSearchCatcherFound(FunctionId functionId);

    HResult ExceptionOSHandlerEnter(nint* __unused);
    HResult ExceptionOSHandlerLeave(nint* __unused);

    HResult ExceptionUnwindFunctionEnter(FunctionId functionId);
    HResult ExceptionUnwindFunctionLeave();

    HResult ExceptionUnwindFinallyEnter(FunctionId functionId);
    HResult ExceptionUnwindFinallyLeave();

    HResult ExceptionCatcherEnter(FunctionId functionId, ObjectId objectId);
    HResult ExceptionCatcherLeave();

    HResult COMClassicVTableCreated(
        ClassId wrappedClassId,
        in Guid implementedIID,
        void* pVTable,
        uint cSlots);

    HResult COMClassicVTableDestroyed(
        ClassId wrappedClassId,
        in Guid implementedIID,
        void* pVTable);

    HResult ExceptionCLRCatcherFound();
    HResult ExceptionCLRCatcherExecute();
}
