// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <iostream>
#include <unknwn.h>

#include "CorProfilerCallback.h"
#include "CorProfilerCallbackFactory.h"
#include "EnvironmentVariables.h"
#include "Log.h"

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
            Log::Info("Pointer size: ", 8 * sizeof(void*), " bits.");
            break;

        case DLL_PROCESS_DETACH:
            Log::Info("Profiler DLL unloaded.");
            break;
    }

    DllHandle = hInstDll;

    return TRUE;
}

bool CheckProfilingEnabledEnvironmentVariable()
{
    // If we are in this function, then the user has already configured profiling by setting CORECLR_ENABLE_PROFILING to 1
    // and by correctly pointing the CORECLR_PROFILER_XXX variables.
    // However, we still want to respect the DD_PROFILING_ENABLED variable for:
    //  - consistency with other profiling products;
    //  - supporting scenarios where CORECLR_PROFILER_XXX point to the shared native loader, where some of the suit's products
    //    are enabled, but profiling is explicitly disabled;
    //  - supporting a scenario where CORECLR_PROFILER_XXX is set machine-wide and DD_PROFILING_ENABLED is set per service.
    const bool IsProfilingEnabledDefault = false;
    shared::WSTRING isProfilingEnabledConfigStr = shared::GetEnvironmentValue(EnvironmentVariables::ProfilingEnabled);

    // no environment variable set
    if (isProfilingEnabledConfigStr.empty())
    {
        Log::Info("No \"", EnvironmentVariables::ProfilingEnabled, "\" environment variable has been found.",
                  " Using default (", IsProfilingEnabledDefault, ").");

        return IsProfilingEnabledDefault;
    }
    else
    {
        bool isProfilingEnabled;
        if (!shared::TryParseBooleanEnvironmentValue(isProfilingEnabledConfigStr, isProfilingEnabled))
        {
            // invalid value for environment variable
            Log::Info("Invalid value \"", isProfilingEnabledConfigStr, "\" for \"",
                      EnvironmentVariables::ProfilingEnabled, "\" environment variable.",
                      " Using default (", IsProfilingEnabledDefault, ").");

            return IsProfilingEnabledDefault;
        }
        else
        {
            // take environment variable into account
            Log::Info("Value \"", isProfilingEnabledConfigStr, "\" for \"",
                      EnvironmentVariables::ProfilingEnabled, "\" environment variable.",
                      " Enable = ", isProfilingEnabled);

            return isProfilingEnabled;
        }
    }
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
        Log::Info("DllGetClassObject(): Cannot return an instance of CorProfilerCallbackFactory because the specified out-param 'ppv' is null.");
        return E_FAIL;
    }

    if (rclsid != CLSID_CorProfiler)
    {
        Log::Info("DllGetClassObject(): Cannot return an instance of factory because the specified 'rclsid' is not known.");
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    bool isProfilingEnabled = CheckProfilingEnabledEnvironmentVariable();
    if (!isProfilingEnabled)
    {
        Log::Info("DllGetClassObject(): Will not return an instance of CorProfilerCallbackFactory because Profiling has been"
                  " disabled via an environment variable.");

        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

    CorProfilerCallbackFactory* factory = new CorProfilerCallbackFactory();
    if (factory == nullptr)
    {
        Log::Info("DllGetClassObject(): Cannot return an instance of CorProfilerCallbackFactory because the instantiation failed.");
        return E_FAIL;
    }

    HRESULT hr = factory->QueryInterface(riid, ppv);

    Log::Info("DllGetClassObject(): Returning an instance of CorProfilerCallbackFactory (hr=0x", std::hex, hr, std::dec, ")");
    return hr;
}

extern "C" HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    Log::Debug("DllCanUnloadNow() invoked.");

    return S_OK;
}
