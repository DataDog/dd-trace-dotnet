#pragma once
#include <corhlpr.h>
#include <corprof.h>

class TestCorProfiler : public ICorProfilerCallback10
{
public:
    ULONG m_Ref = 0;
    ULONG m_Initialize = 0;
    ULONG m_Shutdown = 0;
    ULONG m_AppDomainCreationStarted = 0;
    ULONG m_AppDomainCreationFinished = 0;
    ULONG m_AppDomainShutdownStarted = 0;
    ULONG m_AppDomainShutdownFinished = 0;
    ULONG m_AssemblyLoadStarted = 0;
    ULONG m_AssemblyLoadFinished = 0;
    ULONG m_AssemblyUnloadStarted = 0;
    ULONG m_AssemblyUnloadFinished = 0;
    ULONG m_ModuleLoadStarted = 0;
    ULONG m_ModuleLoadFinished = 0;
    ULONG m_ModuleUnloadStarted = 0;
    ULONG m_ModuleUnloadFinished = 0;
    ULONG m_ModuleAttachedToAssembly = 0;
    ULONG m_ClassLoadStarted = 0;
    ULONG m_ClassLoadFinished = 0;
    ULONG m_ClassUnloadStarted = 0;
    ULONG m_ClassUnloadFinished = 0;
    ULONG m_FunctionUnloadStarted = 0;
    ULONG m_JITCompilationStarted = 0;
    ULONG m_JITCompilationFinished = 0;
    ULONG m_JITCachedFunctionSearchStarted = 0;
    ULONG m_JITCachedFunctionSearchFinished = 0;
    ULONG m_JITFunctionPitched = 0;
    ULONG m_JITInlining = 0;
    ULONG m_ThreadCreated = 0;
    ULONG m_ThreadDestroyed = 0;
    ULONG m_ThreadAssignedToOSThread = 0;
    ULONG m_RemotingClientInvocationStarted = 0;
    ULONG m_RemotingClientSendingMessage = 0;
    ULONG m_RemotingClientReceivingReply = 0;
    ULONG m_RemotingClientInvocationFinished = 0;
    ULONG m_RemotingServerReceivingMessage = 0;
    ULONG m_RemotingServerInvocationStarted = 0;
    ULONG m_RemotingServerInvocationReturned = 0;
    ULONG m_RemotingServerSendingReply = 0;
    ULONG m_UnmanagedToManagedTransition = 0;
    ULONG m_ManagedToUnmanagedTransition = 0;
    ULONG m_RuntimeSuspendStarted = 0;
    ULONG m_RuntimeSuspendFinished = 0;
    ULONG m_RuntimeSuspendAborted = 0;
    ULONG m_RuntimeResumeStarted = 0;
    ULONG m_RuntimeResumeFinished = 0;
    ULONG m_RuntimeThreadSuspended = 0;
    ULONG m_RuntimeThreadResumed = 0;
    ULONG m_MovedReferences = 0;
    ULONG m_ObjectAllocated = 0;
    ULONG m_ObjectsAllocatedByClass = 0;
    ULONG m_ObjectReferences = 0;
    ULONG m_RootReferences = 0;
    ULONG m_ExceptionThrown = 0;
    ULONG m_ExceptionSearchFunctionEnter = 0;
    ULONG m_ExceptionSearchFunctionLeave = 0;
    ULONG m_ExceptionSearchFilterEnter = 0;
    ULONG m_ExceptionSearchFilterLeave = 0;
    ULONG m_ExceptionSearchCatcherFound = 0;
    ULONG m_ExceptionOSHandlerEnter = 0;
    ULONG m_ExceptionOSHandlerLeave = 0;
    ULONG m_ExceptionUnwindFunctionEnter = 0;
    ULONG m_ExceptionUnwindFunctionLeave = 0;
    ULONG m_ExceptionUnwindFinallyEnter = 0;
    ULONG m_ExceptionUnwindFinallyLeave = 0;
    ULONG m_ExceptionCatcherEnter = 0;
    ULONG m_ExceptionCatcherLeave = 0;
    ULONG m_COMClassicVTableCreated = 0;
    ULONG m_COMClassicVTableDestroyed = 0;
    ULONG m_ExceptionCLRCatcherFound = 0;
    ULONG m_ExceptionCLRCatcherExecute = 0;
    ULONG m_ThreadNameChanged = 0;
    ULONG m_GarbageCollectionStarted = 0;
    ULONG m_SurvivingReferences = 0;
    ULONG m_GarbageCollectionFinished = 0;
    ULONG m_FinalizeableObjectQueued = 0;
    ULONG m_RootReferences2 = 0;
    ULONG m_HandleCreated = 0;
    ULONG m_HandleDestroyed = 0;
    ULONG m_InitializeForAttach = 0;
    ULONG m_ProfilerAttachComplete = 0;
    ULONG m_ProfilerDetachSucceeded = 0;
    ULONG m_ReJITCompilationStarted = 0;
    ULONG m_GetReJITParameters = 0;
    ULONG m_ReJITCompilationFinished = 0;
    ULONG m_ReJITError = 0;
    ULONG m_MovedReferences2 = 0;
    ULONG m_SurvivingReferences2 = 0;
    ULONG m_ConditionalWeakTableElementReferences = 0;
    ULONG m_GetAssemblyReferences = 0;
    ULONG m_ModuleInMemorySymbolsUpdated = 0;
    ULONG m_DynamicMethodJITCompilationStarted = 0;
    ULONG m_DynamicMethodJITCompilationFinished = 0;
    ULONG m_DynamicMethodUnloaded = 0;
    ULONG m_EventPipeEventDelivered = 0;
    ULONG m_EventPipeProviderCreated = 0;

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
    {
        return E_FAIL;
    }
    ULONG STDMETHODCALLTYPE AddRef(void) override
    {
        return ++m_Ref;
    }
    ULONG STDMETHODCALLTYPE Release(void) override
    {
        return --m_Ref;
    }

    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override
    {
        m_Initialize++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Shutdown() override
    {
        m_Shutdown++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE AppDomainCreationStarted(AppDomainID appDomainId) override
    {
        m_AppDomainCreationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus) override
    {
        m_AppDomainCreationFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AppDomainShutdownStarted(AppDomainID appDomainId) override
    {
        m_AppDomainShutdownStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus) override
    {
        m_AppDomainShutdownFinished++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE AssemblyLoadStarted(AssemblyID assemblyId) override
    {
        m_AssemblyLoadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus) override
    {
        m_AssemblyLoadFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AssemblyUnloadStarted(AssemblyID assemblyId) override
    {
        m_AssemblyUnloadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus) override
    {
        m_AssemblyUnloadFinished++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ModuleLoadStarted(ModuleID moduleId) override
    {
        m_ModuleLoadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) override
    {
        m_ModuleLoadFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID moduleId) override
    {
        m_ModuleUnloadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) override
    {
        m_ModuleUnloadFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId) override
    {
        m_ModuleAttachedToAssembly++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ClassLoadStarted(ClassID classId) override
    {
        m_ClassLoadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ClassLoadFinished(ClassID classId, HRESULT hrStatus) override
    {
        m_ClassLoadFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ClassUnloadStarted(ClassID classId) override
    {
        m_ClassUnloadStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ClassUnloadFinished(ClassID classId, HRESULT hrStatus) override
    {
        m_ClassUnloadFinished++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE FunctionUnloadStarted(FunctionID functionId) override
    {
        m_FunctionUnloadStarted++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) override
    {
        m_JITCompilationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE JITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                     BOOL fIsSafeToBlock) override
    {
        m_JITCompilationFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) override
    {
        m_JITCachedFunctionSearchStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchFinished(FunctionID functionId, COR_PRF_JIT_CACHE result) override
    {
        m_JITCachedFunctionSearchFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE JITFunctionPitched(FunctionID functionId) override
    {
        m_JITFunctionPitched++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) override
    {
        m_JITInlining++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ThreadCreated(ThreadID threadId) override
    {
        m_ThreadCreated++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ThreadDestroyed(ThreadID threadId) override
    {
        m_ThreadDestroyed++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId) override
    {
        m_ThreadAssignedToOSThread++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RemotingClientInvocationStarted() override
    {
        m_RemotingClientInvocationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync) override
    {
        m_RemotingClientSendingMessage++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync) override
    {
        m_RemotingClientReceivingReply++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationFinished() override
    {
        m_RemotingClientInvocationFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync) override
    {
        m_RemotingServerReceivingMessage++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationStarted() override
    {
        m_RemotingServerInvocationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationReturned() override
    {
        m_RemotingServerInvocationReturned++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync) override
    {
        m_RemotingServerSendingReply++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE UnmanagedToManagedTransition(FunctionID functionId,
                                                           COR_PRF_TRANSITION_REASON reason) override
    {
        m_UnmanagedToManagedTransition++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ManagedToUnmanagedTransition(FunctionID functionId,
                                                           COR_PRF_TRANSITION_REASON reason) override
    {
        m_ManagedToUnmanagedTransition++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) override
    {
        m_RuntimeSuspendStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeSuspendFinished() override
    {
        m_RuntimeSuspendFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeSuspendAborted() override
    {
        m_RuntimeSuspendAborted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeResumeStarted() override
    {
        m_RuntimeResumeStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeResumeFinished() override
    {
        m_RuntimeResumeFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeThreadSuspended(ThreadID threadId) override
    {
        m_RuntimeThreadSuspended++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RuntimeThreadResumed(ThreadID threadId) override
    {
        m_RuntimeThreadResumed++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                              ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) override
    {
        m_MovedReferences++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ObjectAllocated(ObjectID objectId, ClassID classId) override
    {
        m_ObjectAllocated++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) override
    {
        m_ObjectsAllocatedByClass++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs,
                                               ObjectID objectRefIds[]) override
    {
        m_ObjectReferences++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RootReferences(ULONG cRootRefs, ObjectID rootRefIds[]) override
    {
        m_RootReferences++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ExceptionThrown(ObjectID thrownObjectId) override
    {
        m_ExceptionThrown++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter(FunctionID functionId) override
    {
        m_ExceptionSearchFunctionEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave() override
    {
        m_ExceptionSearchFunctionLeave++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter(FunctionID functionId) override
    {
        m_ExceptionSearchFilterEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave() override
    {
        m_ExceptionSearchFilterLeave++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound(FunctionID functionId) override
    {
        m_ExceptionSearchCatcherFound++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerEnter(UINT_PTR __unused) override
    {
        m_ExceptionOSHandlerEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerLeave(UINT_PTR __unused) override
    {
        m_ExceptionOSHandlerLeave++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter(FunctionID functionId) override
    {
        m_ExceptionUnwindFunctionEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave() override
    {
        m_ExceptionUnwindFunctionLeave++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter(FunctionID functionId) override
    {
        m_ExceptionUnwindFinallyEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave() override
    {
        m_ExceptionUnwindFinallyLeave++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId) override
    {
        m_ExceptionCatcherEnter++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave() override
    {
        m_ExceptionCatcherLeave++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable,
                                                      ULONG cSlots) override
    {
        m_COMClassicVTableCreated++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID,
                                                        void* pVTable) override
    {
        m_COMClassicVTableDestroyed++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherFound() override
    {
        m_ExceptionCLRCatcherFound++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherExecute() override
    {
        m_ExceptionCLRCatcherExecute++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[]) override
    {
        m_ThreadNameChanged++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GarbageCollectionStarted(int cGenerations, BOOL generationCollected[],
                                                       COR_PRF_GC_REASON reason) override
    {
        m_GarbageCollectionStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[],
                                                  ULONG cObjectIDRangeLength[]) override
    {
        m_SurvivingReferences++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GarbageCollectionFinished() override
    {
        m_GarbageCollectionFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID) override
    {
        m_FinalizeableObjectQueued++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[],
                                              COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override
    {
        m_RootReferences2++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE HandleCreated(GCHandleID handleId, ObjectID initialObjectId) override
    {
        m_HandleCreated++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE HandleDestroyed(GCHandleID handleId) override
    {
        m_HandleDestroyed++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData,
                                                  UINT cbClientData) override
    {
        m_InitializeForAttach++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ProfilerAttachComplete() override
    {
        m_ProfilerAttachComplete++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded() override
    {
        m_ProfilerDetachSucceeded++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                      BOOL fIsSafeToBlock) override
    {
        m_ReJITCompilationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                 ICorProfilerFunctionControl* pFunctionControl) override
    {
        m_GetReJITParameters++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus,
                                                       BOOL fIsSafeToBlock) override
    {
        m_ReJITCompilationFinished++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                         HRESULT hrStatus) override
    {
        m_ReJITError++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE MovedReferences2(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                               ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override
    {
        m_MovedReferences2++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE SurvivingReferences2(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[],
                                                   SIZE_T cObjectIDRangeLength[]) override
    {
        m_SurvivingReferences2++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[],
                                                                    ObjectID valueRefIds[],
                                                                    GCHandleID rootIds[]) override
    {
        m_ConditionalWeakTableElementReferences++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                    ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override
    {
        m_GetAssemblyReferences++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE ModuleInMemorySymbolsUpdated(ModuleID moduleId) override
    {
        m_ModuleInMemorySymbolsUpdated++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock,
                                                                 LPCBYTE ilHeader, ULONG cbILHeader) override
    {
        m_DynamicMethodJITCompilationStarted++;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                  BOOL fIsSafeToBlock) override
    {
        m_DynamicMethodJITCompilationFinished++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DynamicMethodUnloaded(FunctionID functionId) override
    {
        m_DynamicMethodUnloaded++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE EventPipeEventDelivered(EVENTPIPE_PROVIDER provider, DWORD eventId, DWORD eventVersion,
                                                      ULONG cbMetadataBlob, LPCBYTE metadataBlob, ULONG cbEventData,
                                                      LPCBYTE eventData, LPCGUID pActivityId,
                                                      LPCGUID pRelatedActivityId, ThreadID eventThread,
                                                      ULONG numStackFrames, UINT_PTR stackFrames[]) override
    {
        m_EventPipeEventDelivered++;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE EventPipeProviderCreated(EVENTPIPE_PROVIDER provider) override
    {
        m_EventPipeProviderCreated++;
        return S_OK;
    }
};
