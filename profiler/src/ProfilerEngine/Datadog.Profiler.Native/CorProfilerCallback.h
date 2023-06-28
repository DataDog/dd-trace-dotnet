// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "AllocationsProvider.h"
#include "ApplicationStore.h"
#include "ClrEventsParser.h"
#include "ExceptionsProvider.h"
#include "IAppDomainStore.h"
#include "IClrLifetime.h"
#include "IConfiguration.h"
#include "IDebugInfoStore.h"
#include "IExporter.h"
#include "IFrameStore.h"
#include "IMetricsSender.h"
#include "ISamplesProvider.h"
#include "WallTimeProvider.h"
#include "CpuTimeProvider.h"
#include "SamplesCollector.h"
#include "GarbageCollectionProvider.h"
#include "StopTheWorldGCProvider.h"
#include "LiveObjectsProvider.h"
#include "IRuntimeInfo.h"
#include "IEnabledProfilers.h"
#include "MetricsRegistry.h"
#include "ProxyMetric.h"
#include "IAllocationsRecorder.h"

#include "shared/src/native-src/string.h"

#include <atomic>
#include <memory>
#include <vector>

class ContentionProvider;
class IService;
class IThreadsCpuManager;
class IManagedThreadList;
class IStackSamplerLoopManager;
class IConfiguration;
class IExporter;

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
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    ULONG STDMETHODCALLTYPE GetRefCount() const;
    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override;
    HRESULT STDMETHODCALLTYPE Shutdown() override;
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
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationStarted() override;
    HRESULT STDMETHODCALLTYPE RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationFinished() override;
    HRESULT STDMETHODCALLTYPE RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationStarted() override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationReturned() override;
    HRESULT STDMETHODCALLTYPE RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync) override;
    HRESULT STDMETHODCALLTYPE UnmanagedToManagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override;
    HRESULT STDMETHODCALLTYPE ManagedToUnmanagedTransition(FunctionID functionId, COR_PRF_TRANSITION_REASON reason) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendFinished() override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendAborted() override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeStarted() override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeFinished() override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadSuspended(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadResumed(ThreadID threadId) override;
    HRESULT STDMETHODCALLTYPE MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE ObjectAllocated(ObjectID objectId, ClassID classId) override;
    HRESULT STDMETHODCALLTYPE ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[], ULONG cObjects[]) override;
    HRESULT STDMETHODCALLTYPE ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs, ObjectID objectRefIds[]) override;
    HRESULT STDMETHODCALLTYPE RootReferences(ULONG cRootRefs, ObjectID rootRefIds[]) override;
    HRESULT STDMETHODCALLTYPE ExceptionThrown(ObjectID thrownObjectId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave() override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave() override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerEnter(UINT_PTR __unused) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerLeave(UINT_PTR __unused) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave() override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave() override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId) override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave() override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable, ULONG cSlots) override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID, void* pVTable) override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherFound() override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherExecute() override;
    HRESULT STDMETHODCALLTYPE ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason) override;
    HRESULT STDMETHODCALLTYPE SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionFinished() override;
    HRESULT STDMETHODCALLTYPE FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID) override;
    HRESULT STDMETHODCALLTYPE RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]) override;
    HRESULT STDMETHODCALLTYPE HandleCreated(GCHandleID handleId, ObjectID initialObjectId) override;
    HRESULT STDMETHODCALLTYPE HandleDestroyed(GCHandleID handleId) override;
    HRESULT STDMETHODCALLTYPE InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData, UINT cbClientData) override;
    HRESULT STDMETHODCALLTYPE ProfilerAttachComplete() override;
    HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded() override;
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

    std::string GetRuntimeDescription()
    {
        return _runtimeDescription;
    }

    IClrLifetime* GetClrLifetime() const;

// Access to global services
// All services are allocated/started and stopped/deleted by the CorProfilerCallback (no need to use unique_ptr/shared_ptr)
// Their lifetime lasts between Initialize() and Shutdown()
public:
    IThreadsCpuManager* GetThreadsCpuManager() { return _pThreadsCpuManager; }
    IManagedThreadList* GetManagedThreadList() { return _pManagedThreadList; }
    IManagedThreadList* GetCodeHotspotThreadList() { return _pCodeHotspotsThreadList; }
    IStackSamplerLoopManager* GetStackSamplerLoopManager() { return _pStackSamplerLoopManager; }
    IApplicationStore* GetApplicationStore() { return _pApplicationStore; }
    IExporter* GetExporter() { return _pExporter.get(); }

private :
    static CorProfilerCallback* _this;
    std::string _runtimeDescription;
    std::unique_ptr<IClrLifetime> _pClrLifetime = nullptr;

    std::atomic<ULONG> _refCount{0};
    ICorProfilerInfo5* _pCorProfilerInfo = nullptr;
    ICorProfilerInfo12* _pCorProfilerInfoEvents = nullptr;
    ICorProfilerInfo13* _pCorProfilerInfoLiveHeap = nullptr;

    std::unique_ptr<ClrEventsParser> _pClrEventsParser = nullptr;
    EVENTPIPE_SESSION _session{0};
    inline static bool _isNet46OrGreater = false;
    std::shared_ptr<IMetricsSender> _metricsSender;
    std::atomic<bool> _isInitialized{false}; // pay attention to keeping ProfilerEngineStatus::IsProfilerEngiveActive in sync with this!

    // The pointer here are observable pointer which means that they are used only to access the data.
    // Their lifetime is managed by the _services vector.
    IThreadsCpuManager* _pThreadsCpuManager = nullptr;
    IStackSamplerLoopManager* _pStackSamplerLoopManager = nullptr;
    IManagedThreadList* _pManagedThreadList = nullptr;
    IManagedThreadList* _pCodeHotspotsThreadList = nullptr;
    IApplicationStore* _pApplicationStore = nullptr;
    ExceptionsProvider* _pExceptionsProvider = nullptr;
    WallTimeProvider* _pWallTimeProvider = nullptr;
    CpuTimeProvider* _pCpuTimeProvider = nullptr;
    AllocationsProvider* _pAllocationsProvider = nullptr;
    ContentionProvider* _pContentionProvider = nullptr;
    SamplesCollector* _pSamplesCollector = nullptr;
    StopTheWorldGCProvider* _pStopTheWorldProvider = nullptr;
    GarbageCollectionProvider* _pGarbageCollectionProvider = nullptr;
    LiveObjectsProvider* _pLiveObjectsProvider = nullptr;

    std::vector<std::unique_ptr<IService>> _services;

    std::unique_ptr<IExporter> _pExporter = nullptr;
    std::unique_ptr<IConfiguration> _pConfiguration = nullptr;
    std::unique_ptr<IAppDomainStore> _pAppDomainStore = nullptr;
    std::unique_ptr<IFrameStore> _pFrameStore = nullptr;
    std::unique_ptr<IRuntimeInfo> _pRuntimeInfo = nullptr;
    std::unique_ptr<IEnabledProfilers> _pEnabledProfilers = nullptr;
    std::unique_ptr<IAllocationsRecorder> _pAllocationsRecorder = nullptr;
    std::unique_ptr<IDebugInfoStore> _pDebugInfoStore = nullptr;

    MetricsRegistry _metricsRegistry;
    std::shared_ptr<ProxyMetric> _managedThreadsMetric;
    std::shared_ptr<ProxyMetric> _managedThreadsWithContextMetric;

    std::unique_ptr<ISamplesProvider> _gcThreadsCpuProvider;

private:
    static void ConfigureDebugLog();
    static void InspectRuntimeCompatibility(IUnknown* corProfilerInfoUnk, uint16_t& runtimeMajor, uint16_t& runtimeMinor);
    static void InspectProcessorInfo();
    static const char* SysInfoProcessorArchitectureToStr(WORD wProcArch);
    static void PrintEnvironmentVariables();

    void InspectRuntimeVersion(ICorProfilerInfo5* pCorProfilerInfo, USHORT& major, USHORT& minor, COR_PRF_RUNTIME_TYPE& runtimeType);
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
