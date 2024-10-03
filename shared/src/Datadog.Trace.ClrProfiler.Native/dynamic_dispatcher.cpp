#include "dynamic_dispatcher.h"

#include <fstream>
#include <unordered_map>

#include "log.h"
#include "util.h"
#include "../../../shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "../../../shared/src/native-src/pal.h"
#include "../../../shared/src/native-src/util.h"

namespace datadog::shared::nativeloader
{

    // ************************************************************************

    //
    // public
    //

    DynamicDispatcherImpl::DynamicDispatcherImpl() :
        m_continuousProfilerInstance(nullptr),
        m_tracerInstance(nullptr),
        m_customInstance(nullptr),
        m_initialized(false),
        m_initializationResult(E_UNEXPECTED)
    {
    }

    HRESULT DynamicDispatcherImpl::Initialize()
    {
        if (m_initialized)
        {
            return m_initializationResult;
        }

        m_initialized = true;

        LoadConfiguration(GetConfigurationFilePath());

        m_initializationResult = LoadClassFactory(IID_IClassFactory);

        if (FAILED(m_initializationResult))
        {
            Log::Error("Error loading all cor profiler class factories.");
            return m_initializationResult;
        }

        m_initializationResult = LoadInstance();

        if (FAILED(m_initializationResult))
        {
            Log::Error("Error loading all cor profiler instances.");
            return m_initializationResult;
        }

        m_initializationResult = S_OK;
        return m_initializationResult;
    }

    void DynamicDispatcherImpl::LoadConfiguration(fs::path&& configFilePath)
    {
        ::shared::WSTRING nativeLoaderPath = ::shared::GetCurrentModuleFileName();
        ::shared::WSTRING nativeLoaderPathKey = WStr("DD_INTERNAL_NATIVE_LOADER_PATH");
        Log::Debug("DynamicDispatcherImpl::LoadConfiguration:  Setting environment variable: ", nativeLoaderPathKey, "=", nativeLoaderPath);
        bool nativeLoaderEnvSet = ::shared::SetEnvironmentValue(nativeLoaderPathKey, nativeLoaderPath);
        Log::Debug("DynamicDispatcherImpl::LoadConfiguration: SetEnvironmentValue result: ", nativeLoaderEnvSet);

        std::error_code ec; // fs::exists might throw if no error_code parameter is provided
        if (!fs::exists(configFilePath, ec))
        {
            Log::Warn("DynamicDispatcherImpl::LoadConfiguration: Configuration file doesn't exist.");
            return;
        }
        Log::Info("DynamicDispatcherImpl::LoadConfiguration: Reading configuration file from: ", configFilePath);
        std::ifstream t(configFilePath);

        // Gets the configuration file folder
        fs::path configFolder = fs::path(configFilePath).remove_filename();
        Log::Debug("DynamicDispatcherImpl::LoadConfiguration: Config Folder: ", configFolder);

        // Get the current path
        fs::path oldCurrentPath = fs::current_path();
        Log::Debug("DynamicDispatcherImpl::LoadConfiguration: Current Path: ", oldCurrentPath);

        // Set the current path to the configuration folder (to allow relative paths)
        fs::current_path(configFolder);

        const auto isRunningOnAlpine = IsRunningOnAlpine();
        const auto currentOsArch = GetCurrentOsArch(isRunningOnAlpine);

        const std::string allOsArch[16] = {
            "win-x64", "linux-x64", "linux-musl-x64", "osx-x64",
            "win-x86", "linux-x86", "linux-musl-x86", "osx-x86",
            "win-arm64", "linux-arm64", "linux-musl-arm64", "osx-arm64",
            "win-arm", "linux-arm", "linux-musl-arm", "osx-arm",
        };

        while (t)
        {
            std::string line;
            std::getline(t, line);
            line = ::shared::Trim(line);
            if (line.length() != 0)
            {
                Log::Debug(line);

                if (line[0] == '#' || ::shared::IsEmptyOrWhitespace(line))
                {
                    continue;
                }

                std::vector<std::string> lineArray = ::shared::Split(line, ';');
                if (lineArray.size() != 4)
                {
                    Log::Warn("DynamicDispatcherImpl::LoadConfiguration: Invalid line: ", line);
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
                        std::string absoluteFilepathValue = fs::absolute(filepathValue).string();
                        Log::Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Loading: ", filepathValue, " [AbsolutePath=", absoluteFilepathValue,"] (", currentOsArch, ")" );
                        if (fs::exists(absoluteFilepathValue))
                        {
                            Log::Debug("[", type, "] Creating a new DynamicInstance object");

                            ::shared::WSTRING env_key;

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

                            ::shared::WSTRING env_value = ::shared::ToWSTRING(absoluteFilepathValue);
                            Log::Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Setting environment variable: ", env_key, "=", env_value);
                            bool envVal = ::shared::SetEnvironmentValue(env_key, env_value);
                            Log::Debug("DynamicDispatcherImpl::LoadConfiguration: [", type, "] SetEnvironmentValue result: ", envVal);
                        }
                        else
                        {
                            Log::Warn("DynamicDispatcherImpl::LoadConfiguration: [", type, "] Dynamic library for '", absoluteFilepathValue,
                                 "' cannot be loaded, file doesn't exist.");
                        }
                    }
                    else
                    {
                        const std::string* findRes = std::find(std::begin(allOsArch), std::end(allOsArch), osArchValue);
                        if (findRes == std::end(allOsArch))
                        {
                            Log::Warn("DynamicDispatcherImpl::LoadConfiguration: [", type, "] The OS and Architecture is invalid: ", osArchValue);
                        }
                    }
                }
                else
                {
                    Log::Warn("DynamicDispatcherImpl::LoadConfiguration: COR Profiler Type is invalid: ", type);
                }
            }
        }
        t.close();

        // Set the current path to the original one
        fs::current_path(oldCurrentPath);
    }

    HRESULT DynamicDispatcherImpl::LoadClassFactory(REFIID riid)
    {
        HRESULT GHR = S_OK;

        if (m_continuousProfilerInstance != nullptr)
        {
            HRESULT result = m_continuousProfilerInstance->LoadClassFactory(riid);
            if (FAILED(result))
            {
                Log::Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load continuous profiler class factory in: ",
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
                Log::Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load tracer class factory in: ", m_tracerInstance->GetFilePath());

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
                Log::Warn("DynamicDispatcherImpl::LoadClassFactory: Error trying to load custom class factory in: ", m_customInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_customInstance.release();
                GHR = result;
            }
        }

        return GHR;
    }

    HRESULT DynamicDispatcherImpl::LoadInstance()
    {
        HRESULT GHR = S_OK;

        if (m_continuousProfilerInstance != nullptr)
        {
            HRESULT result = m_continuousProfilerInstance->LoadInstance();
            if (FAILED(result))
            {
                Log::Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the continuous profiler instance in: ",
                     m_continuousProfilerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_continuousProfilerInstance.release();
                GHR = result;
            }
        }

        if (m_tracerInstance != nullptr)
        {
            HRESULT result = m_tracerInstance->LoadInstance();
            if (FAILED(result))
            {
                Log::Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the tracer instance in: ", m_tracerInstance->GetFilePath());

                // If we cannot load the class factory we release the instance.
                m_tracerInstance.release();
                GHR = result;
            }
        }

        if (m_customInstance != nullptr)
        {
            HRESULT result = m_customInstance->LoadInstance();
            if (FAILED(result))
            {
                Log::Warn("DynamicDispatcherImpl::LoadInstance: Error trying to load the custom instance in: ", m_customInstance->GetFilePath());

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
                Log::Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the continuous profiler DllCanUnloadNow in: ",
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
                Log::Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the tracer DllCanUnloadNow in: ", m_tracerInstance->GetFilePath());
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
                Log::Warn("DynamicDispatcherImpl::DllCanUnloadNow: Error calling the custom DllCanUnloadNow in: ", m_customInstance->GetFilePath());
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
