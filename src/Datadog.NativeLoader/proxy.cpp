#include "proxy.h"

#include <filesystem>
#include <fstream>
#include <unordered_map>

#include "guid.h"
#include "logging.h"
#include "pal.h"

#if AMD64

#if _WINDOWS
const std::string currentOsArch = "win-x64";
#elif LINUX
const std::string currentOsArch = "linux-x64";
#elif MACOS
const std::string currentOsArch = "osx-x64";
#else
#error "currentOsArch not defined."
#endif

#elif X86

#if _WINDOWS
const std::string currentOsArch = "win-x86";
#elif LINUX
const std::string currentOsArch = "linux-x86";
#elif MACOS
const std::string currentOsArch = "osx-x86";
#else
#error "currentOsArch not defined."
#endif

#elif ARM64

#if _WINDOWS
const std::string currentOsArch = "win-arm64";
#elif LINUX
const std::string currentOsArch = "linux-arm64";
#elif MACOS
const std::string currentOsArch = "osx-arm64";
#else
#error "currentOsArch not defined."
#endif

#elif ARM

#if _WINDOWS
const std::string currentOsArch = "win-arm";
#elif LINUX
const std::string currentOsArch = "linux-arm";
#elif MACOS
const std::string currentOsArch = "osx-arm";
#else
#error "currentOsArch not defined."
#endif

#else
#error "currentOsArch not defined."
#endif


namespace datadog
{
namespace nativeloader
{

    // ************************************************************************

    //
    // private
    //

    HRESULT DynamicInstance::EnsureDynamicLibraryIsLoaded()
    {
        if (!m_loaded)
        {
            m_instance = LoadDynamicLibrary(m_filepath);
            m_loaded = true;
        }

        return m_instance != nullptr ? S_OK : E_FAIL;
    }

    HRESULT DynamicInstance::DllGetClassObject(REFIID riid, LPVOID* ppv)
    {
        // Check if the library is loaded
        if (FAILED(EnsureDynamicLibraryIsLoaded()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_getClassObjectPtr == nullptr)
        {
            m_getClassObjectPtr =
                static_cast<dllGetClassObjectPtr>(GetExternalFunction(m_instance, "DllGetClassObject"));
        }

        // If we have the function pointer we call the function
        if (m_getClassObjectPtr != nullptr)
        {
            return m_getClassObjectPtr(m_clsid, riid, ppv);
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    //
    // public
    //

    DynamicInstance::DynamicInstance(std::string filePath, std::string clsid)
    {
        m_filepath = filePath;
        m_clsid_string = clsid;
        m_clsid = guid_parse::make_guid(clsid);
        m_loaded = false;
        m_instance = nullptr;
        m_getClassObjectPtr = nullptr;
        m_canUnloadNow = nullptr;
        m_classFactory = nullptr;
        m_corProfilerCallback = nullptr;
    }

    DynamicInstance::~DynamicInstance()
    {
        m_corProfilerCallback = nullptr;
        m_classFactory = nullptr;
        m_canUnloadNow = nullptr;
        m_getClassObjectPtr = nullptr;
        m_loaded = false;

        if (m_instance != nullptr)
        {
            if (!FreeDynamicLibrary(m_instance))
            {
                Warn("Error unloading: ", m_filepath, " dynamic library.");
            }
            m_instance = nullptr;
        }
    }


    HRESULT DynamicInstance::LoadClassFactory(REFIID riid)
    {
        LPVOID ppv;
        HRESULT res = DllGetClassObject(riid, &ppv);
        if (SUCCEEDED(res))
        {
            m_classFactory = static_cast<IClassFactory*>(ppv);
        }
        else
        {
            Warn("Error getting IClassFactory from: ", m_filepath);
        }

        Debug("LoadClassFactory: ", res);
        return res;
    }

    HRESULT DynamicInstance::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        Debug("Running LoadInstance: ");

        // Check if the class factory instance is loaded.
        if (m_classFactory == nullptr)
        {
            return E_FAIL;
        }

        // Creates the profiler callback instance from the class factory
        Debug("m_classFactory: ", HexStr(m_classFactory, sizeof(IClassFactory*)));
        HRESULT res =
            m_classFactory->CreateInstance(nullptr, __uuidof(ICorProfilerCallback10), (void**) &m_corProfilerCallback);
        if (FAILED(res))
        {
            m_corProfilerCallback = nullptr;
            Warn("Error getting ICorProfilerCallback10 from: ", m_filepath);
        }

        Debug("LoadInstance: ", res);
        return res;
    }

    HRESULT STDMETHODCALLTYPE DynamicInstance::DllCanUnloadNow()
    {
        // Check if the library is loaded
        if (FAILED(EnsureDynamicLibraryIsLoaded()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_canUnloadNow == nullptr)
        {
            m_canUnloadNow = static_cast<dllCanUnloadNow>(GetExternalFunction(m_instance, "DllCanUnloadNow"));
        }

        // If we have the function pointer we call the function
        if (m_canUnloadNow != nullptr)
        {
            return m_canUnloadNow();
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    ICorProfilerCallback10* DynamicInstance::GetProfilerCallback()
    {
        return m_corProfilerCallback;
    }

    std::string DynamicInstance::GetFilePath()
    {
        return m_filepath;
    }

    std::string DynamicInstance::GetClsId()
    {
        return m_clsid_string;
    }

    // ************************************************************************

    //
    // public
    //

    DynamicDispatcher::DynamicDispatcher()
    {
        m_instances = std::vector<std::unique_ptr<DynamicInstance>>();
    }

    void DynamicDispatcher::Add(std::unique_ptr<DynamicInstance>& instance)
    {
        if (instance.get() != nullptr)
        {
            m_instances.push_back(std::move(instance));
        }
    }

    void DynamicDispatcher::LoadConfiguration(std::string configFilePath)
    {
        if (!std::filesystem::exists(configFilePath))
        {
            Warn("Configuration file doesn't exist.");
            return;
        }

        std::unordered_map<std::string, bool> guidBoolMap;
        std::ifstream t;
        t.open(configFilePath);

        // Gets the configuration file folder
        std::filesystem::path configFolder = std::filesystem::path(configFilePath).remove_filename();

        // Get the current path
        std::filesystem::path oldCurrentPath = std::filesystem::current_path();

        // Set the current path to the configuration folder (to allow relative paths)
        std::filesystem::current_path(configFolder);

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
                std::string idValue = lineArray[0];
                std::string osArchValue = lineArray[1];
                std::string filepathValue = lineArray[2];

                if (osArchValue == currentOsArch)
                {
                    // Convert possible relative paths to absolute paths using the configuration file folder as base
                    // (current_path)
                    filepathValue = std::filesystem::absolute(filepathValue).string();
                    if (std::filesystem::exists(filepathValue))
                    {
                        guidBoolMap[idValue] = true;
                        std::unique_ptr<DynamicInstance> instance =
                            std::make_unique<DynamicInstance>(filepathValue, idValue);
                        this->Add(instance);
                        WSTRING env_key = WStr("PROFID_") + ToWSTRING(idValue);
                        WSTRING env_value = ToWSTRING(filepathValue);
                        bool envVal = SetEnvironmentValue(env_key, env_value);
                        Debug("SetEnvVal: ", envVal, "; ", env_key, "=", env_value);
                    }
                    else if (guidBoolMap.find(idValue) == guidBoolMap.end())
                    {
                        guidBoolMap[idValue] = false;
                    }
                }
            }
        }
        t.close();

        // Set the current path to the original one
        std::filesystem::current_path(oldCurrentPath);
        for (const std::pair<const std::string, bool> item : guidBoolMap)
        {
            if (!item.second)
            {
                Warn("Dynamic library for '", item.first, "' cannot be loaded");
            }
        }
    }

    HRESULT DynamicDispatcher::LoadClassFactory(REFIID riid)
    {
        for (const std::unique_ptr<DynamicInstance>& dynIns : m_instances)
        {
            HRESULT localResult = dynIns->LoadClassFactory(riid);
            if (FAILED(localResult))
            {
                Warn("Error trying to load class factory in: ", dynIns->GetFilePath());
                return localResult;
            }
        }
        return S_OK;
    }

    HRESULT DynamicDispatcher::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        for (const std::unique_ptr<DynamicInstance>& dynIns : m_instances)
        {
            HRESULT localResult = dynIns->LoadInstance(pUnkOuter, riid);
            if (FAILED(localResult))
            {
                Warn("Error trying to load the instance in: ", dynIns->GetFilePath());
                return localResult;
            }
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DynamicDispatcher::DllCanUnloadNow()
    {
        for (const std::unique_ptr<DynamicInstance>& dynIns : m_instances)
        {
            HRESULT localResult = dynIns->DllCanUnloadNow();
            if (FAILED(localResult))
            {
                Warn("Error calling DllCanUnloadNow in: ", dynIns->GetFilePath());
                return localResult;
            }
        }
        return S_OK;
    }

    HRESULT DynamicDispatcher::Execute(std::function<HRESULT(ICorProfilerCallback10*)> func)
    {
        if (func == nullptr)
        {
            return E_FAIL;
        }

        HRESULT result = S_OK;
        for (const std::unique_ptr<DynamicInstance>& dynIns : m_instances)
        {
            ICorProfilerCallback10* profilerCallback = dynIns->GetProfilerCallback();
            if (profilerCallback == nullptr)
            {
                Warn("Error trying to execute in: ", dynIns->GetFilePath());
                continue;
            }

            HRESULT localResult = func(profilerCallback);
            if (FAILED(localResult))
            {
                result = localResult;
            }
        }
        return result;
    }

    std::unique_ptr<DynamicInstance>* DynamicDispatcher::GetInstances()
    {
        return &m_instances[0];
    }

    size_t DynamicDispatcher::GetLength()
    {
        return m_instances.size();
    }

} // namespace nativeloader
} // namespace datadog