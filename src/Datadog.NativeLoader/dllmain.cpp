// dllmain.cpp : Defines the entry point for the DLL application.
#include "class_factory.h"

#include "logging.h"
#include "proxy.h"

datadog::nativeloader::DynamicDispatcher* dispatcher;

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

                Debug("DllMain - DLL_PROCESS_ATTACH");

                dispatcher = new datadog::nativeloader::DynamicDispatcher();

#if _WIN32
                std::string instanceDef = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}=C:\\github\\dd-trace-dotnet\\src\\bin\\windows-tracer-home\\win-x64\\Datadog.Trace.ClrProfiler.Native.dll";
#elif LINUX
                std::string instanceDef = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}=/Users/tony.redondo/repos/github/DataDog/dd-trace-dotnet/bin/tracer-home/Datadog.Trace.ClrProfiler.Native.so";
#elif MACOS
                std::string instanceDef = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}=/Users/tony.redondo/repos/github/DataDog/dd-trace-dotnet/bin/tracer-home/Datadog.Trace.ClrProfiler.Native.dylib";
#endif

                std::unique_ptr<datadog::nativeloader::DynamicInstance> instance = std::make_unique<datadog::nativeloader::DynamicInstance>(instanceDef);
                dispatcher->Add(instance);

                // std::unique_ptr<datadog::nativeloader::DynamicInstance> instance2 = std::make_unique<datadog::nativeloader::DynamicInstance>(instanceDef);
                // dispatcher->Add(instance2);

                break;
            }
            case DLL_THREAD_ATTACH:
                // Do thread-specific initialization.
                Debug("DllMain - DLL_THREAD_ATTACH");

                break;

            case DLL_THREAD_DETACH:
                // Do thread-specific cleanup.
                Debug("DllMain - DLL_THREAD_DETACH");

                break;

            case DLL_PROCESS_DETACH:
                // Perform any necessary cleanup.
                Debug("DllMain - DLL_PROCESS_DETACH");

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

        auto factory = new ClassFactory(dispatcher);
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
