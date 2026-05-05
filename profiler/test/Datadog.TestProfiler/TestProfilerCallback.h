// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ManagedCodeCache.h"
#include "FrameStore.h"

#include <memory>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <mutex>
#include <random>
#include <condition_variable>
#include <atomic>

enum class MethodCategory {
    JitCompiled,
    DynamicMethod,
    ReadyToRun,
    ReJIT
};

struct CodeRangeInfo {
    std::vector<uintptr_t> instructionPointers;  // 3-4 sampled IPs
    COR_PRF_CODE_INFO codeInfo;
    uint64_t timestamp;
    MethodCategory category;
    bool skipValidation = false;
    std::string skipReason;
};

struct InvalidIPTest {
    uintptr_t ip;
    std::string description;
};

// TODO: ReJIT support
// ReJIT testing is currently disabled because all methods fail with error 0x8013137c
// (likely compiler-generated methods that cannot be ReJIT'd in .NET Core).
// Future work: Investigate which method types can be ReJIT'd and implement worker-thread
// based ReJIT triggering similar to the tracer (see tracer/src/Datadog.Tracer.Native/rejit_handler.cpp)

class TestProfilerCallback : public ICorProfilerCallback10 {
public:
    // Exposed for validation
    std::unique_ptr<ManagedCodeCache> _pManagedCodeCache;
    std::unique_ptr<FrameStore> _pFrameStore;
    ICorProfilerInfo4* _pCorProfilerInfo = nullptr;

    // IP collection
    std::unordered_map<FunctionID, std::vector<CodeRangeInfo>> _collectedIPs;
    std::mutex _ipCollectionMutex;

    // Invalid IP testing
    std::vector<InvalidIPTest> _invalidIPsToTest;
    std::mutex _invalidIPsMutex;

private:
    std::atomic<ULONG> _refCount{0};
    std::mt19937 _randomGen;

    // Singleton
    static TestProfilerCallback* _instance;

    // Helpers
    void CollectIPsForFunction(FunctionID functionId, MethodCategory category);
    void AddClearlyInvalidIPs();
    void CollectNativeCodeIPs();

public:
    TestProfilerCallback();
    virtual ~TestProfilerCallback();

    static TestProfilerCallback* GetInstance() { return _instance; }

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override;
    STDMETHOD_(ULONG, AddRef)() override;
    STDMETHOD_(ULONG, Release)() override;

    // ICorProfilerCallback - required methods
    STDMETHOD(Initialize)(IUnknown* pICorProfilerInfoUnk) override;
    STDMETHOD(Shutdown)() override;
    STDMETHOD(AppDomainCreationStarted)(AppDomainID appDomainId) override { return S_OK; }
    STDMETHOD(AppDomainCreationFinished)(AppDomainID appDomainId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(AppDomainShutdownStarted)(AppDomainID appDomainId) override { return S_OK; }
    STDMETHOD(AppDomainShutdownFinished)(AppDomainID appDomainId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(AssemblyLoadStarted)(AssemblyID assemblyId) override { return S_OK; }
    STDMETHOD(AssemblyLoadFinished)(AssemblyID assemblyId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(AssemblyUnloadStarted)(AssemblyID assemblyId) override { return S_OK; }
    STDMETHOD(AssemblyUnloadFinished)(AssemblyID assemblyId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(ModuleLoadStarted)(ModuleID moduleId) override { return S_OK; }
    STDMETHOD(ModuleLoadFinished)(ModuleID moduleId, HRESULT hrStatus) override;
    STDMETHOD(ModuleUnloadStarted)(ModuleID moduleId) override { return S_OK; }
    STDMETHOD(ModuleUnloadFinished)(ModuleID moduleId, HRESULT hrStatus) override;
    STDMETHOD(ModuleAttachedToAssembly)(ModuleID moduleId, AssemblyID AssemblyId) override { return S_OK; }
    STDMETHOD(ClassLoadStarted)(ClassID classId) override { return S_OK; }
    STDMETHOD(ClassLoadFinished)(ClassID classId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(ClassUnloadStarted)(ClassID classId) override { return S_OK; }
    STDMETHOD(ClassUnloadFinished)(ClassID classId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(FunctionUnloadStarted)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(JITCompilationStarted)(FunctionID functionId, BOOL fIsSafeToBlock) override { return S_OK; }
    STDMETHOD(JITCompilationFinished)(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    STDMETHOD(JITCachedFunctionSearchStarted)(FunctionID functionId, BOOL* pbUseCachedFunction) override { return S_OK; }
    STDMETHOD(JITCachedFunctionSearchFinished)(FunctionID functionId, COR_PRF_JIT_CACHE result) override { return S_OK; }
    STDMETHOD(JITFunctionPitched)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(JITInlining)(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) override { return S_OK; }
    STDMETHOD(ThreadCreated)(ThreadID threadId) override { return S_OK; }
    STDMETHOD(ThreadDestroyed)(ThreadID threadId) override { return S_OK; }
    STDMETHOD(ThreadAssignedToOSThread)(ThreadID managedThreadId, DWORD osThreadId) override { return S_OK; }
    STDMETHOD(RemotingClientInvocationStarted)() override { return S_OK; }
    STDMETHOD(RemotingClientSendingMessage)(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
    STDMETHOD(RemotingClientReceivingReply)(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
    STDMETHOD(RemotingClientInvocationFinished)() override { return S_OK; }
    STDMETHOD(RemotingServerReceivingMessage)(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
    STDMETHOD(RemotingServerInvocationStarted)() override { return S_OK; }
    STDMETHOD(RemotingServerInvocationReturned)() override { return S_OK; }
    STDMETHOD(RemotingServerSendingReply)(GUID* pCookie, BOOL fIsAsync) override { return S_OK; }
    STDMETHOD(UnmanagedToManagedTransition)(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override { return S_OK; }
    STDMETHOD(ManagedToUnmanagedTransition)(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override { return S_OK; }
    STDMETHOD(RuntimeSuspendStarted)(COR_PRF_SUSPEND_REASON suspendReason) override { return S_OK; }
    STDMETHOD(RuntimeSuspendFinished)() override { return S_OK; }
    STDMETHOD(RuntimeSuspendAborted)() override { return S_OK; }
    STDMETHOD(RuntimeResumeStarted)() override { return S_OK; }
    STDMETHOD(RuntimeResumeFinished)() override { return S_OK; }
    STDMETHOD(RuntimeThreadSuspended)(ThreadID threadId) override { return S_OK; }
    STDMETHOD(RuntimeThreadResumed)(ThreadID threadId) override { return S_OK; }
    STDMETHOD(MovedReferences)(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) override { return S_OK; }
    STDMETHOD(ObjectAllocated)(ObjectID objectId, ClassID classId) override { return S_OK; }
    STDMETHOD(ObjectsAllocatedByClass)(ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) override { return S_OK; }
    STDMETHOD(ObjectReferences)(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]) override { return S_OK; }
    STDMETHOD(RootReferences)(ULONG cRootRefs, ObjectID rootRefIds[]) override { return S_OK; }
    STDMETHOD(ExceptionThrown)(ObjectID thrownObjectId) override { return S_OK; }
    STDMETHOD(ExceptionSearchFunctionEnter)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(ExceptionSearchFunctionLeave)() override { return S_OK; }
    STDMETHOD(ExceptionSearchFilterEnter)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(ExceptionSearchFilterLeave)() override { return S_OK; }
    STDMETHOD(ExceptionSearchCatcherFound)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(ExceptionOSHandlerEnter)(UINT_PTR __unused) override { return S_OK; }
    STDMETHOD(ExceptionOSHandlerLeave)(UINT_PTR __unused) override { return S_OK; }
    STDMETHOD(ExceptionUnwindFunctionEnter)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(ExceptionUnwindFunctionLeave)() override { return S_OK; }
    STDMETHOD(ExceptionUnwindFinallyEnter)(FunctionID functionId) override { return S_OK; }
    STDMETHOD(ExceptionUnwindFinallyLeave)() override { return S_OK; }
    STDMETHOD(ExceptionCatcherEnter)(FunctionID functionId, ObjectID objectId) override { return S_OK; }
    STDMETHOD(ExceptionCatcherLeave)() override { return S_OK; }
    STDMETHOD(COMClassicVTableCreated)(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable, ULONG cSlots) override { return S_OK; }
    STDMETHOD(COMClassicVTableDestroyed)(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable) override { return S_OK; }
    STDMETHOD(ExceptionCLRCatcherFound)() override { return S_OK; }
    STDMETHOD(ExceptionCLRCatcherExecute)() override { return S_OK; }

    // ICorProfilerCallback2
    STDMETHOD(ThreadNameChanged)(ThreadID threadId, ULONG cchName, WCHAR name[]) override { return S_OK; }
    STDMETHOD(GarbageCollectionStarted)(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason) override { return S_OK; }
    STDMETHOD(SurvivingReferences)(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]) override { return S_OK; }
    STDMETHOD(GarbageCollectionFinished)() override { return S_OK; }
    STDMETHOD(FinalizeableObjectQueued)(DWORD finalizerFlags, ObjectID objectID) override { return S_OK; }
    STDMETHOD(RootReferences2)(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override { return S_OK; }
    STDMETHOD(HandleCreated)(GCHandleID handleId, ObjectID initialObjectId) override { return S_OK; }
    STDMETHOD(HandleDestroyed)(GCHandleID handleId) override { return S_OK; }

    // ICorProfilerCallback3
    STDMETHOD(InitializeForAttach)(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData) override { return S_OK; }
    STDMETHOD(ProfilerAttachComplete)() override { return S_OK; }
    STDMETHOD(ProfilerDetachSucceeded)() override { return S_OK; }

    // ICorProfilerCallback4
    STDMETHOD(ReJITCompilationStarted)(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock) override { return S_OK; }
    STDMETHOD(GetReJITParameters)(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl) override { return S_OK; }
    STDMETHOD(ReJITCompilationFinished)(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    STDMETHOD(ReJITError)(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus) override { return S_OK; }
    STDMETHOD(MovedReferences2)(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override { return S_OK; }
    STDMETHOD(SurvivingReferences2)(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override { return S_OK; }

    // ICorProfilerCallback5
    STDMETHOD(ConditionalWeakTableElementReferences)(ULONG cRootRefs, ObjectID keyRefIds[], ObjectID valueRefIds[], GCHandleID rootIds[]) override { return S_OK; }

    // ICorProfilerCallback6
    STDMETHOD(GetAssemblyReferences)(const WCHAR* wszAssemblyPath, ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override { return S_OK; }

    // ICorProfilerCallback7
    STDMETHOD(ModuleInMemorySymbolsUpdated)(ModuleID moduleId) override { return S_OK; }

    // ICorProfilerCallback8
    STDMETHOD(DynamicMethodJITCompilationStarted)(FunctionID functionId, BOOL fIsSafeToBlock, LPCBYTE ilHeader, ULONG cbILHeader) override { return S_OK; }
    STDMETHOD(DynamicMethodJITCompilationFinished)(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    STDMETHOD(DynamicMethodUnloaded)(FunctionID functionId) override { return S_OK; }

    // ICorProfilerCallback9 - No additional methods beyond ICorProfilerCallback8

    // ICorProfilerCallback10
    STDMETHOD(EventPipeEventDelivered)(EVENTPIPE_PROVIDER provider, DWORD eventId, DWORD eventVersion, ULONG cbMetadataBlob, LPCBYTE metadataBlob, ULONG cbEventData, LPCBYTE eventData, LPCGUID pActivityId, LPCGUID pRelatedActivityId, ThreadID eventThread, ULONG numStackFrames, UINT_PTR stackFrames[]) override { return S_OK; }
    STDMETHOD(EventPipeProviderCreated)(EVENTPIPE_PROVIDER provider) override { return S_OK; }
};
