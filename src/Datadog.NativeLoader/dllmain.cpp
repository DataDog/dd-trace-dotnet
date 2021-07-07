// dllmain.cpp : Defines the entry point for the DLL application.
#include "class_factory.h"

#include <filesystem>
#include <fstream>
#include <unordered_map>
#include <vector>

#include "logging.h"
#include "pal.h"
#include "proxy.h"
#include "util.h"

using namespace datadog::nativeloader;

DynamicDispatcher* dispatcher;

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

                dispatcher = new DynamicDispatcher();

                std::string configFilePath = GetConfigurationFilePath();

                std::unordered_map<std::string, bool> guidBoolMap;
                std::ifstream t;
                t.open(configFilePath);
                while (t)
                {
                    std::string line;
                    std::getline(t, line);
                    line = Trim(line);
                    if (line.length() != 0)
                    {
                        Debug(line);

                        if (line[0] == '#')
                        {
                            continue;
                        }

                        std::vector<std::string> lineArray = Split(line, ';');
                        std::string clsid = lineArray[0];
                        std::string filepath = lineArray[1];

                        if (!std::filesystem::exists(filepath))
                        {
                            filepath = std::filesystem::path(filepath).replace_extension(dynExtension).string();
                        }

                        if (std::filesystem::exists(filepath))
                        {
                            guidBoolMap[clsid] = true;
                            std::unique_ptr<DynamicInstance> instance = std::make_unique<DynamicInstance>(filepath, clsid);
                            dispatcher->Add(instance);
                            auto envVal = SetEnvironmentValue(WStr("PROFID_") + ToWSTRING(clsid), ToWSTRING(filepath));
                            Debug("SetEnvVal: ", envVal, "; ", WStr("PROFID_") + ToWSTRING(clsid), "=", ToWSTRING(filepath));
                        }
                        else if (guidBoolMap.find(clsid) == guidBoolMap.end())
                        {
                            guidBoolMap[clsid] = false;
                        }
                    }
                }
                t.close();

                for (const auto item : guidBoolMap)
                {
                    if (!item.second)
                    {
                        Warn("Dynamic library for '", item.first, "' cannot be loaded");
                    }
                }

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
