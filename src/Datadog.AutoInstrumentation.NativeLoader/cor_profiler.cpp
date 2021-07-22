#include "cor_profiler.h"

#include "logging.h"
#include "dynamic_dispatcher.h"

namespace datadog::shared::nativeloader
{

    CorProfiler::CorProfiler(DynamicDispatcher* dispatcher) : m_refCount(0), m_dispatcher(dispatcher)
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
        return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
    }

    ULONG STDMETHODCALLTYPE CorProfiler::Release(void)
    {
        Debug("CorProfiler::Release");
        int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

        if (count <= 0)
        {
            delete this;
        }

        return count;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Initialize(IUnknown* pICorProfilerInfoUnk)
    {
        Debug("CorProfiler::Initialize");

        // get Profiler interface ICorProfilerInfo6 for net46+
        ICorProfilerInfo6* info6 = nullptr;
        HRESULT hr = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo6), (void**) &info6);
        if (FAILED(hr))
        {
            Warn("Failed to attach profiler: interface ICorProfilerInfo6 not found.");
            return E_FAIL;
        }

        // Gets the initial value for the event mask
        DWORD mask_low;
        DWORD mask_hi;
        hr = info6->GetEventMask2(&mask_low, &mask_hi);
        if (FAILED(hr))
        {
            Warn("Error getting the event mask.");
            return E_FAIL;
        }

        Debug("MaskLow: ", mask_low);
        Debug("MaskHi : ", mask_hi);

        // Execute all Initialize functions from the dispatcher and collect each event mask
        HRESULT dispatcherResult = m_dispatcher->Execute(
            [info6, &mask_low, &mask_hi, pICorProfilerInfoUnk](ICorProfilerCallback10* pCallback) {
                HRESULT localResult = pCallback->Initialize(pICorProfilerInfoUnk);
                if (SUCCEEDED(localResult))
                {
                    DWORD local_mask_low;
                    DWORD local_mask_hi;
                    HRESULT hr = info6->GetEventMask2(&local_mask_low, &local_mask_hi);
                    if (SUCCEEDED(hr))
                    {
                        mask_low = mask_low | local_mask_low;
                        mask_hi = mask_hi | local_mask_hi;

                        Debug("*LocalMaskLow: ", local_mask_low);
                        Debug("*LocalMaskHi : ", local_mask_hi);
                    }
                    else
                    {
                        Warn("Error getting the event mask.");
                    }
                }
                return localResult;
            });

        Debug("*MaskLow: ", mask_low);
        Debug("*MaskHi : ", mask_hi);

        // Sets the final event mask for the profiler
        hr = info6->SetEventMask2(mask_low, mask_hi);
        if (FAILED(hr))
        {
            Warn("Error setting the event mask.");
            return E_FAIL;
        }

        return dispatcherResult;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
    {
        Debug("CorProfiler::Shutdown");
        return m_dispatcher->Execute([](ICorProfilerCallback10* pCallback) { return pCallback->Shutdown(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationStarted(AppDomainID appDomainId)
    {
        Verbose("CorProfiler::AppDomainCreationStarted");
        return m_dispatcher->Execute([appDomainId](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainCreationStarted(appDomainId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::AppDomainCreationFinished");
        return m_dispatcher->Execute([appDomainId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainCreationFinished(appDomainId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownStarted(AppDomainID appDomainId)
    {
        Verbose("CorProfiler::AppDomainShutdownStarted");
        return m_dispatcher->Execute([appDomainId](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainShutdownStarted(appDomainId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::AppDomainShutdownFinished");
        return m_dispatcher->Execute([appDomainId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AppDomainShutdownFinished(appDomainId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadStarted(AssemblyID assemblyId)
    {
        Verbose("CorProfiler::AssemblyLoadStarted");
        return m_dispatcher->Execute(
            [assemblyId](ICorProfilerCallback10* pCallback) { return pCallback->AssemblyLoadStarted(assemblyId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::AssemblyLoadFinished");
        return m_dispatcher->Execute([assemblyId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AssemblyLoadFinished(assemblyId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadStarted(AssemblyID assemblyId)
    {
        Verbose("CorProfiler::AssemblyUnloadStarted");
        return m_dispatcher->Execute(
            [assemblyId](ICorProfilerCallback10* pCallback) { return pCallback->AssemblyUnloadStarted(assemblyId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::AssemblyUnloadFinished");
        return m_dispatcher->Execute([assemblyId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->AssemblyUnloadFinished(assemblyId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadStarted(ModuleID moduleId)
    {
        Verbose("CorProfiler::ModuleLoadStarted");
        return m_dispatcher->Execute(
            [moduleId](ICorProfilerCallback10* pCallback) { return pCallback->ModuleLoadStarted(moduleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::ModuleLoadFinished");
        return m_dispatcher->Execute([moduleId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleLoadFinished(moduleId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID moduleId)
    {
        Verbose("CorProfiler::ModuleUnloadStarted");
        return m_dispatcher->Execute(
            [moduleId](ICorProfilerCallback10* pCallback) { return pCallback->ModuleUnloadStarted(moduleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::ModuleUnloadFinished");
        return m_dispatcher->Execute([moduleId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleUnloadFinished(moduleId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId)
    {
        Verbose("CorProfiler::ModuleAttachedToAssembly");
        return m_dispatcher->Execute([moduleId, AssemblyId](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleAttachedToAssembly(moduleId, AssemblyId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadStarted(ClassID classId)
    {
        Verbose("CorProfiler::ClassLoadStarted");
        return m_dispatcher->Execute(
            [classId](ICorProfilerCallback10* pCallback) { return pCallback->ClassLoadStarted(classId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadFinished(ClassID classId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::ClassLoadFinished");
        return m_dispatcher->Execute([classId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ClassLoadFinished(classId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadStarted(ClassID classId)
    {
        Verbose("CorProfiler::ClassUnloadStarted");
        return m_dispatcher->Execute(
            [classId](ICorProfilerCallback10* pCallback) { return pCallback->ClassUnloadStarted(classId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadFinished(ClassID classId, HRESULT hrStatus)
    {
        Verbose("CorProfiler::ClassUnloadFinished");
        return m_dispatcher->Execute([classId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ClassUnloadFinished(classId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FunctionUnloadStarted(FunctionID functionId)
    {
        Verbose("CorProfiler::FunctionUnloadStarted");
        return m_dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->FunctionUnloadStarted(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
    {
        Verbose("CorProfiler::JITCompilationStarted");
        return m_dispatcher->Execute([functionId, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCompilationStarted(functionId, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                  BOOL fIsSafeToBlock)
    {
        Verbose("CorProfiler::JITCompilationFinished");
        return m_dispatcher->Execute([functionId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId,
                                                                          BOOL* pbUseCachedFunction)
    {
        Verbose("CorProfiler::JITCachedFunctionSearchStarted");
        return m_dispatcher->Execute([functionId, pbUseCachedFunction](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchFinished(FunctionID functionId,
                                                                           COR_PRF_JIT_CACHE result)
    {
        Verbose("CorProfiler::JITCachedFunctionSearchFinished");
        return m_dispatcher->Execute([functionId, result](ICorProfilerCallback10* pCallback) {
            return pCallback->JITCachedFunctionSearchFinished(functionId, result);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITFunctionPitched(FunctionID functionId)
    {
        Verbose("CorProfiler::JITFunctionPitched");
        return m_dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->JITFunctionPitched(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
    {
        Verbose("CorProfiler::JITInlining");
        return m_dispatcher->Execute([callerId, calleeId, pfShouldInline](ICorProfilerCallback10* pCallback) {
            return pCallback->JITInlining(callerId, calleeId, pfShouldInline);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadCreated(ThreadID threadId)
    {
        Verbose("CorProfiler::ThreadCreated");
        return m_dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->ThreadCreated(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadDestroyed(ThreadID threadId)
    {
        Verbose("CorProfiler::ThreadDestroyed");
        return m_dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->ThreadDestroyed(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId)
    {
        Verbose("CorProfiler::ThreadAssignedToOSThread");
        return m_dispatcher->Execute([managedThreadId, osThreadId](ICorProfilerCallback10* pCallback) {
            return pCallback->ThreadAssignedToOSThread(managedThreadId, osThreadId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationStarted()
    {
        Verbose("CorProfiler::RemotingClientInvocationStarted");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingClientInvocationStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        Verbose("CorProfiler::RemotingClientSendingMessage");
        return m_dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingClientSendingMessage(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync)
    {
        Verbose("CorProfiler::RemotingClientReceivingReply");
        return m_dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingClientReceivingReply(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationFinished()
    {
        Verbose("CorProfiler::RemotingClientInvocationFinished");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingClientInvocationFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        Verbose("CorProfiler::RemotingServerReceivingMessage");
        return m_dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingServerReceivingMessage(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationStarted()
    {
        Verbose("CorProfiler::RemotingServerInvocationStarted");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingServerInvocationStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationReturned()
    {
        Verbose("CorProfiler::RemotingServerInvocationReturned");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RemotingServerInvocationReturned(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync)
    {
        Verbose("CorProfiler::RemotingServerSendingReply");
        return m_dispatcher->Execute([pCookie, fIsAsync](ICorProfilerCallback10* pCallback) {
            return pCallback->RemotingServerSendingReply(pCookie, fIsAsync);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::UnmanagedToManagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        Verbose("CorProfiler::UnmanagedToManagedTransition");
        return m_dispatcher->Execute([functionId, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->UnmanagedToManagedTransition(functionId, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ManagedToUnmanagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        Verbose("CorProfiler::ManagedToUnmanagedTransition");
        return m_dispatcher->Execute([functionId, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->ManagedToUnmanagedTransition(functionId, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
    {
        Verbose("CorProfiler::RuntimeSuspendStarted");
        return m_dispatcher->Execute([suspendReason](ICorProfilerCallback10* pCallback) {
            return pCallback->RuntimeSuspendStarted(suspendReason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendFinished()
    {
        Verbose("CorProfiler::RuntimeSuspendFinished");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeSuspendFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendAborted()
    {
        Verbose("CorProfiler::RuntimeSuspendAborted");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeSuspendAborted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeStarted()
    {
        Verbose("CorProfiler::RuntimeResumeStarted");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeResumeStarted(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeFinished()
    {
        Verbose("CorProfiler::RuntimeResumeFinished");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeResumeFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadSuspended(ThreadID threadId)
    {
        Verbose("CorProfiler::RuntimeThreadSuspended");
        return m_dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeThreadSuspended(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadResumed(ThreadID threadId)
    {
        Debug("CorProfiler::RuntimeThreadResumed");
        return m_dispatcher->Execute(
            [threadId](ICorProfilerCallback10* pCallback) { return pCallback->RuntimeThreadResumed(threadId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                                           ObjectID newObjectIDRangeStart[],
                                                           ULONG cObjectIDRangeLength[])
    {
        Verbose("CorProfiler::MovedReferences");
        return m_dispatcher->Execute([cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                      cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                              cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectAllocated(ObjectID objectId, ClassID classId)
    {
        Verbose("CorProfiler::ObjectAllocated");
        return m_dispatcher->Execute([objectId, classId](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectAllocated(objectId, classId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[],
                                                                   ULONG cObjects[])
    {
        Verbose("CorProfiler::ObjectsAllocatedByClass");
        return m_dispatcher->Execute([cClassCount, classIds, cObjects](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectsAllocatedByClass(cClassCount, classIds, cObjects);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs,
                                                            ObjectID objectRefIds[])
    {
        Verbose("CorProfiler::ObjectReferences");
        return m_dispatcher->Execute([objectId, classId, cObjectRefs, objectRefIds](ICorProfilerCallback10* pCallback) {
            return pCallback->ObjectReferences(objectId, classId, cObjectRefs, objectRefIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[])
    {
        Verbose("CorProfiler::RootReferences");
        return m_dispatcher->Execute([cRootRefs, rootRefIds](ICorProfilerCallback10* pCallback) {
            return pCallback->RootReferences(cRootRefs, rootRefIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionThrown(ObjectID thrownObjectId)
    {
        Verbose("CorProfiler::ExceptionThrown");
        return m_dispatcher->Execute(
            [thrownObjectId](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionThrown(thrownObjectId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionEnter(FunctionID functionId)
    {
        Verbose("CorProfiler::ExceptionSearchFunctionEnter");
        return m_dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchFunctionEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionLeave()
    {
        Verbose("CorProfiler::ExceptionSearchFunctionLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionSearchFunctionLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterEnter(FunctionID functionId)
    {
        Verbose("CorProfiler::ExceptionSearchFilterEnter");
        return m_dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchFilterEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterLeave()
    {
        Verbose("CorProfiler::ExceptionSearchFilterLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionSearchFilterLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchCatcherFound(FunctionID functionId)
    {
        Verbose("CorProfiler::ExceptionSearchCatcherFound");
        return m_dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionSearchCatcherFound(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerEnter(UINT_PTR __unused)
    {
        Verbose("CorProfiler::ExceptionOSHandlerEnter");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionOSHandlerEnter(NULL); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerLeave(UINT_PTR __unused)
    {
        Verbose("CorProfiler::ExceptionOSHandlerLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionOSHandlerLeave(NULL); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionEnter(FunctionID functionId)
    {
        Verbose("CorProfiler::ExceptionUnwindFunctionEnter");
        return m_dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionUnwindFunctionEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionLeave()
    {
        Verbose("CorProfiler::ExceptionUnwindFunctionLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionUnwindFunctionLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyEnter(FunctionID functionId)
    {
        Verbose("CorProfiler::ExceptionUnwindFinallyEnter");
        return m_dispatcher->Execute([functionId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionUnwindFinallyEnter(functionId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyLeave()
    {
        Verbose("CorProfiler::ExceptionUnwindFinallyLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionUnwindFinallyLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
    {
        Verbose("CorProfiler::ExceptionCatcherEnter");
        return m_dispatcher->Execute([functionId, objectId](ICorProfilerCallback10* pCallback) {
            return pCallback->ExceptionCatcherEnter(functionId, objectId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherLeave()
    {
        Verbose("CorProfiler::ExceptionCatcherLeave");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCatcherLeave(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID,
                                                                   void* pVTable, ULONG cSlots)
    {
        Verbose("CorProfiler::COMClassicVTableCreated");
        return m_dispatcher->Execute(
            [wrappedClassId, implementedIID, pVTable, cSlots](ICorProfilerCallback10* pCallback) {
                return pCallback->COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID,
                                                                     void* pVTable)
    {
        Verbose("CorProfiler::COMClassicVTableDestroyed");
        return m_dispatcher->Execute([wrappedClassId, implementedIID, pVTable](ICorProfilerCallback10* pCallback) {
            return pCallback->COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherFound()
    {
        Verbose("CorProfiler::ExceptionCLRCatcherFound");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCLRCatcherFound(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherExecute()
    {
        Verbose("CorProfiler::ExceptionCLRCatcherExecute");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ExceptionCLRCatcherExecute(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[])
    {
        Verbose("CorProfiler::ThreadNameChanged");
        return m_dispatcher->Execute([threadId, cchName, name](ICorProfilerCallback10* pCallback) {
            return pCallback->ThreadNameChanged(threadId, cchName, name);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[],
                                                                    COR_PRF_GC_REASON reason)
    {
        Verbose("CorProfiler::GarbageCollectionStarted");
        return m_dispatcher->Execute([cGenerations, generationCollected, reason](ICorProfilerCallback10* pCallback) {
            return pCallback->GarbageCollectionStarted(cGenerations, generationCollected, reason);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges,
                                                               ObjectID objectIDRangeStart[],
                                                               ULONG cObjectIDRangeLength[])
    {
        Verbose("CorProfiler::SurvivingReferences");
        return m_dispatcher->Execute([cSurvivingObjectIDRanges, objectIDRangeStart,
                                      cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionFinished()
    {
        Verbose("CorProfiler::GarbageCollectionFinished");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->GarbageCollectionFinished(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
    {
        Verbose("CorProfiler::FinalizeableObjectQueued");
        return m_dispatcher->Execute([finalizerFlags, objectID](ICorProfilerCallback10* pCallback) {
            return pCallback->FinalizeableObjectQueued(finalizerFlags, objectID);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[],
                                                           COR_PRF_GC_ROOT_KIND rootKinds[],
                                                           COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
    {
        Verbose("CorProfiler::RootReferences2");
        return m_dispatcher->Execute(
            [cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds](ICorProfilerCallback10* pCallback) {
                return pCallback->RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleCreated(GCHandleID handleId, ObjectID initialObjectId)
    {
        Verbose("CorProfiler::HandleCreated");
        return m_dispatcher->Execute([handleId, initialObjectId](ICorProfilerCallback10* pCallback) {
            return pCallback->HandleCreated(handleId, initialObjectId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleDestroyed(GCHandleID handleId)
    {
        Verbose("CorProfiler::HandleDestroyed");
        return m_dispatcher->Execute(
            [handleId](ICorProfilerCallback10* pCallback) { return pCallback->HandleDestroyed(handleId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData,
                                                               UINT cbClientData)
    {
        Verbose("CorProfiler::InitializeForAttach");
        return m_dispatcher->Execute(
            [pCorProfilerInfoUnk, pvClientData, cbClientData](ICorProfilerCallback10* pCallback) {
                return pCallback->InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerAttachComplete()
    {
        Verbose("CorProfiler::ProfilerAttachComplete");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ProfilerAttachComplete(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded()
    {
        Verbose("CorProfiler::ProfilerDetachSucceeded");
        return m_dispatcher->Execute(
            [](ICorProfilerCallback10* pCallback) { return pCallback->ProfilerDetachSucceeded(); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                                   BOOL fIsSafeToBlock)
    {
        Verbose("CorProfiler::ReJITCompilationStarted");
        return m_dispatcher->Execute([functionId, rejitId, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                              ICorProfilerFunctionControl* pFunctionControl)
    {
        Verbose("CorProfiler::GetReJITParameters");
        return m_dispatcher->Execute([moduleId, methodId, pFunctionControl](ICorProfilerCallback10* pCallback) {
            return pCallback->GetReJITParameters(moduleId, methodId, pFunctionControl);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId,
                                                                    HRESULT hrStatus, BOOL fIsSafeToBlock)
    {
        Verbose("CorProfiler::ReJITCompilationFinished");
        return m_dispatcher->Execute(
            [functionId, rejitId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
                return pCallback->ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                      HRESULT hrStatus)
    {
        Verbose("CorProfiler::ReJITError");
        return m_dispatcher->Execute([moduleId, methodId, functionId, hrStatus](ICorProfilerCallback10* pCallback) {
            return pCallback->ReJITError(moduleId, methodId, functionId, hrStatus);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences2(ULONG cMovedObjectIDRanges,
                                                            ObjectID oldObjectIDRangeStart[],
                                                            ObjectID newObjectIDRangeStart[],
                                                            SIZE_T cObjectIDRangeLength[])
    {
        Verbose("CorProfiler::MovedReferences2");
        return m_dispatcher->Execute([cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                      cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart,
                                               cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences2(ULONG cSurvivingObjectIDRanges,
                                                                ObjectID objectIDRangeStart[],
                                                                SIZE_T cObjectIDRangeLength[])
    {
        Verbose("CorProfiler::SurvivingReferences2");
        return m_dispatcher->Execute([cSurvivingObjectIDRanges, objectIDRangeStart,
                                      cObjectIDRangeLength](ICorProfilerCallback10* pCallback) {
            return pCallback->SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[],
                                                                                 ObjectID valueRefIds[],
                                                                                 GCHandleID rootIds[])
    {
        Verbose("CorProfiler::ConditionalWeakTableElementReferences");
        return m_dispatcher->Execute([cRootRefs, keyRefIds, valueRefIds, rootIds](ICorProfilerCallback10* pCallback) {
            return pCallback->ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                                 ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
    {
        Verbose("CorProfiler::GetAssemblyReferences");
        return m_dispatcher->Execute([wszAssemblyPath, pAsmRefProvider](ICorProfilerCallback10* pCallback) {
            return pCallback->GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleInMemorySymbolsUpdated(ModuleID moduleId)
    {
        Verbose("CorProfiler::ModuleInMemorySymbolsUpdated");
        return m_dispatcher->Execute([moduleId](ICorProfilerCallback10* pCallback) {
            return pCallback->ModuleInMemorySymbolsUpdated(moduleId);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationStarted(FunctionID functionId,
                                                                              BOOL fIsSafeToBlock, LPCBYTE ilHeader,
                                                                              ULONG cbILHeader)
    {
        Verbose("CorProfiler::DynamicMethodJITCompilationStarted");
        return m_dispatcher->Execute(
            [functionId, fIsSafeToBlock, ilHeader, cbILHeader](ICorProfilerCallback10* pCallback) {
                return pCallback->DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader);
            });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                               BOOL fIsSafeToBlock)
    {
        Verbose("CorProfiler::DynamicMethodJITCompilationFinished");
        return m_dispatcher->Execute([functionId, hrStatus, fIsSafeToBlock](ICorProfilerCallback10* pCallback) {
            return pCallback->DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodUnloaded(FunctionID functionId)
    {
        Verbose("CorProfiler::DynamicMethodUnloaded");
        return m_dispatcher->Execute(
            [functionId](ICorProfilerCallback10* pCallback) { return pCallback->DynamicMethodUnloaded(functionId); });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeEventDelivered(EVENTPIPE_PROVIDER provider, DWORD eventId,
                                                                   DWORD eventVersion, ULONG cbMetadataBlob,
                                                                   LPCBYTE metadataBlob, ULONG cbEventData,
                                                                   LPCBYTE eventData, LPCGUID pActivityId,
                                                                   LPCGUID pRelatedActivityId, ThreadID eventThread,
                                                                   ULONG numStackFrames, UINT_PTR stackFrames[])
    {
        Verbose("CorProfiler::EventPipeEventDelivered");
        return m_dispatcher->Execute([provider, eventId, eventVersion, cbMetadataBlob, metadataBlob, cbEventData,
                                      eventData, pActivityId, pRelatedActivityId, eventThread, numStackFrames,
                                      stackFrames](ICorProfilerCallback10* pCallback) {
            return pCallback->EventPipeEventDelivered(provider, eventId, eventVersion, cbMetadataBlob, metadataBlob,
                                                      cbEventData, eventData, pActivityId, pRelatedActivityId,
                                                      eventThread, numStackFrames, stackFrames);
        });
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
    {
        Verbose("CorProfiler::EventPipeProviderCreated");
        return m_dispatcher->Execute(
            [provider](ICorProfilerCallback10* pCallback) { return pCallback->EventPipeProviderCreated(provider); });
    }

} // namespace datadog::shared::nativeloader