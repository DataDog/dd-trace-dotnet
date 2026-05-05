// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TestProfilerClassFactory.h"
#include "Log.h"

#ifdef _WIN32
#include <windows.h>
#endif

HINSTANCE DllHandle;

extern "C" BOOL STDMETHODCALLTYPE DllMain(HINSTANCE hInstDll, DWORD reason, LPVOID lpReserved)
{
    switch (reason)
    {
        case DLL_PROCESS_ATTACH:
            Log::Info("TestProfiler::DllMain: DLL loaded.");
            Log::Info("TestProfiler::DllMain: Pointer size: ", 8 * sizeof(void*), " bits.");
            break;

        case DLL_PROCESS_DETACH:
            Log::Info("TestProfiler::DllMain: DLL unloaded.");
            break;
    }

    DllHandle = hInstDll;

    return TRUE;
}

// DLL export for getting the class object
extern "C" HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    Log::Info("TestProfiler::DllGetClassObject called");

    // Profiler GUID: {12345678-ABCD-1234-ABCD-123456789ABC}
    // This should match the GUID set in the environment variables
    const GUID CLSID_TestProfiler = {0x12345678, 0xABCD, 0x1234, {0xAB, 0xCD, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC}};

    Log::Info("TestProfiler::DllGetClassObject: Checking CLSID...");
    if (rclsid != CLSID_TestProfiler)
    {
        Log::Warn("TestProfiler::DllGetClassObject: CLSID mismatch!");
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    Log::Info("TestProfiler::DllGetClassObject: Creating TestProfilerClassFactory...");
    auto factory = new TestProfilerClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    Log::Info("TestProfiler::DllGetClassObject: Calling factory->QueryInterface...");
    return factory->QueryInterface(riid, ppv);
}

extern "C" HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    Log::Info("TestProfiler::DllCanUnloadNow() invoked.");
    return S_OK;
}
