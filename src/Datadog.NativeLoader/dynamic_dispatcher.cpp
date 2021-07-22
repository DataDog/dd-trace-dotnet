#include "dynamic_dispatcher.h"

#include <filesystem>
#include <fstream>
#include <unordered_map>

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

        std::ifstream t;
        t.open(configFilePath);

        // Gets the configuration file folder
        std::filesystem::path configFolder = std::filesystem::path(configFilePath).remove_filename();
        Debug("Config Folder: ", configFolder);

        // Get the current path
        std::filesystem::path oldCurrentPath = std::filesystem::current_path();
        Debug("Current Path: ", oldCurrentPath);

        // Set the current path to the configuration folder (to allow relative paths)
        std::filesystem::current_path(configFolder);

        const std::string allOsArch[12] = {
            "win-x64", "linux-x64", "osx-x64",
            "win-x86", "linux-x86", "osx-x86",
            "win-arm64", "linux-arm64", "osx-arm64",
            "win-arm", "linux-arm", "osx-arm",
        };

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
                    std::string absoluteFilepathValue = std::filesystem::absolute(filepathValue).string();
                    Debug("Loading: ", filepathValue, " [AbsolutePath=", absoluteFilepathValue,"]");
                    if (std::filesystem::exists(absoluteFilepathValue))
                    {
                        Debug("Creating a new DynamicInstance object");
                        std::unique_ptr<DynamicInstance> instance =
                            std::make_unique<DynamicInstance>(absoluteFilepathValue, idValue);
                        this->Add(instance);
                        WSTRING env_key = WStr("PROFID_") + ToWSTRING(idValue);
                        WSTRING env_value = ToWSTRING(absoluteFilepathValue);
                        Debug("Setting environment variable: ", env_key, "=", env_value);
                        bool envVal = SetEnvironmentValue(env_key, env_value);
                        Debug("SetEnvironmentValue result: ", envVal);
                    }
                    else
                    {
                        Warn("Dynamic library for '", absoluteFilepathValue, "' cannot be loadeds, file doesn't exist.");
                    }
                }
                else
                {
                    const std::string* findRes = std::find(std::begin(allOsArch), std::end(allOsArch), osArchValue);
                    if (findRes == std::end(allOsArch))
                    {
                        Warn("The OS and Architecture is invalid: ", osArchValue);
                    }
                }
            }
        }
        t.close();

        // Set the current path to the original one
        std::filesystem::current_path(oldCurrentPath);
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
        HRESULT result = S_OK;
        for (const std::unique_ptr<DynamicInstance>& dynIns : m_instances)
        {
            HRESULT localResult = dynIns->DllCanUnloadNow();
            if (FAILED(localResult))
            {
                Warn("Error calling DllCanUnloadNow in: ", dynIns->GetFilePath());
                result = localResult;
            }
            else if (localResult != S_OK)
            {
                // If we get something different than S_OK then we keep that result because the DLL cannot be unloaded.
                result = localResult;
            }
        }
        return result;
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