// dllmain.cpp : Defines the entry point for the DLL application.
#include "class_factory.h"

#include <filesystem>
#include <fstream>

#include "logging.h"
#include "pal.h"
#include "proxy.h"

#if _WIN32
const std::string dynExtension = ".dll";
#elif LINUX
const std::string dynExtension = ".so";
#elif MACOS
const std::string dynExtension = ".dylib";
#endif

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

                // *****************************************************************************************************************

                WSTRING profiler_path;
                profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH"));
                if (profiler_path.length() == 0)
                {
                    profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("COR_PROFILER_PATH"));
                }

#if BIT64
                if (profiler_path.length() == 0)
                {
                    profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_64"));
                }
                if (profiler_path.length() == 0)
                {
                    profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("COR_PROFILER_PATH_64"));
                }
#else
                if (profiler_path.length() == 0)
                {
                    profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_32"));
                }
                if (profiler_path.length() == 0)
                {
                    profiler_path = datadog::nativeloader::GetEnvironmentValue(WStr("COR_PROFILER_PATH_32"));
                }
#endif

                // *****************************************************************************************************************

                auto path = std::filesystem::path(profiler_path).remove_filename().append("loader.conf").string();
                std::ifstream t;
                t.open(path);
                while (t)
                {
                    std::string line;
                    std::getline(t, line);
                    line = Trim(line);
                    if (line.length() != 0)
                    {
                        Debug(line);

                        size_t delimiter = line.find("=");
                        std::string filepath = line.substr(delimiter + 1);
                        std::string clsid = line.substr(0, delimiter);

                        filepath = std::filesystem::path(filepath).replace_extension(dynExtension).string();
                        if (std::filesystem::exists(filepath))
                        {
                            std::unique_ptr<datadog::nativeloader::DynamicInstance> instance =
                                std::make_unique<datadog::nativeloader::DynamicInstance>(filepath, clsid);

                            dispatcher->Add(instance);
                        }
                        else
                        {
                            Warn("Dynamic library doesn't exists: ", filepath);
                        }
                    }
                }
                t.close();

                // *****************************************************************************************************************
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
