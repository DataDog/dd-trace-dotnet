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

namespace datadog::shared::nativeloader
{

    // ************************************************************************

    //
    // public
    //

    DynamicDispatcherImpl::DynamicDispatcherImpl()
    {
        m_continuousProfilerInstance = nullptr;
        m_tracerInstance = nullptr;
        m_customInstance = nullptr;
    }

    void DynamicDispatcherImpl::LoadConfiguration(std::string configFilePath)
    {
        if (!std::filesystem::exists(configFilePath))
        {
            Warn("DynamicDispatcherImpl::LoadConfiguration: Configuration file doesn't exist.");
            return;
        }

        std::ifstream t(configFilePath);

        // Gets the configuration file folder
        std::filesystem::path configFolder = std::filesystem::path(configFilePath).remove_filename();
        Debug("DynamicDispatcherImpl::LoadConfiguration: Config Folder: ", configFolder);

        // Get the current path
        std::filesystem::path oldCurrentPath = std::filesystem::current_path();
        Debug("DynamicDispatcherImpl::LoadConfiguration: Current Path: ", oldCurrentPath);

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
                if (lineArray.size() != 4)
                {
                    Warn("DynamicDispatcherImpl::LoadConfiguration: Invalid line: ", line);
                }

                std::string type = lineArray[0];
                std::string idValue = lineArray[1];
                std::string osArchValue = lineArray[2];
                std::string filepathValue = lineArray[3];

                if (type == "TRACER" || type == "PROFILER" || type == "CUSTOM")
                {
                    if (osArchValue == currentOsArch)
                    {
                        // Convert possible relative paths to absolute paths using the configuration file folder as base
                        // (current_path)
                        std::string absoluteFilepathValue = std::filesystem::absolute(filepathValue).string();
                        Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Loading: ", filepathValue, " [AbsolutePath=", absoluteFilepathValue,"]");
                        if (std::filesystem::exists(absoluteFilepathValue))
                        {
                            Debug("[", type, "] Creating a new DynamicInstance object");

                            WSTRING env_key;

                            if (type == "TRACER")
                            {
                                this->m_tracerInstance =
                                    std::make_unique<DynamicInstanceImpl>(absoluteFilepathValue, idValue);
                                env_key = WStr("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH");
                            }
                            else if (type == "PROFILER")
                            {
                                this->m_continuousProfilerInstance =
                                    std::make_unique<DynamicInstanceImpl>(absoluteFilepathValue, idValue);
                                env_key = WStr("DD_INTERNAL_PROFILING_NATIVE_ENGINE_PATH");
                            }
                            else if (type == "CUSTOM")
                            {
                                this->m_customInstance =
                                    std::make_unique<DynamicInstanceImpl>(absoluteFilepathValue, idValue);
                                env_key = WStr("DD_INTERNAL_CUSTOM_CLR_PROFILER_PATH");
                            }

                            WSTRING env_value = ToWSTRING(absoluteFilepathValue);
                            Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Setting environment variable: ", env_key, "=", env_value);
                            bool envVal = SetEnvironmentValue(env_key, env_value);
                            Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] SetEnvironmentValue result: ", envVal);
                        }
                        else
                        {
                            Warn("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Dynamic library for '", absoluteFilepathValue,
                                 "' cannot be loadeds, file doesn't exist.");
                        }
                    }
                    else
                    {
                        const std::string* findRes = std::find(std::begin(allOsArch), std::end(allOsArch), osArchValue);
                        if (findRes == std::end(allOsArch))
                        {
                            Warn("DynamicDispatcherImpl::LoadConfiguration: [", type, "] The OS and Architecture is invalid: ", osArchValue);
                        }
                    }
                }
                else
                {
                    Warn("DynamicDispatcherImpl::LoadConfiguration: COR Profiler Type is invalid: ", type);
                }
            }
        }
        t.close();

        // Set the current path to the original one
        std::filesystem::current_path(oldCurrentPath);
    }

    HRESULT DynamicDispatcherImpl::LoadClassFactory(REFIID riid)
    {
        HRESULT GHR = S_OK;

        if (m_continuousProfilerInstance != nullptr)
        {
            HRESULT result = m_continuousProfilerInstance->LoadClassFactory(riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load continuous profiler class factory in: ",
                     m_continuousProfilerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_continuousProfilerInstance.release();
                GHR = result;
            }
        }

        if (m_tracerInstance != nullptr)
        {
            HRESULT result = m_tracerInstance->LoadClassFactory(riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load tracer class factory in: ", m_tracerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_tracerInstance.release();
                GHR = result;
            }
        }

        if (m_customInstance != nullptr)
        {
            HRESULT result = m_customInstance->LoadClassFactory(riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load custom class factory in: ", m_customInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_customInstance.release();
                GHR = result;
            }
        }

        return GHR;
    }

    HRESULT DynamicDispatcherImpl::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        HRESULT GHR = S_OK;

        if (m_continuousProfilerInstance != nullptr)
        {
            HRESULT result = m_continuousProfilerInstance->LoadInstance(pUnkOuter, riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the continuous profiler instance in: ",
                     m_continuousProfilerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_continuousProfilerInstance.release();
                GHR = result;
            }
        }

        if (m_tracerInstance != nullptr)
        {
            HRESULT result = m_tracerInstance->LoadInstance(pUnkOuter, riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the tracer instance in: ", m_tracerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_tracerInstance.release();
                GHR = result;
            }
        }

        if (m_customInstance != nullptr)
        {
            HRESULT result = m_customInstance->LoadInstance(pUnkOuter, riid);
            if (FAILED(result))
            {
                Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the custom instance in: ", m_customInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_customInstance.release();
                GHR = result;
            }
        }

        return GHR;
    }

    HRESULT STDMETHODCALLTYPE DynamicDispatcherImpl::DllCanUnloadNow()
    {
        HRESULT result = S_OK;

        if (m_continuousProfilerInstance != nullptr)
        {
            HRESULT hr = m_continuousProfilerInstance->DllCanUnloadNow();
            if (FAILED(hr))
            {
                Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the continuous profiler DllCanUnloadNow in: ",
                     m_continuousProfilerInstance->GetFilePath());
                result = hr;
            }
            else if (hr != S_OK)
            {
                // If we get something different than S_OK then we keep that result because the DLL cannot be unloaded.
                result = hr;
            }
        }

        if (m_tracerInstance != nullptr)
        {
            HRESULT hr = m_tracerInstance->DllCanUnloadNow();
            if (FAILED(hr))
            {
                Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the tracer DllCanUnloadNow in: ", m_tracerInstance->GetFilePath());
                result = hr;
            }
            else if (hr != S_OK)
            {
                // If we get something different than S_OK then we keep that result because the DLL cannot be unloaded.
                result = hr;
            }
        }

        if (m_customInstance != nullptr)
        {
            HRESULT hr = m_customInstance->DllCanUnloadNow();
            if (FAILED(hr))
            {
                Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the custom DllCanUnloadNow in: ", m_customInstance->GetFilePath());
                result = hr;
            }
            else if (hr != S_OK)
            {
                // If we get something different than S_OK then we keep that result because the DLL cannot be unloaded.
                result = hr;
            }
        }

        return result;
    }

    IDynamicInstance* DynamicDispatcherImpl::GetContinuousProfilerInstance()
    {
        return m_continuousProfilerInstance.get();
    }

    IDynamicInstance* DynamicDispatcherImpl::GetTracerInstance()
    {
        return m_tracerInstance.get();
    }

    IDynamicInstance* DynamicDispatcherImpl::GetCustomInstance()
    {
        return m_customInstance.get();
    }

} // namespace datadog::shared::nativeloader