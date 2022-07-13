// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "ApplicationStore.h"
#include "ExceptionsProvider.h"
#include "IAppDomainStore.h"
#include "IClrLifetime.h"
#include "IConfiguration.h"
#include "IExporter.h"
#include "IFrameStore.h"
#include "IMetricsSender.h"
#include "WallTimeProvider.h"
#include "CpuTimeProvider.h"
#include "SamplesCollector.h"
#include "shared/src/native-src/string.h"

#include <atomic>
#include <memory>
#include <vector>

class IService;
class IThreadsCpuManager;
class IManagedThreadList;
class IStackSamplerLoopManager;
class IConfiguration;
class IExporter;
class SamplesAggregator;

namespace shared {
class Loader;
}

class CorProfilerCallback : public ICorProfilerCallback10
{
private:
    static const std::vector<shared::WSTRING> ManagedAssembliesToLoad_AppDomainDefault_ProcNonIIS;
    static const std::vector<shared::WSTRING> ManagedAssembliesToLoad_AppDomainNonDefault_ProcNonIIS;
    static const std::vector<shared::WSTRING> ManagedAssembliesToLoad_AppDomainDefault_ProcIIS;
    static const std::vector<shared::WSTRING> ManagedAssembliesToLoad_AppDomainNonDefault_ProcIIS;

public:
    CorProfilerCallback();
    virtual ~CorProfilerCallback();

    // use STDMETHODCALLTYPE macro to match the CLR declaration.
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef(void) override;
    ULONG STDMETHODCALLTYPE Release(void) override;
    ULONG STDMETHODCALLTYPE GetRefCount(void) const;
    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override;
    HRESULT STDMETHODCALLTYPE Shutdown(void) override;
    HRESULT STDMETHODCALLTYPE AppDomainCreationStarted(AppDomainID appDomainId) override;
    HRESULT STDMETHODCALLTYPE AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE AppDomainShutdownStarted(AppDomainID appDomainId) override;
    HRESULT STDMETHODCALLTYPE AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE AssemblyLoadStarted(AssemblyID assemblyId) override;
    HRESULT STDMETHODCALLTYPE AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE AssemblyUnloadStarted(AssemblyID assemblyId) override;
    HRESULT STDMETHODCALLTYPE AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE ModuleLoadStarted(ModuleID moduleId) override;
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID moduleId) override;
    HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId) override;
    HRESULT STDMETHODCALLTYPE ClassLoadStarted(ClassID classId) override;
    HRESULT STDMETHODCALLTYPE ClassLoadFinished(ClassID classId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE ClassUnloadStarted(ClassID classId) override;
    HRESULT STDMETHODCALLTYPE ClassUnloadFinished(ClassID classId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE FunctionUnloadStarted(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) override;
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchFinished(FunctionID functionId, COR_PRF_JIT_CACHE result) override;
    HRESULT STDMETHODCALLTYPE JITFunctionPitched(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) override;
    HRESULT STDMETHODCALLTYPE ThreadCreated(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE ThreadDestroyed(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId) override;
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationStarted(void) override;
    HRESULT STDMETHODCALLTYPE RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationFinished(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationStarted(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationReturned(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE UnmanagedToManagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override;
    HRESULT STDMETHODCALLTYPE ManagedToUnmanagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendFinished(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendAborted(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeStarted(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeFinished(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadSuspended(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadResumed(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE ObjectAllocated(ObjectID objectId, ClassID classId) override;
    HRESULT STDMETHODCALLTYPE ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) override;
    HRESULT STDMETHODCALLTYPE ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]) override;
    HRESULT STDMETHODCALLTYPE RootReferences(ULONG cRootRefs, ObjectID rootRefIds[]) override;
    HRESULT STDMETHODCALLTYPE ExceptionThrown(ObjectID thrownObjectId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerEnter(UINT_PTR __unused) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerLeave(UINT_PTR __unused) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId) override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave(void) override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable, ULONG cSlots) override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable) override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherFound(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherExecute(void) override;
    HRESULT STDMETHODCALLTYPE ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason) override;
    HRESULT STDMETHODCALLTYPE SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionFinished(void) override;
    HRESULT STDMETHODCALLTYPE FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID) override;
    HRESULT STDMETHODCALLTYPE RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override;
    HRESULT STDMETHODCALLTYPE HandleCreated(GCHandleID handleId, ObjectID initialObjectId) override;
    HRESULT STDMETHODCALLTYPE HandleDestroyed(GCHandleID handleId) override;
    HRESULT STDMETHODCALLTYPE InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData) override;
    HRESULT STDMETHODCALLTYPE ProfilerAttachComplete(void) override;
    HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded(void) override;
    HRESULT STDMETHODCALLTYPE ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl) override;
    HRESULT STDMETHODCALLTYPE ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE MovedReferences2(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE SurvivingReferences2(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], SIZE_T cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[], ObjectID valueRefIds[], GCHandleID rootIds[]) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyReferences(const WCHAR* wszAssemblyPath, ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override;
    HRESULT STDMETHODCALLTYPE ModuleInMemorySymbolsUpdated(ModuleID moduleId) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock, LPCBYTE pILHeader, ULONG cbILHeader) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodUnloaded(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE EventPipeEventDelivered(EVENTPIPE_PROVIDER provider,
                                                      DWORD eventId,
                                                      DWORD eventVersion,
                                                      ULONG cbMetadataBlob,
                                                      LPCBYTE metadataBlob,
                                                      ULONG cbEventData,
                                                      LPCBYTE eventData,
                                                      LPCGUID pActivityId,
                                                      LPCGUID pRelatedActivityId,
                                                      ThreadID eventThread,
                                                      ULONG numStackFrames,
                                                      UINT_PTR stackFrames[]) override;
    HRESULT STDMETHODCALLTYPE EventPipeProviderCreated(EVENTPIPE_PROVIDER provider) override;

public:
    // One and only instance of CorProfilerCallback
    // from which services could be retrieved
    static CorProfilerCallback* GetInstance()
    {
        return _this;
    }
    static IClrLifetime* GetClrLifetime();

// Access to global services
// All services are allocated/started and stopped/deleted by the CorProfilerCallback (no need to use unique_ptr/shared_ptr)
// Their lifetime lasts between Initialize() and Shutdown()
public:
    IThreadsCpuManager* GetThreadsCpuManager() { return _pThreadsCpuManager; }
    IManagedThreadList* GetManagedThreadList() { return _pManagedThreadList; }
    IStackSamplerLoopManager* GetStackSamplerLoopManager() { return _pStackSamplerLoopManager; }
    IApplicationStore* GetApplicationStore() { return _pApplicationStore; }

private :
    static CorProfilerCallback* _this;
    std::unique_ptr<IClrLifetime> _pClrLifetime = nullptr;

    std::atomic<ULONG> _refCount{0};
    ICorProfilerInfo4* _pCorProfilerInfo = nullptr;
    inline static bool _isNet46OrGreater = false;
    std::shared_ptr<IMetricsSender> _metricsSender;
    std::atomic<bool> _isInitialized{false}; // pay attention to keeping ProfilerEngineStatus::IsProfilerEngiveActive in sync with this!

    // The pointer here are observable pointer which means that they are used only to access the data.
    // Their lifetime is managed by the _services vector.
    IThreadsCpuManager* _pThreadsCpuManager = nullptr;
    IStackSamplerLoopManager* _pStackSamplerLoopManager = nullptr;
    IManagedThreadList* _pManagedThreadList = nullptr;
    IApplicationStore* _pApplicationStore = nullptr;
    ExceptionsProvider* _pExceptionsProvider = nullptr;
    WallTimeProvider* _pWallTimeProvider = nullptr;
    CpuTimeProvider* _pCpuTimeProvider = nullptr;
    SamplesAggregator* _pSamplesAggregator = nullptr;
    SamplesCollector* _pSamplesCollector = nullptr;

    std::vector<std::unique_ptr<IService>> _services;

    std::unique_ptr<IExporter> _pExporter = nullptr;
    std::unique_ptr<IConfiguration> _pConfiguration = nullptr;
    std::unique_ptr<IAppDomainStore> _pAppDomainStore = nullptr;
    std::unique_ptr<IFrameStore> _pFrameStore = nullptr;

private:
    static void ConfigureDebugLog();
    static void InspectRuntimeCompatibility(IUnknown* corProfilerInfoUnk);
    static void InspectProcessorInfo();
    static void InspectRuntimeVersion(ICorProfilerInfo4* pCorProfilerInfo);
    static const char* SysInfoProcessorArchitectureToStr(WORD wProcArch);

    void DisposeInternal();
    bool InitializeServices();
    bool DisposeServices();
    bool StartServices();
    bool StopServices();


    template <class T, typename... ArgTypes>
    T* RegisterService(ArgTypes&&... args)
    {
        _services.push_back(std::make_unique<T>(std::forward<ArgTypes>(args)...));
        return dynamic_cast<T*>(_services.back().get());
    }
};
