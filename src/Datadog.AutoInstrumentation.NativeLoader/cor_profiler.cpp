#include "cor_profiler.h"

#include "logging.h"
#include "dynamic_dispatcher.h"

namespace datadog::shared::nativeloader
{

    CorProfiler::CorProfiler(DynamicDispatcher* dispatcher) :
        m_refCount(0), m_dispatcher(dispatcher), m_cpProfiler(nullptr), m_tracerProfiler(nullptr), m_customProfiler(nullptr)
    {
        Debug("CorProfiler::.ctor");
    }

    CorProfiler::~CorProfiler()
    {
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::QueryInterface(REFIID riid, void** ppvObject)
    {
        Debug("CorProfiler::QueryInterface");
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
        //
        // Get and set profiler pointers
        //
        if (m_dispatcher == nullptr)
        {
            return E_FAIL;
        }
        DynamicInstance* cpInstance = m_dispatcher->GetContinuousProfilerInstance();
        if (cpInstance != nullptr)
        {
            m_cpProfiler = cpInstance->GetProfilerCallback();
        }
        DynamicInstance* tracerInstance = m_dispatcher->GetTracerInstance();
        if (tracerInstance != nullptr)
        {
            m_tracerProfiler = tracerInstance->GetProfilerCallback();
        }
        DynamicInstance* customInstance = m_dispatcher->GetCustomInstance();
        if (customInstance != nullptr)
        {
            m_customProfiler = customInstance->GetProfilerCallback();
        }

        //
        // Get Profiler interface ICorProfilerInfo6 for net46+
        //
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

        //
        // Continuous Profiler Initialization
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT localResult = m_cpProfiler->Initialize(pICorProfilerInfoUnk);
            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
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
            else
            {
                Warn("Error Initializing the Continuous Profiler.");
                return localResult;
            }
        }

        //
        // Tracer Initialization
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT localResult = m_tracerProfiler->Initialize(pICorProfilerInfoUnk);
            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
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
            else
            {
                Warn("Error Initializing the Tracer Profiler.");
                return localResult;
            }
        }

        //
        // Custom Profiler Initialization
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT localResult = m_customProfiler->Initialize(pICorProfilerInfoUnk);
            if (SUCCEEDED(localResult))
            {
                // let's get the event mask set by the CP.
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
            else
            {
                Warn("Error Initializing the Custom Profiler.");
                return localResult;
            }
        }

        //
        // Sets final event mask as a combination of each cor profiler masks.
        //
        Debug("*MaskLow: ", mask_low);
        Debug("*MaskHi : ", mask_hi);

        // Sets the final event mask for the profiler
        hr = info6->SetEventMask2(mask_low, mask_hi);
        if (FAILED(hr))
        {
            Warn("Error setting the event mask.");
            return E_FAIL;
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->Shutdown();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in Shutdown() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->Shutdown();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in Shutdown() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->Shutdown();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in Shutdown() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationStarted(AppDomainID appDomainId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AppDomainCreationStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AppDomainCreationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AppDomainCreationStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AppDomainCreationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AppDomainCreationStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AppDomainCreationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainCreationFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AppDomainCreationFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AppDomainCreationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AppDomainCreationFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AppDomainCreationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AppDomainCreationFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AppDomainCreationFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownStarted(AppDomainID appDomainId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AppDomainShutdownStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AppDomainShutdownStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AppDomainShutdownStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AppDomainShutdownStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AppDomainShutdownStarted(appDomainId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AppDomainShutdownStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AppDomainShutdownFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AppDomainShutdownFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AppDomainShutdownFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AppDomainShutdownFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AppDomainShutdownFinished(appDomainId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AppDomainShutdownFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadStarted(AssemblyID assemblyId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AssemblyLoadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AssemblyLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AssemblyLoadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AssemblyLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AssemblyLoadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AssemblyLoadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AssemblyLoadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AssemblyLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AssemblyLoadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AssemblyLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AssemblyLoadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AssemblyLoadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadStarted(AssemblyID assemblyId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AssemblyUnloadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AssemblyUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AssemblyUnloadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AssemblyUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AssemblyUnloadStarted(assemblyId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AssemblyUnloadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyUnloadFinished(AssemblyID assemblyId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->AssemblyUnloadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in AssemblyUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->AssemblyUnloadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in AssemblyUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->AssemblyUnloadFinished(assemblyId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in AssemblyUnloadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadStarted(ModuleID moduleId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleLoadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleLoadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleLoadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleLoadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleLoadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleLoadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleLoadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleLoadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID moduleId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleUnloadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleUnloadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleUnloadStarted(moduleId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleUnloadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleUnloadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleUnloadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleUnloadFinished(moduleId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleUnloadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleAttachedToAssembly(ModuleID moduleId, AssemblyID AssemblyId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleAttachedToAssembly(moduleId, AssemblyId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleAttachedToAssembly() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleAttachedToAssembly(moduleId, AssemblyId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleAttachedToAssembly() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleAttachedToAssembly(moduleId, AssemblyId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleAttachedToAssembly() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadStarted(ClassID classId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ClassLoadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ClassLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ClassLoadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ClassLoadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ClassLoadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ClassLoadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassLoadFinished(ClassID classId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ClassLoadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ClassLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ClassLoadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ClassLoadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ClassLoadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ClassLoadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadStarted(ClassID classId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ClassUnloadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ClassUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ClassUnloadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ClassUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ClassUnloadStarted(classId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ClassUnloadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ClassUnloadFinished(ClassID classId, HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ClassUnloadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ClassUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ClassUnloadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ClassUnloadFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ClassUnloadFinished(classId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ClassUnloadFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::FunctionUnloadStarted(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->FunctionUnloadStarted(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in FunctionUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->FunctionUnloadStarted(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in FunctionUnloadStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->FunctionUnloadStarted(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in FunctionUnloadStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITCompilationStarted(functionId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITCompilationStarted(functionId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITCompilationStarted(functionId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITCompilationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                  BOOL fIsSafeToBlock)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITCompilationFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchStarted(FunctionID functionId,
                                                                          BOOL* pbUseCachedFunction)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITCachedFunctionSearchStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITCachedFunctionSearchStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITCachedFunctionSearchStarted(functionId, pbUseCachedFunction);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITCachedFunctionSearchStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITCachedFunctionSearchFinished(FunctionID functionId,
                                                                           COR_PRF_JIT_CACHE result)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITCachedFunctionSearchFinished(functionId, result);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITCachedFunctionSearchFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITCachedFunctionSearchFinished(functionId, result);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITCachedFunctionSearchFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITCachedFunctionSearchFinished(functionId, result);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITCachedFunctionSearchFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITFunctionPitched(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITFunctionPitched(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITFunctionPitched() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITFunctionPitched(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITFunctionPitched() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITFunctionPitched(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITFunctionPitched() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->JITInlining(callerId, calleeId, pfShouldInline);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in JITInlining() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->JITInlining(callerId, calleeId, pfShouldInline);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in JITInlining() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->JITInlining(callerId, calleeId, pfShouldInline);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in JITInlining() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadCreated(ThreadID threadId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ThreadCreated(threadId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ThreadCreated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ThreadCreated(threadId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ThreadCreated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ThreadCreated(threadId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ThreadCreated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadDestroyed(ThreadID threadId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ThreadDestroyed(threadId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ThreadDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ThreadDestroyed(threadId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ThreadDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ThreadDestroyed(threadId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ThreadDestroyed() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadAssignedToOSThread(ThreadID managedThreadId, DWORD osThreadId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ThreadAssignedToOSThread(managedThreadId, osThreadId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ThreadAssignedToOSThread() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ThreadAssignedToOSThread(managedThreadId, osThreadId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ThreadAssignedToOSThread() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ThreadAssignedToOSThread(managedThreadId, osThreadId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ThreadAssignedToOSThread() call.");
                gHR = hr;
            }
        }

        return gHR;
    }






    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationStarted()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingClientInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingClientInvocationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingClientInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingClientInvocationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingClientInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingClientInvocationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientSendingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingClientSendingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingClientSendingMessage() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingClientSendingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingClientSendingMessage() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingClientSendingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingClientSendingMessage() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientReceivingReply(GUID* pCookie, BOOL fIsAsync)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingClientReceivingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingClientReceivingReply() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingClientReceivingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingClientReceivingReply() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingClientReceivingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingClientReceivingReply() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingClientInvocationFinished()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingClientInvocationFinished();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingClientInvocationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingClientInvocationFinished();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingClientInvocationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingClientInvocationFinished();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingClientInvocationFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerReceivingMessage(GUID* pCookie, BOOL fIsAsync)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingServerReceivingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingServerReceivingMessage() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingServerReceivingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingServerReceivingMessage() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingServerReceivingMessage(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingServerReceivingMessage() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationStarted()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingServerInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingServerInvocationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingServerInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingServerInvocationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingServerInvocationStarted();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingServerInvocationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerInvocationReturned()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingServerInvocationReturned();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingServerInvocationReturned() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingServerInvocationReturned();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingServerInvocationReturned() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingServerInvocationReturned();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingServerInvocationReturned() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RemotingServerSendingReply(GUID* pCookie, BOOL fIsAsync)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RemotingServerSendingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RemotingServerSendingReply() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RemotingServerSendingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RemotingServerSendingReply() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RemotingServerSendingReply(pCookie, fIsAsync);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RemotingServerSendingReply() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::UnmanagedToManagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->UnmanagedToManagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in UnmanagedToManagedTransition() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->UnmanagedToManagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in UnmanagedToManagedTransition() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->UnmanagedToManagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in UnmanagedToManagedTransition() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ManagedToUnmanagedTransition(FunctionID functionId,
                                                                        COR_PRF_TRANSITION_REASON reason)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ManagedToUnmanagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ManagedToUnmanagedTransition() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ManagedToUnmanagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ManagedToUnmanagedTransition() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ManagedToUnmanagedTransition(functionId, reason);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ManagedToUnmanagedTransition() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeSuspendStarted(suspendReason);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeSuspendStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeSuspendStarted(suspendReason);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeSuspendStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeSuspendStarted(suspendReason);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeSuspendStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendFinished()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeSuspendFinished();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeSuspendFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeSuspendFinished();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeSuspendFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeSuspendFinished();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeSuspendFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeSuspendAborted()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeSuspendAborted();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeSuspendAborted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeSuspendAborted();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeSuspendAborted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeSuspendAborted();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeSuspendAborted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeStarted()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeResumeStarted();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeResumeStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeResumeStarted();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeResumeStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeResumeStarted();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeResumeStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeResumeFinished()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeResumeFinished();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeResumeFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeResumeFinished();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeResumeFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeResumeFinished();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeResumeFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadSuspended(ThreadID threadId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeThreadSuspended(threadId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeThreadSuspended() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeThreadSuspended(threadId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeThreadSuspended() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeThreadSuspended(threadId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeThreadSuspended() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RuntimeThreadResumed(ThreadID threadId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RuntimeThreadResumed(threadId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RuntimeThreadResumed() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RuntimeThreadResumed(threadId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RuntimeThreadResumed() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RuntimeThreadResumed(threadId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RuntimeThreadResumed() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences(ULONG cMovedObjectIDRanges, ObjectID oldObjectIDRangeStart[],
                                                           ObjectID newObjectIDRangeStart[],
                                                           ULONG cObjectIDRangeLength[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                       newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in MovedReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                           newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in MovedReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->MovedReferences(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                           newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in MovedReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectAllocated(ObjectID objectId, ClassID classId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ObjectAllocated(objectId, classId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ObjectAllocated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ObjectAllocated(objectId, classId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ObjectAllocated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ObjectAllocated(objectId, classId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ObjectAllocated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectsAllocatedByClass(ULONG cClassCount, ClassID classIds[],
                                                                   ULONG cObjects[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ObjectsAllocatedByClass(cClassCount, classIds, cObjects);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ObjectsAllocatedByClass() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ObjectsAllocatedByClass(cClassCount, classIds, cObjects);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ObjectsAllocatedByClass() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ObjectsAllocatedByClass(cClassCount, classIds, cObjects);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ObjectsAllocatedByClass() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ObjectReferences(ObjectID objectId, ClassID classId, ULONG cObjectRefs,
                                                            ObjectID objectRefIds[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ObjectReferences(objectId, classId, cObjectRefs, objectRefIds);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ObjectReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ObjectReferences(objectId, classId, cObjectRefs, objectRefIds);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ObjectReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ObjectReferences(objectId, classId, cObjectRefs, objectRefIds);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ObjectReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences(ULONG cRootRefs, ObjectID rootRefIds[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RootReferences(cRootRefs, rootRefIds);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RootReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RootReferences(cRootRefs, rootRefIds);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RootReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RootReferences(cRootRefs, rootRefIds);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RootReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionThrown(ObjectID thrownObjectId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionThrown(thrownObjectId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionThrown() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionThrown(thrownObjectId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionThrown() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionThrown(thrownObjectId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionThrown() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionEnter(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionSearchFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionSearchFunctionEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionSearchFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionSearchFunctionEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionSearchFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionSearchFunctionEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFunctionLeave()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionSearchFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionSearchFunctionLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionSearchFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionSearchFunctionLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionSearchFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionSearchFunctionLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterEnter(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionSearchFilterEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionSearchFilterEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionSearchFilterEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionSearchFilterEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionSearchFilterEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionSearchFilterEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchFilterLeave()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionSearchFilterLeave();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionSearchFilterLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionSearchFilterLeave();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionSearchFilterLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionSearchFilterLeave();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionSearchFilterLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionSearchCatcherFound(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionSearchCatcherFound(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionSearchCatcherFound() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionSearchCatcherFound(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionSearchCatcherFound() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionSearchCatcherFound(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionSearchCatcherFound() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerEnter(UINT_PTR __unused)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionOSHandlerEnter(NULL);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionOSHandlerEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionOSHandlerEnter(NULL);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionOSHandlerEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionOSHandlerEnter(NULL);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionOSHandlerEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionOSHandlerLeave(UINT_PTR __unused)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionOSHandlerLeave(NULL);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionOSHandlerLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionOSHandlerLeave(NULL);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionOSHandlerLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionOSHandlerLeave(NULL);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionOSHandlerLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionEnter(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionUnwindFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionUnwindFunctionEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionUnwindFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionUnwindFunctionEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionUnwindFunctionEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionUnwindFunctionEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFunctionLeave()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionUnwindFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionUnwindFunctionLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionUnwindFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionUnwindFunctionLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionUnwindFunctionLeave();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionUnwindFunctionLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyEnter(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionUnwindFinallyEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionUnwindFinallyEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionUnwindFinallyEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionUnwindFinallyEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionUnwindFinallyEnter(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionUnwindFinallyEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionUnwindFinallyLeave()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionUnwindFinallyLeave();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionUnwindFinallyLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionUnwindFinallyLeave();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionUnwindFinallyLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionUnwindFinallyLeave();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionUnwindFinallyLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherEnter(FunctionID functionId, ObjectID objectId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionCatcherEnter(functionId, objectId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionCatcherEnter() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionCatcherEnter(functionId, objectId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionCatcherEnter() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionCatcherEnter(functionId, objectId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionCatcherEnter() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCatcherLeave()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionCatcherLeave();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionCatcherLeave() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionCatcherLeave();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionCatcherLeave() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionCatcherLeave();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionCatcherLeave() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableCreated(ClassID wrappedClassId, REFGUID implementedIID,
                                                                   void* pVTable, ULONG cSlots)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in COMClassicVTableCreated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in COMClassicVTableCreated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->COMClassicVTableCreated(wrappedClassId, implementedIID, pVTable, cSlots);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in COMClassicVTableCreated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::COMClassicVTableDestroyed(ClassID wrappedClassId, REFGUID implementedIID,
                                                                     void* pVTable)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in COMClassicVTableDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in COMClassicVTableDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->COMClassicVTableDestroyed(wrappedClassId, implementedIID, pVTable);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in COMClassicVTableDestroyed() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherFound()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionCLRCatcherFound();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionCLRCatcherFound() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionCLRCatcherFound();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionCLRCatcherFound() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionCLRCatcherFound();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionCLRCatcherFound() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ExceptionCLRCatcherExecute()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ExceptionCLRCatcherExecute();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ExceptionCLRCatcherExecute() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ExceptionCLRCatcherExecute();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ExceptionCLRCatcherExecute() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ExceptionCLRCatcherExecute();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ExceptionCLRCatcherExecute() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ThreadNameChanged(ThreadID threadId, ULONG cchName, WCHAR name[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ThreadNameChanged(threadId, cchName, name);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ThreadNameChanged() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ThreadNameChanged(threadId, cchName, name);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ThreadNameChanged() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ThreadNameChanged(threadId, cchName, name);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ThreadNameChanged() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[],
                                                                    COR_PRF_GC_REASON reason)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->GarbageCollectionStarted(cGenerations, generationCollected, reason);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in GarbageCollectionStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->GarbageCollectionStarted(cGenerations, generationCollected, reason);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in GarbageCollectionStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->GarbageCollectionStarted(cGenerations, generationCollected, reason);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in GarbageCollectionStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges,
                                                               ObjectID objectIDRangeStart[],
                                                               ULONG cObjectIDRangeLength[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr =
                m_cpProfiler->SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in SurvivingReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart,
                                                               cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in SurvivingReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->SurvivingReferences(cSurvivingObjectIDRanges, objectIDRangeStart,
                                                               cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in SurvivingReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GarbageCollectionFinished()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->GarbageCollectionFinished();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in GarbageCollectionFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->GarbageCollectionFinished();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in GarbageCollectionFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->GarbageCollectionFinished();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in GarbageCollectionFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->FinalizeableObjectQueued(finalizerFlags, objectID);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in FinalizeableObjectQueued() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->FinalizeableObjectQueued(finalizerFlags, objectID);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in FinalizeableObjectQueued() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->FinalizeableObjectQueued(finalizerFlags, objectID);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in FinalizeableObjectQueued() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::RootReferences2(ULONG cRootRefs, ObjectID rootRefIds[],
                                                           COR_PRF_GC_ROOT_KIND rootKinds[],
                                                           COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in RootReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in RootReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->RootReferences2(cRootRefs, rootRefIds, rootKinds, rootFlags, rootIds);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in RootReferences2() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::HandleCreated(GCHandleID handleId, ObjectID initialObjectId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->HandleCreated(handleId, initialObjectId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in HandleCreated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->HandleCreated(handleId, initialObjectId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in HandleCreated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->HandleCreated(handleId, initialObjectId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in HandleCreated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::HandleDestroyed(GCHandleID handleId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->HandleDestroyed(handleId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in HandleDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->HandleDestroyed(handleId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in HandleDestroyed() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->HandleDestroyed(handleId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in HandleDestroyed() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::InitializeForAttach(IUnknown* pCorProfilerInfoUnk, void* pvClientData,
                                                               UINT cbClientData)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in InitializeForAttach() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in InitializeForAttach() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->InitializeForAttach(pCorProfilerInfoUnk, pvClientData, cbClientData);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in InitializeForAttach() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerAttachComplete()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ProfilerAttachComplete();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ProfilerAttachComplete() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ProfilerAttachComplete();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ProfilerAttachComplete() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ProfilerAttachComplete();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ProfilerAttachComplete() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded()
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ProfilerDetachSucceeded();
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ProfilerDetachSucceeded() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ProfilerDetachSucceeded();
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ProfilerDetachSucceeded() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ProfilerDetachSucceeded();
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ProfilerDetachSucceeded() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                                   BOOL fIsSafeToBlock)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ReJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ReJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ReJITCompilationStarted(functionId, rejitId, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ReJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                              ICorProfilerFunctionControl* pFunctionControl)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->GetReJITParameters(moduleId, methodId, pFunctionControl);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in GetReJITParameters() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->GetReJITParameters(moduleId, methodId, pFunctionControl);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in GetReJITParameters() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->GetReJITParameters(moduleId, methodId, pFunctionControl);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in GetReJITParameters() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId,
                                                                    HRESULT hrStatus, BOOL fIsSafeToBlock)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ReJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ReJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ReJITCompilationFinished(functionId, rejitId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ReJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                                      HRESULT hrStatus)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ReJITError(moduleId, methodId, functionId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ReJITError() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ReJITError(moduleId, methodId, functionId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ReJITError() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ReJITError(moduleId, methodId, functionId, hrStatus);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ReJITError() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::MovedReferences2(ULONG cMovedObjectIDRanges,
                                                            ObjectID oldObjectIDRangeStart[],
                                                            ObjectID newObjectIDRangeStart[],
                                                            SIZE_T cObjectIDRangeLength[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                        newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in MovedReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                            newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in MovedReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->MovedReferences2(cMovedObjectIDRanges, oldObjectIDRangeStart,
                                                            newObjectIDRangeStart, cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in MovedReferences2() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::SurvivingReferences2(ULONG cSurvivingObjectIDRanges,
                                                                ObjectID objectIDRangeStart[],
                                                                SIZE_T cObjectIDRangeLength[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart,
                                                            cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in SurvivingReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart,
                                                                cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in SurvivingReferences2() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->SurvivingReferences2(cSurvivingObjectIDRanges, objectIDRangeStart,
                                                                cObjectIDRangeLength);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in SurvivingReferences2() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ConditionalWeakTableElementReferences(ULONG cRootRefs, ObjectID keyRefIds[],
                                                                                 ObjectID valueRefIds[],
                                                                                 GCHandleID rootIds[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ConditionalWeakTableElementReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr =
                m_tracerProfiler->ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ConditionalWeakTableElementReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr =
                m_customProfiler->ConditionalWeakTableElementReferences(cRootRefs, keyRefIds, valueRefIds, rootIds);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ConditionalWeakTableElementReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                                 ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in GetAssemblyReferences() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in GetAssemblyReferences() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->GetAssemblyReferences(wszAssemblyPath, pAsmRefProvider);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in GetAssemblyReferences() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::ModuleInMemorySymbolsUpdated(ModuleID moduleId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->ModuleInMemorySymbolsUpdated(moduleId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in ModuleInMemorySymbolsUpdated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->ModuleInMemorySymbolsUpdated(moduleId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in ModuleInMemorySymbolsUpdated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->ModuleInMemorySymbolsUpdated(moduleId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in ModuleInMemorySymbolsUpdated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationStarted(FunctionID functionId,
                                                                              BOOL fIsSafeToBlock, LPCBYTE ilHeader,
                                                                              ULONG cbILHeader)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr =
                m_cpProfiler->DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in DynamicMethodJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr =
                m_tracerProfiler->DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in DynamicMethodJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr =
                m_customProfiler->DynamicMethodJITCompilationStarted(functionId, fIsSafeToBlock, ilHeader, cbILHeader);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in DynamicMethodJITCompilationStarted() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus,
                                                                               BOOL fIsSafeToBlock)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in DynamicMethodJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in DynamicMethodJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->DynamicMethodJITCompilationFinished(functionId, hrStatus, fIsSafeToBlock);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in DynamicMethodJITCompilationFinished() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::DynamicMethodUnloaded(FunctionID functionId)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->DynamicMethodUnloaded(functionId);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in DynamicMethodUnloaded() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->DynamicMethodUnloaded(functionId);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in DynamicMethodUnloaded() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->DynamicMethodUnloaded(functionId);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in DynamicMethodUnloaded() call.");
                gHR = hr;
            }
        }

        return gHR;
    }


    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeEventDelivered(EVENTPIPE_PROVIDER provider, DWORD eventId,
                                                                   DWORD eventVersion, ULONG cbMetadataBlob,
                                                                   LPCBYTE metadataBlob, ULONG cbEventData,
                                                                   LPCBYTE eventData, LPCGUID pActivityId,
                                                                   LPCGUID pRelatedActivityId, ThreadID eventThread,
                                                                   ULONG numStackFrames, UINT_PTR stackFrames[])
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->EventPipeEventDelivered(
                provider, eventId, eventVersion, cbMetadataBlob, metadataBlob, cbEventData, eventData, pActivityId,
                pRelatedActivityId, eventThread, numStackFrames, stackFrames);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in EventPipeEventDelivered() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->EventPipeEventDelivered(
                provider, eventId, eventVersion, cbMetadataBlob, metadataBlob, cbEventData, eventData, pActivityId,
                pRelatedActivityId, eventThread, numStackFrames, stackFrames);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in EventPipeEventDelivered() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->EventPipeEventDelivered(
                provider, eventId, eventVersion, cbMetadataBlob, metadataBlob, cbEventData, eventData, pActivityId,
                pRelatedActivityId, eventThread, numStackFrames, stackFrames);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in EventPipeEventDelivered() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

    HRESULT STDMETHODCALLTYPE CorProfiler::EventPipeProviderCreated(EVENTPIPE_PROVIDER provider)
    {
        HRESULT gHR = S_OK;

        //
        // Continuous Profiler
        //
        if (m_cpProfiler != nullptr)
        {
            HRESULT hr = m_cpProfiler->EventPipeProviderCreated(provider);
            if (FAILED(hr))
            {
                Warn("[Continuous Profiler] Error in EventPipeProviderCreated() call.");
                gHR = hr;
            }
        }

        //
        // Tracer
        //
        if (m_tracerProfiler != nullptr)
        {
            HRESULT hr = m_tracerProfiler->EventPipeProviderCreated(provider);
            if (FAILED(hr))
            {
                Warn("[Tracer] Error in EventPipeProviderCreated() call.");
                gHR = hr;
            }
        }

        //
        // Custom
        //
        if (m_customProfiler != nullptr)
        {
            HRESULT hr = m_customProfiler->EventPipeProviderCreated(provider);
            if (FAILED(hr))
            {
                Warn("[Custom] Error in EventPipeProviderCreated() call.");
                gHR = hr;
            }
        }

        return gHR;
    }

} // namespace datadog::shared::nativeloader