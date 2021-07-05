#include "cor_profiler.h"

#include "logging.h"

namespace datadog
{
namespace nativeloader
{

    CorProfiler::CorProfiler(DynamicDispatcher* dispatcher) : ref_count_(0), dispatcher(dispatcher)
    {
        Debug("CorProfiler::.ctor");
    }

    CorProfiler::~CorProfiler()
    {
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::QueryInterface(REFIID riid, void** ppvObject)
    {
        Debug("CorProfiler::QueryInterface");
        if (riid == __uuidof(ICorProfilerCallback10) || riid == __uuidof(ICorProfilerCallback9) ||
            riid == __uuidof(ICorProfilerCallback8) || riid == __uuidof(ICorProfilerCallback7) ||
            riid == __uuidof(ICorProfilerCallback6) || riid == __uuidof(ICorProfilerCallback5) ||
            riid == __uuidof(ICorProfilerCallback4) || riid == __uuidof(ICorProfilerCallback3) ||
            riid == __uuidof(ICorProfilerCallback2) || riid == __uuidof(ICorProfilerCallback) || riid == IID_IUnknown)
        {
            *ppvObject = this;
            this->AddRef();
            return S_OK;
        }

        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE CorProfiler::AddRef(void)
    {
        Debug("CorProfiler::AddRef");
        return std::atomic_fetch_add(&this->ref_count_, 1) + 1;
    }

    ULONG STDMETHODCALLTYPE CorProfiler::Release(void)
    {
        Debug("CorProfiler::Release");
        int count = std::atomic_fetch_sub(&this->ref_count_, 1) - 1;

        if (count <= 0)
        {
            delete this;
        }

        return count;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Initialize(IUnknown* pICorProfilerInfoUnk)
    {
        Debug("CorProfiler::Initialize");
        return dispatcher->Execute([pICorProfilerInfoUnk](ICorProfilerCallback10* pCallback) {
            return pCallback->Initialize(pICorProfilerInfoUnk);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
    {
        Debug("CorProfiler::Shutdown");
        return dispatcher->Execute([](ICorProfilerCallback10* pCallback) { return pCallback->Shutdown(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationStarted(AppDomainID appDomainId)
    {
        Debug("CorProfiler::AppDomainCreationStarted");
        return dispatcher->Execute([appDomainId](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainCreationStarted(appDomainId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        Debug("CorProfiler::AppDomainCreationFinished");
        return dispatcher->Execute([appDomainId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainCreationFinished(appDomainId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownStarted(AppDomainID appDomainId)
    {
        Debug("CorProfiler::AppDomainShutdownStarted");
        return dispatcher->Execute([appDomainId](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainShutdownStarted(appDomainId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        Debug("CorProfiler::AppDomainShutdownFinished");
        return dispatcher->Execute([appDomainId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainShutdownFinished(appDomainId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadStarted(AssemblyID assemblyId)
    {
        Debug("CorProfiler::AssemblyLoadStarted");
        return dispatcher->Execute(
            [assemblyId](ICorProfilerCallback10* pCallback) { return pCallback->AssemblyLoadStarted(assemblyId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        Debug("CorProfiler::AssemblyLoadFinished");
        return dispatcher->Execute([assemblyId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AssemblyLoadFinished(assemblyId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadStarted(AssemblyID assemblyId)
    {
        Debug("CorProfiler::AssemblyUnloadStarted");
        return dispatcher->Execute(
            [assemblyId](ICorProfilerCallback10* pCallback) { return pCallback->AssemblyUnloadStarted(assemblyId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        Debug("CorProfiler::AssemblyUnloadFinished");
        return dispatcher->Execute([assemblyId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AssemblyUnloadFinished(assemblyId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadStarted(ModuleID moduleId)
    {
        Debug("CorProfiler::ModuleLoadStarted");
        return dispatcher->Execute(
            [moduleId](ICorProfilerCallback10* pCallback) { return pCallback->ModuleLoadStarted(moduleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        Debug("CorProfiler::ModuleLoadFinished");
        return dispatcher->Execute([moduleId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleLoadFinished(moduleId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID moduleId)
    {
        Debug("CorProfiler::ModuleUnloadStarted");
        return dispatcher->Execute(
            [moduleId](ICorProfilerCallback10* pCallback) { return pCallback->ModuleUnloadStarted(moduleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        Debug("CorProfiler::ModuleUnloadFinished");
        return dispatcher->Execute([moduleId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleUnloadFinished(moduleId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId)
    {
        Debug("CorProfiler::ModuleAttachedToAssembly");
        return dispatcher->Execute([moduleId, AssemblyId](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleAttachedToAssembly(moduleId, AssemblyId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadStarted(ClassID classId)
    {
        Debug("CorProfiler::ClassLoadStarted");
        return dispatcher->Execute(
            [classId](ICorProfilerCallback10* pCallback) { return pCallback->ClassLoadStarted(classId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadFinished(ClassID classId, HRESULT hrStatus)
    {
        Debug("CorProfiler::ClassLoadFinished");
        return dispatcher->Execute([classId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ClassLoadFinished(classId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadStarted(ClassID classId)
    {
        Debug("CorProfiler::ClassUnloadStarted");
        return dispatcher->Execute(
            [classId](ICorProfilerCallback10* pCallback) { return pCallback->ClassUnloadStarted(classId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadFinished(ClassID classId, HRESULT hrStatus)
    {
        Debug("CorProfiler::ClassUnloadFinished");
        return dispatcher->Execute([classId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ClassUnloadFinished(classId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FunctionUnloadStarted(FunctionID functionId)
    {
        Debug("CorProfiler::FunctionUnloadStarted");
        return dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->FunctionUnloadStarted(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
    {
        Debug("CorProfiler::JITCompilationStarted");
        return dispatcher->Execute([functionId, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCompilationStarted(functionId, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                  BOOL fIsSafeToBlock)
    {
        Debug("CorProfiler::JITCompilationFinished");
        return dispatcher->Execute([functionId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId,
                                                                          BOOL* pbUseCachedFunction)
    {
        Debug("CorProfiler::JITCachedFunctionSearchStarted");
        return dispatcher->Execute([functionId, pbUseCachedFunction](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchFinished(FunctionID functionId,
                                                                           COR_PRF_JIT_CACHE result)
    {
        Debug("CorProfiler::JITCachedFunctionSearchFinished");
        return dispatcher->Execute([functionId, result](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCachedFunctionSearchFinished(functionId, result);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITFunctionPitched(FunctionID functionId)
    {
        Debug("CorProfiler::JITFunctionPitched");
        return dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->JITFunctionPitched(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
    {
        Debug("CorProfiler::JITInlining");
        return dispatcher->Execute([callerId, calleeId, pfShouldInline](ICorProfilerCallback10* pCallback) {
            return pCallback->JITInlining(callerId, calleeId, pfShouldInline);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadCreated(ThreadID threadId)
    {
        Debug("CorProfiler::ThreadCreated");
        return dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->ThreadCreated(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadDestroyed(ThreadID threadId)
    {
        Debug("CorProfiler::ThreadDestroyed");
        return dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->ThreadDestroyed(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId)
    {
        Debug("CorProfiler::ThreadAssignedToOSThread");
        return dispatcher->Execute([managedThreadId, osThreadId](ICorProfilerCallback10* pCallback) {
            return pCallback->ThreadAssignedToOSThread(managedThreadId, osThreadId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationStarted()
    {
        Debug("CorProfiler::RemotingClientInvocationStarted");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingClientInvocationStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        Debug("CorProfiler::RemotingClientSendingMessage");
        return dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingClientSendingMessage(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync)
    {
        Debug("CorProfiler::RemotingClientReceivingReply");
        return dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingClientReceivingReply(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationFinished()
    {
        Debug("CorProfiler::RemotingClientInvocationFinished");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingClientInvocationFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        Debug("CorProfiler::RemotingServerReceivingMessage");
        return dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingServerReceivingMessage(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationStarted()
    {
        Debug("CorProfiler::RemotingServerInvocationStarted");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingServerInvocationStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationReturned()
    {
        Debug("CorProfiler::RemotingServerInvocationReturned");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingServerInvocationReturned(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync)
    {
        Debug("CorProfiler::RemotingServerSendingReply");
        return dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingServerSendingReply(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::UnmanagedToManagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        Debug("CorProfiler::UnmanagedToManagedTransition");
        return dispatcher->Execute([functionId, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->UnmanagedToManagedTransition(functionId, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ManagedToUnmanagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        Debug("CorProfiler::ManagedToUnmanagedTransition");
        return dispatcher->Execute([functionId, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->ManagedToUnmanagedTransition(functionId, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
    {
        Debug("CorProfiler::RuntimeSuspendStarted");
        return dispatcher->Execute([suspendReason](ICorProfilerCallback10* pCallback) {
            return pCallback->RuntimeSuspendStarted(suspendReason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendFinished()
    {
        Debug("CorProfiler::RuntimeSuspendFinished");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeSuspendFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendAborted()
    {
        Debug("CorProfiler::RuntimeSuspendAborted");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeSuspendAborted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeStarted()
    {
        Debug("CorProfiler::RuntimeResumeStarted");
        return dispatcher->Execute([](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeResumeStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeFinished()
    {
        Debug("CorProfiler::RuntimeResumeFinished");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeResumeFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadSuspended(ThreadID threadId)
    {
        Debug("CorProfiler::RuntimeThreadSuspended");
        return dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeThreadSuspended(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadResumed(ThreadID threadId)
    {
        Debug("CorProfiler::RuntimeThreadResumed");
        return dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeThreadResumed(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                                           ObjectID newObjectIDRangeStart[],
                                                           ULONG cObjectIDRangeLength[])
    {
        Debug("CorProfiler::MovedReferences");
        return dispatcher->Execute([cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                    cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                              cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectAllocated(ObjectID objectId, ClassID classId)
    {
        Debug("CorProfiler::ObjectAllocated");
        return dispatcher->Execute([objectId, classId](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectAllocated(objectId, classId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[],
                                                                   ULONG cObjects[])
    {
        Debug("CorProfiler::ObjectsAllocatedByClass");
        return dispatcher->Execute([cClassCount, classIds, cObjects](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectsAllocatedByClass(cClassCount, classIds, cObjects);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs,
                                                            ObjectID objectRefIds[])
    {
        Debug("CorProfiler::ObjectReferences");
        return dispatcher->Execute([objectId, classId, cObjectRefs, objectRefIds](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectReferences(objectId, classId, cObjectRefs, objectRefIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[])
    {
        Debug("CorProfiler::RootReferences");
        return dispatcher->Execute([cRootRefs, rootRefIds](ICorProfilerCallback10* pCallback) {
            return pCallback->RootReferences(cRootRefs, rootRefIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionThrown(ObjectID thrownObjectId)
    {
        Debug("CorProfiler::ExceptionThrown");
        return dispatcher->Execute(
            [thrownObjectId](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionThrown(thrownObjectId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionEnter(FunctionID functionId)
    {
        Debug("CorProfiler::ExceptionSearchFunctionEnter");
        return dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchFunctionEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionLeave()
    {
        Debug("CorProfiler::ExceptionSearchFunctionLeave");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionSearchFunctionLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterEnter(FunctionID functionId)
    {
        Debug("CorProfiler::ExceptionSearchFilterEnter");
        return dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchFilterEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterLeave()
    {
        Debug("CorProfiler::ExceptionSearchFilterLeave");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionSearchFilterLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchCatcherFound(FunctionID functionId)
    {
        Debug("CorProfiler::ExceptionSearchCatcherFound");
        return dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchCatcherFound(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerEnter(UINT_PTR __unused)
    {
        Debug("CorProfiler::ExceptionOSHandlerEnter");
        return dispatcher->Execute(
            [__unused](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionOSHandlerEnter(__unused); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerLeave(UINT_PTR __unused)
    {
        Debug("CorProfiler::ExceptionOSHandlerLeave");
        return dispatcher->Execute(
            [__unused](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionOSHandlerLeave(__unused); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionEnter(FunctionID functionId)
    {
        Debug("CorProfiler::ExceptionUnwindFunctionEnter");
        return dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionUnwindFunctionEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionLeave()
    {
        Debug("CorProfiler::ExceptionUnwindFunctionLeave");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionUnwindFunctionLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyEnter(FunctionID functionId)
    {
        Debug("CorProfiler::ExceptionUnwindFinallyEnter");
        return dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionUnwindFinallyEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyLeave()
    {
        Debug("CorProfiler::ExceptionUnwindFinallyLeave");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionUnwindFinallyLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
    {
        Debug("CorProfiler::ExceptionCatcherEnter");
        return dispatcher->Execute([functionId, objectId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionCatcherEnter(functionId, objectId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherLeave()
    {
        Debug("CorProfiler::ExceptionCatcherLeave");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCatcherLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID,
                                                                   void* pVTable, ULONG cSlots)
    {
        Debug("CorProfiler::COMClassicVTableCreated");
        return dispatcher->Execute(
            [wrappedClassId, implementedIID, pVTable, cSlots](ICorProfilerCallback10* pCallback) {
                return pCallback->COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID,
                                                                     void* pVTable)
    {
        Debug("CorProfiler::COMClassicVTableDestroyed");
        return dispatcher->Execute([wrappedClassId, implementedIID, pVTable](ICorProfilerCallback10* pCallback) {
            return pCallback->COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherFound()
    {
        Debug("CorProfiler::ExceptionCLRCatcherFound");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCLRCatcherFound(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherExecute()
    {
        Debug("CorProfiler::ExceptionCLRCatcherExecute");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCLRCatcherExecute(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[])
    {
        Debug("CorProfiler::ThreadNameChanged");
        return dispatcher->Execute([threadId, cchName, name](ICorProfilerCallback10* pCallback) {
            return pCallback->ThreadNameChanged(threadId, cchName, name);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[],
                                                                    COR_PRF_GC_REASON reason)
    {
        Debug("CorProfiler::GarbageCollectionStarted");
        return dispatcher->Execute([cGenerations, generationCollected, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->GarbageCollectionStarted(cGenerations, generationCollected, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges,
                                                               ObjectID objectIDRangeStart[],
                                                               ULONG cObjectIDRangeLength[])
    {
        Debug("CorProfiler::SurvivingReferences");
        return dispatcher->Execute([cSurvivingObjectIDRanges, objectIDRangeStart,
                                    cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionFinished()
    {
        Debug("CorProfiler::GarbageCollectionFinished");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->GarbageCollectionFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
    {
        Debug("CorProfiler::FinalizeableObjectQueued");
        return dispatcher->Execute([finalizerFlags, objectID](ICorProfilerCallback10* pCallback) {
            return pCallback->FinalizeableObjectQueued(finalizerFlags, objectID);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[],
                                                           COR_PRF_GC_ROOT_KIND rootKinds[],
                                                           COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
    {
        Debug("CorProfiler::RootReferences2");
        return dispatcher->Execute(
            [cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds](ICorProfilerCallback10* pCallback) {
                return pCallback->RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleCreated(GCHandleID handleId, ObjectID initialObjectId)
    {
        Debug("CorProfiler::HandleCreated");
        return dispatcher->Execute([handleId, initialObjectId](ICorProfilerCallback10* pCallback) {
            return pCallback->HandleCreated(handleId, initialObjectId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleDestroyed(GCHandleID handleId)
    {
        Debug("CorProfiler::HandleDestroyed");
        return dispatcher->Execute(
            [handleId](ICorProfilerCallback10* pCallback) { return pCallback->HandleDestroyed(handleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData,
                                                               UINT cbClientData)
    {
        Debug("CorProfiler::InitializeForAttach");
        return dispatcher->Execute(
            [pCorProfilerInfoUnk, pvClientData, cbClientData](ICorProfilerCallback10* pCallback) {
                return pCallback->InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerAttachComplete()
    {
        Debug("CorProfiler::ProfilerAttachComplete");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ProfilerAttachComplete(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded()
    {
        Debug("CorProfiler::ProfilerDetachSucceeded");
        return dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ProfilerDetachSucceeded(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                                   BOOL fIsSafeToBlock)
    {
        Debug("CorProfiler::ReJITCompilationStarted");
        return dispatcher->Execute([functionId, rejitId, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                              ICorProfilerFunctionControl* pFunctionControl)
    {
        Debug("CorProfiler::GetReJITParameters");
        return dispatcher->Execute([moduleId, methodId, pFunctionControl](ICorProfilerCallback10* pCallback) {
            return pCallback->GetReJITParameters(moduleId, methodId, pFunctionControl);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId,
                                                                    HRESULT hrStatus, BOOL fIsSafeToBlock)
    {
        Debug("CorProfiler::ReJITCompilationFinished");
        return dispatcher->Execute([functionId, rejitId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                      HRESULT hrStatus)
    {
        Debug("CorProfiler::ReJITError");
        return dispatcher->Execute([moduleId, methodId, functionId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ReJITError(moduleId, methodId, functionId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences2(ULONG cMovedObjectIDRanges,
                                                            ObjectID oldObjectIDRangeStart[],
                                                            ObjectID newObjectIDRangeStart[],
                                                            SIZE_T cObjectIDRangeLength[])
    {
        Debug("CorProfiler::MovedReferences2");
        return dispatcher->Execute([cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                    cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                               cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences2(ULONG cSurvivingObjectIDRanges,
                                                                ObjectID objectIDRangeStart[],
                                                                SIZE_T cObjectIDRangeLength[])
    {
        Debug("CorProfiler::SurvivingReferences2");
        return dispatcher->Execute([cSurvivingObjectIDRanges, objectIDRangeStart,
                                    cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[],
                                                                                 ObjectID valueRefIds[],
                                                                                 GCHandleID rootIds[])
    {
        Debug("CorProfiler::ConditionalWeakTableElementReferences");
        return dispatcher->Execute([cRootRefs, keyRefIds, valueRefIds, rootIds](ICorProfilerCallback10* pCallback) {
            return pCallback->ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                                 ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
    {
        Debug("CorProfiler::GetAssemblyReferences");
        return dispatcher->Execute([wszAssemblyPath, pAsmRefProvider](ICorProfilerCallback10* pCallback) {
            return pCallback->GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleInMemorySymbolsUpdated(ModuleID moduleId)
    {
        Debug("CorProfiler::ModuleInMemorySymbolsUpdated");
        return dispatcher->Execute([moduleId](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleInMemorySymbolsUpdated(moduleId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationStarted(FunctionID functionId,
                                                                              BOOL fIsSafeToBlock, LPCBYTE ilHeader,
                                                                              ULONG cbILHeader)
    {
        Debug("CorProfiler::DynamicMethodJITCompilationStarted");
        return dispatcher->Execute(
            [functionId, fIsSafeToBlock, ilHeader, cbILHeader](ICorProfilerCallback10* pCallback) {
                return pCallback->DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                               BOOL fIsSafeToBlock)
    {
        Debug("CorProfiler::DynamicMethodJITCompilationFinished");
        return dispatcher->Execute([functionId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodUnloaded(FunctionID functionId)
    {
        Debug("CorProfiler::DynamicMethodUnloaded");
        return dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->DynamicMethodUnloaded(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeEventDelivered(EVENTPIPE_PROVIDER provider, DWORD eventId,
                                                                   DWORD eventVersion, ULONG cbMetadataBlob,
                                                                   LPCBYTE metadataBlob, ULONG cbEventData,
                                                                   LPCBYTE eventData, LPCGUID pActivityId,
                                                                   LPCGUID pRelatedActivityId, ThreadID eventThread,
                                                                   ULONG numStackFrames, UINT_PTR stackFrames[])
    {
        Debug("CorProfiler::EventPipeEventDelivered");
        return dispatcher->Execute([provider, eventId, eventVersion, cbMetadataBlob, metadataBlob, cbEventData,
                                    eventData, pActivityId, pRelatedActivityId, eventThread, numStackFrames,
                                    stackFrames](ICorProfilerCallback10* pCallback) {
            return pCallback->EventPipeEventDelivered(provider, eventId, eventVersion, cbMetadataBlob, metadataBlob,
                                                      cbEventData, eventData, pActivityId, pRelatedActivityId,
                                                      eventThread, numStackFrames, stackFrames);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
    {
        Debug("CorProfiler::EventPipeProviderCreated");
        return dispatcher->Execute(
            [provider](ICorProfilerCallback10* pCallback) { return pCallback->EventPipeProviderCreated(provider); });
    }

} // namespace nativeloader
} // namespace datadog