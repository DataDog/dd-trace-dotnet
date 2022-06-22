#include "cor_profiler.h"
#include "log.h"
#include "dynamic_dispatcher.h"
#include "util.h"
#include "instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_function_control.h"
#include "instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_info.h"
#include "instrumented_assembly_generator/instrumented_assembly_generator_helper.h"

using namespace shared;

namespace datadog::shared::nativeloader
{
#define STR(x) #x
#define RunInAllProfilers(EXPR)                                                                                        \
    HRESULT gHR = S_OK;                                                                                                \
    if (m_cpProfiler != nullptr)                                                                                       \
    {                                                                                                                  \
        HRESULT hr = m_cpProfiler->EXPR;                                                                               \
        if (FAILED(hr))                                                                                                \
        {                                                                                                              \
            std::ostringstream hexValue;                                                                               \
            hexValue << std::hex << hr;                                                                                \
            Log::Warn("CorProfiler::", STR(EXPR), ": [Continuous Profiler] Error in ", STR(EXPR),                           \
                 " call: ", hexValue.str());                                                                           \
            gHR = hr;                                                                                                  \
        }                                                                                                              \
    }                                                                                                                  \
    if (m_tracerProfiler != nullptr)                                                                                   \
    {                                                                                                                  \
        HRESULT hr = m_tracerProfiler->EXPR;                                                                           \
        if (FAILED(hr))                                                                                                \
        {                                                                                                              \
            std::ostringstream hexValue;                                                                               \
            hexValue << std::hex << hr;                                                                                \
            Log::Warn("CorProfiler::", STR(EXPR), ": [Tracer] Error in ", STR(EXPR), " call: ", hexValue.str());            \
            gHR = hr;                                                                                                  \
        }                                                                                                              \
    }                                                                                                                  \
    if (m_customProfiler != nullptr)                                                                                   \
    {                                                                                                                  \
        HRESULT hr = m_customProfiler->EXPR;                                                                           \
        if (FAILED(hr))                                                                                                \
        {                                                                                                              \
            std::ostringstream hexValue;                                                                               \
            hexValue << std::hex << hr;                                                                                \
            Log::Warn("CorProfiler::", STR(EXPR), ": [Custom] Error in ", STR(EXPR), " call: ", hexValue.str());            \
            gHR = hr;                                                                                                  \
        }                                                                                                              \
    }                                                                                                                  \
    return gHR;

    CorProfiler* CorProfiler::m_this = nullptr;

    CorProfiler::CorProfiler(IDynamicDispatcher* dispatcher) :
        m_refCount(0), m_dispatcher(dispatcher), m_cpProfiler(nullptr), m_tracerProfiler(nullptr), m_customProfiler(nullptr)
    {
        Log::Debug("CorProfiler::.ctor");
    }

    CorProfiler::~CorProfiler()
    {
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::QueryInterface(REFIID riid, void** ppvObject)
    {
        Log::Debug("CorProfiler::QueryInterface");
        if (ppvObject == nullptr)
        {
            return E_POINTER;
        }

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
        Log::Debug("CorProfiler::AddRef");
        return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
    }

    ULONG STDMETHODCALLTYPE CorProfiler::Release(void)
    {
        Log::Debug("CorProfiler::Release");
        int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

        if (count <= 0)
        {
            delete this;
        }

        return count;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::Initialize(IUnknown* pICorProfilerInfoUnk)
    {
        Log::Debug("CorProfiler::Initialize");
        InspectRuntimeCompatibility(pICorProfilerInfoUnk);
        // std::this_thread::sleep_for(std::chrono::milliseconds(20000));

        //
        // Get and set profiler pointers
        //
        if (m_dispatcher == nullptr)
        {
            return E_FAIL;
        }
        IDynamicInstance* cpInstance = m_dispatcher->GetContinuousProfilerInstance();
        if (cpInstance != nullptr)
        {
            m_cpProfiler = cpInstance->GetProfilerCallback();
        }
        IDynamicInstance* tracerInstance = m_dispatcher->GetTracerInstance();
        if (tracerInstance != nullptr)
        {
            m_tracerProfiler = tracerInstance->GetProfilerCallback();
        }
        IDynamicInstance* customInstance = m_dispatcher->GetCustomInstance();
        if (customInstance != nullptr)
        {
            m_customProfiler = customInstance->GetProfilerCallback();
        }

        // *******************************************************************************************************
        // We get the ICorProfilerInfo4 and ICorProfilerInfo5 interface from the pICorProfilerInfoUnk
        // given by the runtime.
        // Note: pICorProfilerInfoUnk is shared with CP and Tracer profilers, so their ICorProfilerInfoX
        // will be extracted the same way we do here. And in fact, the mask is global and shared with
        // all profilers.
        //
        // So for event masks we do the following:
        //
        // 1. Read the Profiler mask using the ICorProfilerInfo4 or ICorProfilerInfo5 instance.
        // 2. Call the `Initialize` function from the ContinousProfiler with the same pICorProfilerInfoUnk
        // instance. In this step the Continous Profiler will set the required event mask to work.
        // 3. Read again the event mask from the ICorProfilerInfo4 or ICorProfilerInfo5 instance, and
        // because it's using the same pICorProfilerInfoUnk we will see here the event masks that the
        // continuous profiler set.
        // 4. We do the bitwise OR operation with the global `mask_low` and `mask_hi`.
        // 5. Repeat the steps 2,3,4 for other target profilers.
        // 6. Use the ICorProfilerInfo4 or ICorProfilerInfo5 instance to set the final `mask_low` and `mask_hi`.
        // *******************************************************************************************************

        //
        // Get Profiler interface ICorProfilerInfo4 and ICorProfilerInfo5
        //
        ICorProfilerInfo4* info4 = nullptr;
        HRESULT hr = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**) &info4);
        if (FAILED(hr))
        {
            Log::Warn("CorProfiler::Initialize: Failed to attach profiler, interface ICorProfilerInfo4 not found.");
            return E_FAIL;
        }
        InspectRuntimeVersion(info4);

        ICorProfilerInfo5* info5 = nullptr;
        hr = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo5), (void**) &info5);
        if (FAILED(hr))
        {
            Log::Info("CorProfiler::Initialize: ICorProfilerInfo5 interface not found.");
            info5 = nullptr;
        }

        // Gets the initial value for the event mask
        DWORD mask_low = 0;
        DWORD mask_hi = 0;
        if (info5 != nullptr)
        {
            hr = info5->GetEventMask2(&mask_low, &mask_hi);
        }
        else
        {
            hr = info4->GetEventMask(&mask_low);
        }
        if (FAILED(hr))
        {
            Log::Warn("CorProfiler::Initialize: Error getting the event mask.");
            return E_FAIL;
        }

        Log::Debug("CorProfiler::Initialize: MaskLow: ", mask_low);
        Log::Debug("CorProfiler::Initialize: MaskHi : ", mask_hi);

        if (instrumented_assembly_generator::IsInstrumentedAssemblyGeneratorEnabled())
        {
            m_writeToDiskCorProfilerInfo = std::make_shared<instrumented_assembly_generator::CorProfilerInfo>(
                pICorProfilerInfoUnk);

        }
        else
        {
            m_writeToDiskCorProfilerInfo = nullptr;
        }

        //
        // Continuous Profiler Initialization
        //
        if (m_cpProfiler != nullptr)
        {
            const HRESULT localResult = m_writeToDiskCorProfilerInfo != nullptr
                                            ? m_cpProfiler->Initialize(m_writeToDiskCorProfilerInfo.get())
                                            : m_cpProfiler->Initialize(pICorProfilerInfoUnk);

            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
                DWORD local_mask_low = 0;
                DWORD local_mask_hi = 0;
                HRESULT hr;
                if (info5 != nullptr)
                {
                    hr = info5->GetEventMask2(&local_mask_low, &local_mask_hi);
                }
                else
                {
                    hr = info4->GetEventMask(&local_mask_low);
                }
                if (SUCCEEDED(hr))
                {
                    mask_low = mask_low | local_mask_low;
                    mask_hi = mask_hi | local_mask_hi;

                    Log::Debug("CorProfiler::Initialize: *LocalMaskLow: ", local_mask_low);
                    Log::Debug("CorProfiler::Initialize: *LocalMaskHi : ", local_mask_hi);
                    Log::Info("CorProfiler::Initialize: Continuous Profiler initialized successfully.");
                }
                else
                {
                    Log::Warn("CorProfiler::Initialize: Error getting the event mask.");
                }
            }
            else
            {
                Log::Warn("CorProfiler::Initialize: Error Initializing the Continuous Profiler, unloading the dynamic library.");
                m_cpProfiler = nullptr;
            }
        }

        //
        // Tracer Initialization
        //
        if (m_tracerProfiler != nullptr)
        {
            const HRESULT localResult = m_writeToDiskCorProfilerInfo != nullptr
                    ? m_tracerProfiler->Initialize(m_writeToDiskCorProfilerInfo.get())
                                            : m_tracerProfiler->Initialize(pICorProfilerInfoUnk);
            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
                DWORD local_mask_low = 0;
                DWORD local_mask_hi = 0;
                HRESULT hr;
                if (info5 != nullptr)
                {
                    hr = info5->GetEventMask2(&local_mask_low, &local_mask_hi);
                }
                else
                {
                    hr = info4->GetEventMask(&local_mask_low);
                }
                if (SUCCEEDED(hr))
                {
                    mask_low = mask_low | local_mask_low;
                    mask_hi = mask_hi | local_mask_hi;

                    Log::Debug("CorProfiler::Initialize: *LocalMaskLow: ", local_mask_low);
                    Log::Debug("CorProfiler::Initialize: *LocalMaskHi : ", local_mask_hi);
                    Log::Info("CorProfiler::Initialize: Tracer Profiler initialized successfully.");
                }
                else
                {
                    Log::Warn("CorProfiler::Initialize: Error getting the event mask.");
                }
            }
            else
            {
                Log::Warn("CorProfiler::Initialize: Error Initializing the Tracer Profiler, unloading the dynamic library.");
                m_tracerProfiler = nullptr;
            }
        }

        //
        // Custom Profiler Initialization
        //
        if (m_customProfiler != nullptr)
        {
            const HRESULT localResult = m_writeToDiskCorProfilerInfo != nullptr
                    ? m_customProfiler->Initialize(m_writeToDiskCorProfilerInfo.get())
                                            : m_customProfiler->Initialize(pICorProfilerInfoUnk);
            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
                DWORD local_mask_low = 0;
                DWORD local_mask_hi = 0;
                HRESULT hr;
                if (info5 != nullptr)
                {
                    hr = info5->GetEventMask2(&local_mask_low, &local_mask_hi);
                }
                else
                {
                    hr = info4->GetEventMask(&local_mask_low);
                }
                if (SUCCEEDED(hr))
                {
                    mask_low = mask_low | local_mask_low;
                    mask_hi = mask_hi | local_mask_hi;

                    Log::Debug("CorProfiler::Initialize: *LocalMaskLow: ", local_mask_low);
                    Log::Debug("CorProfiler::Initialize: *LocalMaskHi : ", local_mask_hi);
                    Log::Info("CorProfiler::Initialize: Custom Profiler initialized successfully.");
                }
                else
                {
                    Log::Warn("CorProfiler::Initialize: Error getting the event mask.");
                }
            }
            else
            {
                Log::Warn("CorProfiler::Initialize: Error Initializing the Custom Profiler, unloading the dynamic library.");
                m_customProfiler = nullptr;
            }
        }

        //
        // Sets final event mask as a combination of each cor profiler masks.
        //
        Log::Debug("CorProfiler::Initialize: *MaskLow: ", mask_low);
        Log::Debug("CorProfiler::Initialize: *MaskHi : ", mask_hi);

        // Sets the final event mask for the profiler
        if (info5 != nullptr)
        {
            hr = info5->SetEventMask2(mask_low, mask_hi);
        }
        else
        {
            hr = info4->SetEventMask(mask_low);
        }
        if (FAILED(hr))
        {
            Log::Warn("CorProfiler::Initialize: Error setting the event mask.");
            return E_FAIL;
        }

        m_info = info5 != nullptr ? info5 : info4;

        m_this = this;

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
    {
        RunInAllProfilers(Shutdown());
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationStarted(AppDomainID appDomainId)
    {
        RunInAllProfilers(AppDomainCreationStarted(appDomainId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        RunInAllProfilers(AppDomainCreationFinished(appDomainId, hrStatus));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownStarted(AppDomainID appDomainId)
    {
        RunInAllProfilers(AppDomainShutdownStarted(appDomainId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        RunInAllProfilers(AppDomainShutdownFinished(appDomainId, hrStatus));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadStarted(AssemblyID assemblyId)
    {
        RunInAllProfilers(AssemblyLoadStarted(assemblyId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        RunInAllProfilers(AssemblyLoadFinished(assemblyId, hrStatus));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadStarted(AssemblyID assemblyId)
    {
        RunInAllProfilers(AssemblyUnloadStarted(assemblyId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        RunInAllProfilers(AssemblyUnloadFinished(assemblyId, hrStatus));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadStarted(ModuleID moduleId)
    {
        RunInAllProfilers(ModuleLoadStarted(moduleId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        try
        {
            if (m_writeToDiskCorProfilerInfo != nullptr)
            {
                WCHAR modulePath[MAX_PATH];
                DWORD nameSize = 0;
                LPCBYTE baseLoadAddress;
                AssemblyID assemblyId = 0;
                DWORD moduleFlags = 0;
                const auto hr = m_writeToDiskCorProfilerInfo->GetModuleInfo2(moduleId, &baseLoadAddress, MAX_PATH, &nameSize,
                                                                  modulePath, &assemblyId, &moduleFlags);

                if (FAILED(hr))
                {
                    Log::Warn("InstrumentationVerification: fail on call GetModuleInfo2 for moduleId {}", moduleId);
                }
                else
                {
                    WSTRING moduleName;
                    if (nameSize == 0)
                    {
                        moduleName = WStr("UnknownModule");
                    }
                    else
                    {
                        moduleName = modulePath;
                    }

                    WSTRINGSTREAM stringStream;
                    stringStream << moduleName << std::endl;
                    instrumented_assembly_generator::WriteTextToFile(instrumented_assembly_generator::ModulesFileName, stringStream.str());
                    instrumented_assembly_generator::CopyOriginalModuleForInstrumentationVerification(modulePath);
                }
            }
        }
        catch (...)
        {
            Log::Warn("InstrumentationVerification: fail to write module load to disk on ModuleLoadFinished");
        }

        RunInAllProfilers(ModuleLoadFinished(moduleId, hrStatus));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID moduleId)
    {
        RunInAllProfilers(ModuleUnloadStarted(moduleId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        RunInAllProfilers(ModuleUnloadFinished(moduleId, hrStatus));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId)
    {
        RunInAllProfilers(ModuleAttachedToAssembly(moduleId, AssemblyId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadStarted(ClassID classId)
    {
        RunInAllProfilers(ClassLoadStarted(classId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadFinished(ClassID classId, HRESULT hrStatus)
    {
        RunInAllProfilers(ClassLoadFinished(classId, hrStatus));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadStarted(ClassID classId)
    {
        RunInAllProfilers(ClassUnloadStarted(classId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadFinished(ClassID classId, HRESULT hrStatus)
    {
        RunInAllProfilers(ClassUnloadFinished(classId, hrStatus));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::FunctionUnloadStarted(FunctionID functionId)
    {
        RunInAllProfilers(FunctionUnloadStarted(functionId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
    {
        RunInAllProfilers(JITCompilationStarted(functionId, fIsSafeToBlock));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                  BOOL fIsSafeToBlock)
    {
        RunInAllProfilers(JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId,
                                                                          BOOL* pbUseCachedFunction)
    {
        RunInAllProfilers(JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchFinished(FunctionID functionId,
                                                                           COR_PRF_JIT_CACHE result)
    {
        RunInAllProfilers(JITCachedFunctionSearchFinished(functionId, result));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITFunctionPitched(FunctionID functionId)
    {
        RunInAllProfilers(JITFunctionPitched(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
    {
        RunInAllProfilers(JITInlining(callerId, calleeId, pfShouldInline));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadCreated(ThreadID threadId)
    {
        RunInAllProfilers(ThreadCreated(threadId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadDestroyed(ThreadID threadId)
    {
        RunInAllProfilers(ThreadDestroyed(threadId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId)
    {
        RunInAllProfilers(ThreadAssignedToOSThread(managedThreadId, osThreadId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationStarted()
    {
        RunInAllProfilers(RemotingClientInvocationStarted());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        RunInAllProfilers(RemotingClientSendingMessage(pCookie, fIsAsync));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync)
    {
        RunInAllProfilers(RemotingClientReceivingReply(pCookie, fIsAsync));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationFinished()
    {
        RunInAllProfilers(RemotingClientInvocationFinished());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        RunInAllProfilers(RemotingServerReceivingMessage(pCookie, fIsAsync));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationStarted()
    {
        RunInAllProfilers(RemotingServerInvocationStarted());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationReturned()
    {
        RunInAllProfilers(RemotingServerInvocationReturned());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync)
    {
        RunInAllProfilers(RemotingServerSendingReply(pCookie, fIsAsync));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::UnmanagedToManagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        RunInAllProfilers(UnmanagedToManagedTransition(functionId, reason));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ManagedToUnmanagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        RunInAllProfilers(ManagedToUnmanagedTransition(functionId, reason));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
    {
        RunInAllProfilers(RuntimeSuspendStarted(suspendReason));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendFinished()
    {
        RunInAllProfilers(RuntimeSuspendFinished());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendAborted()
    {
        RunInAllProfilers(RuntimeSuspendAborted());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeStarted()
    {
        RunInAllProfilers(RuntimeResumeStarted());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeFinished()
    {
        RunInAllProfilers(RuntimeResumeFinished());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadSuspended(ThreadID threadId)
    {
        RunInAllProfilers(RuntimeThreadSuspended(threadId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadResumed(ThreadID threadId)
    {
        RunInAllProfilers(RuntimeThreadResumed(threadId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                                           ObjectID newObjectIDRangeStart[],
                                                           ULONG cObjectIDRangeLength[])
    {
        RunInAllProfilers(
            MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart, cObjectIDRangeLength));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectAllocated(ObjectID objectId, ClassID classId)
    {
        RunInAllProfilers(ObjectAllocated(objectId, classId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[],
                                                                   ULONG cObjects[])
    {
        RunInAllProfilers(ObjectsAllocatedByClass(cClassCount, classIds, cObjects));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs,
                                                            ObjectID objectRefIds[])
    {
        RunInAllProfilers(ObjectReferences(objectId, classId, cObjectRefs, objectRefIds));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[])
    {
        RunInAllProfilers(RootReferences(cRootRefs, rootRefIds));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionThrown(ObjectID thrownObjectId)
    {
        RunInAllProfilers(ExceptionThrown(thrownObjectId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionEnter(FunctionID functionId)
    {
        RunInAllProfilers(ExceptionSearchFunctionEnter(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionLeave()
    {
        RunInAllProfilers(ExceptionSearchFunctionLeave());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterEnter(FunctionID functionId)
    {
        RunInAllProfilers(ExceptionSearchFilterEnter(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterLeave()
    {
        RunInAllProfilers(ExceptionSearchFilterLeave());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchCatcherFound(FunctionID functionId)
    {
        RunInAllProfilers(ExceptionSearchCatcherFound(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerEnter(UINT_PTR unused_variable)
    {
        RunInAllProfilers(ExceptionOSHandlerEnter(unused_variable));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerLeave(UINT_PTR unused_variable)
    {
        RunInAllProfilers(ExceptionOSHandlerLeave(unused_variable));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionEnter(FunctionID functionId)
    {
        RunInAllProfilers(ExceptionUnwindFunctionEnter(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionLeave()
    {
        RunInAllProfilers(ExceptionUnwindFunctionLeave());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyEnter(FunctionID functionId)
    {
        RunInAllProfilers(ExceptionUnwindFinallyEnter(functionId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyLeave()
    {
        RunInAllProfilers(ExceptionUnwindFinallyLeave());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
    {
        RunInAllProfilers(ExceptionCatcherEnter(functionId, objectId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherLeave()
    {
        RunInAllProfilers(ExceptionCatcherLeave());
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID,
                                                                   void* pVTable, ULONG cSlots)
    {
        RunInAllProfilers(COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID,
                                                                     void* pVTable)
    {
        RunInAllProfilers(COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherFound()
    {
        RunInAllProfilers(ExceptionCLRCatcherFound());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherExecute()
    {
        RunInAllProfilers(ExceptionCLRCatcherExecute());
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[])
    {
        RunInAllProfilers(ThreadNameChanged(threadId, cchName, name));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[],
                                                                    COR_PRF_GC_REASON reason)
    {
        RunInAllProfilers(GarbageCollectionStarted(cGenerations, generationCollected, reason));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges,
                                                               ObjectID objectIDRangeStart[],
                                                               ULONG cObjectIDRangeLength[])
    {
        RunInAllProfilers(SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionFinished()
    {
        RunInAllProfilers(GarbageCollectionFinished());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
    {
        RunInAllProfilers(FinalizeableObjectQueued(finalizerFlags, objectID));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[],
                                                           COR_PRF_GC_ROOT_KIND rootKinds[],
                                                           COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
    {
        RunInAllProfilers(RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::HandleCreated(GCHandleID handleId, ObjectID initialObjectId)
    {
        RunInAllProfilers(HandleCreated(handleId, initialObjectId));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleDestroyed(GCHandleID handleId)
    {
        RunInAllProfilers(HandleDestroyed(handleId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData,
                                                               UINT cbClientData)
    {
        RunInAllProfilers(InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerAttachComplete()
    {
        RunInAllProfilers(ProfilerAttachComplete());
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded()
    {
        RunInAllProfilers(ProfilerDetachSucceeded());
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                                   BOOL fIsSafeToBlock)
    {
        RunInAllProfilers(ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                              ICorProfilerFunctionControl* pFunctionControl)
    {
        if (m_writeToDiskCorProfilerInfo != nullptr)
        {
            auto instrumentationVerificationFunctionControl =
                std::make_unique<instrumented_assembly_generator::CorProfilerFunctionControl>(
                    pFunctionControl, m_writeToDiskCorProfilerInfo, moduleId, methodId);

            // instrumentationVerificationFunctionControl saved in the target profiler only for GetReJITParameters lifetime,
            // so we can safely make it std::unique_ptr and pass the raw pointer
            RunInAllProfilers(GetReJITParameters(moduleId, methodId, instrumentationVerificationFunctionControl.get()));
        }
        else
        {
            RunInAllProfilers(GetReJITParameters(moduleId, methodId, pFunctionControl));
        }
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId,
                                                                    HRESULT hrStatus, BOOL fIsSafeToBlock)
    {
        RunInAllProfilers(ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                      HRESULT hrStatus)
    {
        RunInAllProfilers(ReJITError(moduleId, methodId, functionId, hrStatus));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences2(ULONG cMovedObjectIDRanges,
                                                            ObjectID oldObjectIDRangeStart[],
                                                            ObjectID newObjectIDRangeStart[],
                                                            SIZE_T cObjectIDRangeLength[])
    {
        RunInAllProfilers(
            MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart, newObjectIDRangeStart, cObjectIDRangeLength));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences2(ULONG cSurvivingObjectIDRanges,
                                                                ObjectID objectIDRangeStart[],
                                                                SIZE_T cObjectIDRangeLength[])
    {
        RunInAllProfilers(SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[],
                                                                                 ObjectID valueRefIds[],
                                                                                 GCHandleID rootIds[])
    {
        RunInAllProfilers(ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                                 ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
    {
        RunInAllProfilers(GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleInMemorySymbolsUpdated(ModuleID moduleId)
    {
        RunInAllProfilers(ModuleInMemorySymbolsUpdated(moduleId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationStarted(FunctionID functionId,
                                                                              BOOL fIsSafeToBlock, LPCBYTE ilHeader,
                                                                              ULONG cbILHeader)
    {
        RunInAllProfilers(DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                               BOOL fIsSafeToBlock)
    {
        RunInAllProfilers(DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodUnloaded(FunctionID functionId)
    {
        RunInAllProfilers(DynamicMethodUnloaded(functionId));
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeEventDelivered(EVENTPIPE_PROVIDER provider, DWORD eventId,
                                                                   DWORD eventVersion, ULONG cbMetadataBlob,
                                                                   LPCBYTE metadataBlob, ULONG cbEventData,
                                                                   LPCBYTE eventData, LPCGUID pActivityId,
                                                                   LPCGUID pRelatedActivityId, ThreadID eventThread,
                                                                   ULONG numStackFrames, UINT_PTR stackFrames[])
    {
        RunInAllProfilers(EventPipeEventDelivered(provider, eventId, eventVersion, cbMetadataBlob, metadataBlob,
                                                  cbEventData, eventData, pActivityId, pRelatedActivityId, eventThread,
                                                  numStackFrames, stackFrames));
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
    {
        RunInAllProfilers(EventPipeProviderCreated(provider));
    }

    void CorProfiler::InspectRuntimeCompatibility(IUnknown* corProfilerInfoUnk)
    {
        if (corProfilerInfoUnk == nullptr)
        {
            Log::Info(
                "No ICorProfilerInfoXxx available. Null pointer was passed to CorProfilerCallback for initialization."
                " No compatible Profiling API is available.");
            return;
        }

        IUnknown* tstVerProfilerInfo;
        if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo11), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo11 available. Profiling API compatibility: .NET Core 5.0 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo10), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo10 available. Profiling API compatibility: .NET Core 3.0 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo9), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo9 available. Profiling API compatibility: .NET Core 2.2 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo8), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo8 available. Profiling API compatibility: .NET Fx 4.7.2 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo7), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo7 available. Profiling API compatibility: .NET Fx 4.6.1 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo6), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo6 available. Profiling API compatibility: .NET Fx 4.6 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo5), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo5 available. Profiling API compatibility: .NET Fx 4.5.2 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo4 available. Profiling API compatibility: .NET Fx 4.5 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo3), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo3 available. Profiling API compatibility: .NET Fx 4.0 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo2), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo2 available. Profiling API compatibility: .NET Fx 2.0 or later.");
            tstVerProfilerInfo->Release();
        }
        else if (S_OK == corProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo), (void**) &tstVerProfilerInfo))
        {
            Log::Info("ICorProfilerInfo available. Profiling API compatibility: .NET Fx 2 or later.");
            tstVerProfilerInfo->Release();
        }
        else
        {
            Log::Info("No ICorProfilerInfoXxx available. A valid IUnknown pointer was passed to CorProfilerCallback"
                 " for initialization, but QueryInterface(..) did not succeed for any of the known "
                 "ICorProfilerInfoXxx ifaces."
                 " No compatible Profiling API is available.");
        }
    }

    void CorProfiler::InspectRuntimeVersion(ICorProfilerInfo4* pCorProfilerInfo)
    {
        USHORT clrInstanceId;
        COR_PRF_RUNTIME_TYPE runtimeType;
        USHORT majorVersion;
        USHORT minorVersion;
        USHORT buildNumber;
        USHORT qfeVersion;

        HRESULT hrGRI = pCorProfilerInfo->GetRuntimeInformation(
            &clrInstanceId, &runtimeType, &majorVersion, &minorVersion, &buildNumber, &qfeVersion, 0, nullptr, nullptr);

        if (FAILED(hrGRI))
        {
            std::ostringstream hex;
            hex << std::hex << hrGRI;
            Log::Info("Initializing the Profiler: Exact runtime version could not be obtained (0x", hex.str(), ")");
        }
        else
        {
            Log::Info("Initializing the Profiler: Reported runtime version : { clrInstanceId: ", clrInstanceId,
                 ", runtimeType:",
                 ((runtimeType == COR_PRF_DESKTOP_CLR) ? "DESKTOP_CLR"
                  : (runtimeType == COR_PRF_CORE_CLR)
                      ? "CORE_CLR"
                      : (std::string("unknown(") + std::to_string(runtimeType) + std::string(")"))),
                 ",", " majorVersion: ", majorVersion, ", minorVersion: ", minorVersion,
                 ", buildNumber: ", buildNumber, ", qfeVersion: ", qfeVersion, " }.");
        }
    }

    AppDomainID CorProfiler::GetCurrentAppDomainId()
    {
        if (m_this == nullptr)
        {
            Log::Warn("The native loader library is not properly initialized. We cannot get the current AppDomain Id.");
            return (AppDomainID)0;
        }

        ThreadID threadId;
        auto hr = m_this->m_info->GetCurrentThreadID(&threadId);

        if (FAILED(hr))
        {
            Log::Warn("Cannot return current ApppDomain because we are unable to get the current thread id. The current thread might not be managed (HRESULT: 0x",
                std::hex, hr, ")");
            return (AppDomainID)0;
        }

        AppDomainID appDomain;
        hr = m_this->m_info->GetThreadAppDomain(threadId, &appDomain);

        if (FAILED(hr))
        {
            Log::Warn("Cannot return current ApppDomain because we are unable to get the app domain id for thread 0x", std::hex, threadId,
                " (HRESULT: 0x", std::hex, hr, ")");
            return (AppDomainID)0;
        }

        return appDomain;
    }

    const char* CorProfiler::GetRuntimeId(AppDomainID appDomain)
    {
        if (m_this == nullptr)
        {
            Log::Warn("The native loader library is not properly initialized. We cannot get the runtime id for the AppDomain ID #", appDomain);
            return nullptr;
        }

        return m_this->m_runtimeIdStore.Get(appDomain).c_str();
    }

} // namespace datadog::shared::nativeloader