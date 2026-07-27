// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <iostream>
#include <unknwn.h>

#include "Configuration.h"
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

bool IsProfilingEnabled(Configuration const& configuration)
{
    // If we are in this function, then the user has already configured profiling by setting CORECLR_ENABLE_PROFILING to 1
    // and by correctly pointing the CORECLR_PROFILER_XXX variables.
    //
    // With Stable Configuration, there is a kill switch to read the env vars instead of waiting for the managed layer
    // to decide if the profiler should be disabled/enabled/auto
    if (configuration.IsManagedActivationEnabled())
    {
        Log::Info("Waiting for managed configuration to enable/disable the profiler");
        return true;
    }

    Log::Warn("Managed configuration will be skipped to enable/disable the profiler: reading local configuration instead");

    // With Single Step Instrumentation deployment, it is possible that the profiler needs to be loaded (to emit telemetry metrics)
    // but not started (i.e. no profiling) so this function will return true in that case.
    //
    auto enablementStatus = configuration.GetEnablementStatus();
    auto deploymentMode = configuration.GetDeploymentMode();

    Log::Info(".NET Profiler deployment mode: ", to_string(deploymentMode));

    if (enablementStatus == EnablementStatus::ManuallyEnabled)
    {
        Log::Info(".NET Profiler is explicitly enabled.");
        return true;
    }

    if (enablementStatus == EnablementStatus::ManuallyDisabled)
    {
        Log::Info(".NET Profiler is explicitly disabled.");
        return false;
    }

    if (enablementStatus == EnablementStatus::Auto)
    {
        Log::Info(".NET Profiler is installed via Single Step Instrumentation and automatically enabled. It will start later.");

        // delay start with SSI is now supported
        return true;
    }

    if (enablementStatus == EnablementStatus::NotSet)
    {
        // in that case, when deployed with SSI, we accept the profiler to be loaded just for the telemetry metrics
        if (deploymentMode == DeploymentMode::SingleStepInstrumentation)
        {
            Log::Info(".NET Profiler is loaded via Single Step Instrumentation but not enabled.");
            return true;
        }

        Log::Info(".NET Profiler environment variable '", EnvironmentVariables::ProfilerEnabled, "' was not set. The .NET profiler will be disabled.");
        return false;
    }

    Log::Warn(".NET Profiler is disabled for an unknown reason.");
    return false;
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

    auto configuration = std::make_shared<Configuration>();

    if (!IsProfilingEnabled(*configuration))
    {
        Log::Info("DllGetClassObject(): Profiling is not enabled.");

        return CORPROF_E_PROFILER_CANCEL_ACTIVATION;
    }

    CorProfilerCallbackFactory* factory = new CorProfilerCallbackFactory(std::move(configuration));
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
