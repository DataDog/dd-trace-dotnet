// dllmain.cpp : Defines the entry point for the DLL application.
#include "cor_profiler_class_factory.h"

#include "logging.h"
#include "dynamic_dispatcher.h"
#include "util.h"

using namespace datadog::shared::nativeloader;

IDynamicDispatcher* dispatcher;

extern "C"
{
    BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
    {
        // Perform actions based on the reason for calling.
        switch (ul_reason_for_call)
        {
            case DLL_PROCESS_ATTACH:
            {
                // Initialize once for each new process.
                // Return FALSE to fail DLL load.

                Debug("DllMain: DLL_PROCESS_ATTACH");
                Debug("DllMain: Pointer size: ", 8 * sizeof(void*), " bits.");

                dispatcher = new DynamicDispatcherImpl();
                dispatcher->LoadConfiguration(GetConfigurationFilePath());

                // *****************************************************************************************************************
                break;
            }
            case DLL_THREAD_ATTACH:
                // Do thread-specific initialization.
                Debug("DllMain: DLL_THREAD_ATTACH");

                break;

            case DLL_THREAD_DETACH:
                // Do thread-specific cleanup.
                Debug("DllMain: DLL_THREAD_DETACH");

                break;

            case DLL_PROCESS_DETACH:
                // Perform any necessary cleanup.
                Debug("DllMain: DLL_PROCESS_DETACH");

                break;
        }
        return TRUE; // Successful DLL_PROCESS_ATTACH.
    }

    HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
    {
        Debug("DllGetClassObject");

        // {50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}
        const GUID CLSID_CorProfiler = {0x50da5eed, 0xf1ed, 0xb00b, {0x10, 0x55, 0x5a, 0xfe, 0x55, 0xa1, 0xad, 0xe5}};

        if (ppv == NULL || rclsid != CLSID_CorProfiler)
        {
            return E_FAIL;
        }

        auto factory = new CorProfilerClassFactory(dispatcher);
        if (factory == NULL)
        {
            return E_FAIL;
        }

        return factory->QueryInterface(riid, ppv);
    }

    HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
    {
        Debug("DllCanUnloadNow");

        return dispatcher->DllCanUnloadNow();
    }
}
