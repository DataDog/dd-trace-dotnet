// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <iostream>
#include <unknwn.h>

#include "CorProfilerCallback.h"
#include "CorProfilerCallbackFactory.h"
#include "EnvironmentVariables.h"
#include "Log.h"

#include "dd_profiler_version.h"


HINSTANCE DllHandle;

const IID IID_IUnknown = {0x00000000,
                          0x0000,
                          0x0000,
                          {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

const IID IID_IClassFactory = {
    0x00000001,
    0x0000,
    0x0000,
    {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

// use STDMETHODCALLTYPE macro to match the CLR declaration.
extern "C" BOOL STDMETHODCALLTYPE DllMain(HINSTANCE hInstDll, DWORD reason, PVOID)
{
    switch (reason)
    {
        case DLL_PROCESS_ATTACH:
            Log::Info("Profiler DLL loaded.");
            Log::Info("Profiler version = ", PROFILER_VERSION);
            Log::Info("Pointer size: ", 8 * sizeof(void*), " bits.");
            break;

        case DLL_PROCESS_DETACH:
            Log::Info("Profiler DLL unloaded.");
            break;
    }

    DllHandle = hInstDll;

    return TRUE;
}

bool IsProfilingEnabled()
{
    // If we are in this function, then the user has already configured profiling by setting CORECLR_ENABLE_PROFILING to 1
    // and by correctly pointing the CORECLR_PROFILER_XXX variables.
    // However, we still want to respect the DD_PROFILING_ENABLED variable for:
    //  - consistency with other profiling products;
    //  - supporting scenarios where CORECLR_PROFILER_XXX point to the shared native loader, where some of the suit's products
    //    are enabled, but profiling is explicitly disabled;
    //  - supporting a scenario where CORECLR_PROFILER_XXX is set machine-wide and DD_PROFILING_ENABLED is set per service.
    shared::WSTRING isProfilingEnabledConfigStr = shared::GetEnvironmentValue(EnvironmentVariables::ProfilingEnabled);
    bool isEnabled = false;  // disabled by default

    if (isProfilingEnabledConfigStr.empty())
    {
        Log::Info("No \"", EnvironmentVariables::ProfilingEnabled, "\" environment variable has been found.",
                  " Using default (", isEnabled, ").");
    }
    else
    {
        if (!shared::TryParseBooleanEnvironmentValue(isProfilingEnabledConfigStr, isEnabled))
        {
            Log::Info("Invalid value \"", isProfilingEnabledConfigStr, "\" for \"",
                      EnvironmentVariables::ProfilingEnabled, "\" environment variable.",
                      " Using default (", isEnabled, ").");
        }
        else
        {
            Log::Info("Value \"", isProfilingEnabledConfigStr, "\" for \"",
                      EnvironmentVariables::ProfilingEnabled, "\" environment variable.",
                      " Enable = ", isEnabled);
        }
    }

    return isEnabled;
}

class __declspec(uuid("BD1A650D-AC5D-4896-B64F-D6FA25D6B26A")) CorProfilerCallback;

extern "C" HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    // {bd1a650d-ac5d-4896-b64f-d6fa25d6b26a}
    const GUID CLSID_CorProfiler = {0xbd1a650d,
                                    0xac5d,
                                    0x4896,
                                    {0xb6, 0x4f, 0xd6, 0xfa, 0x25, 0xd6, 0xb2, 0x6a}};

    if (ppv == nullptr)
    {
        Log::Info("DllGetClassObject(): the specified out-param 'ppv' is null.");
        return E_FAIL;
    }

    if (rclsid != CLSID_CorProfiler)
    {
        Log::Info(
            "DllGetClassObject(): the specified 'rclsid' ",
            riid.Data1, "-", riid.Data2, "-", riid.Data3, "-", riid.Data4,
            " is not CLSID_CorProfiler.");
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    if (!IsProfilingEnabled())
    {
        Log::Info("DllGetClassObject(): Profiling is not enabled.");

        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

#ifdef ARM64
    Log::Warn("Profiler is deactivated because it runs on an unsupported architecture.");
    return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
#endif

    CorProfilerCallbackFactory* factory = new CorProfilerCallbackFactory();
    if (factory == nullptr)
    {
        Log::Error("DllGetClassObject(): Fail to create CorProfilerCallbackFactory.");
        return E_FAIL;
    }

    HRESULT hr = factory->QueryInterface(riid, ppv);
    if (FAILED(hr))
    {
        Log::Error("DllGetClassObject(): Fail to query interface from CorProfilerCallbackFactory (hr=0x", std::hex, hr, std::dec, ")");
    }
    else
    {
        Log::Info("DllGetClassObject(): Returning an instance of CorProfilerCallbackFactory (hr=0x", std::hex, hr, std::dec, ")");
    }
    return hr;
}

extern "C" HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    Log::Info("DllCanUnloadNow() invoked.");

    return S_OK;
}
