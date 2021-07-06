#include "proxy.h"

#include "guid.h"
#include "logging.h"
#include "pal.h"

namespace datadog
{
namespace nativeloader
{

    // ************************************************************************

    //
    // private
    //

    HRESULT DynamicInstance::EnsureDynamicLibrary()
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
        if (FAILED(EnsureDynamicLibrary()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_getClassObjectPtr == nullptr)
        {
            m_getClassObjectPtr = (dllGetClassObjectPtr) GetExternalFunction(m_instance, "DllGetClassObject");
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

    DynamicInstance::DynamicInstance(std::string filePath, REFCLSID clsid)
    {
        m_filepath = filePath;
        m_clsid = clsid;
        m_loaded = false;
        m_getClassObjectPtr = nullptr;
        m_canUnloadNow = nullptr;
    }

    DynamicInstance::DynamicInstance(std::string str)
    {
        size_t delimiter = str.find("=");
        m_filepath = str.substr(delimiter + 1);
        m_clsid = guid_parse::make_guid(str.substr(0, delimiter));
        m_loaded = false;
        m_getClassObjectPtr = nullptr;
        m_canUnloadNow = nullptr;
    }

    HRESULT DynamicInstance::LoadClassFactory(REFIID riid)
    {
        LPVOID ppv;
        HRESULT res = DllGetClassObject(riid, &ppv);
        if (SUCCEEDED(res))
        {
            m_classFactory = (IClassFactory*) ppv;
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
        HRESULT res = m_classFactory->CreateInstance(nullptr, __uuidof(ICorProfilerCallback10), (void**) &m_corProfilerCallback);
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
        if (FAILED(EnsureDynamicLibrary()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_canUnloadNow == nullptr)
        {
            m_canUnloadNow = (dllCanUnloadNow) GetExternalFunction(m_instance, "DllCanUnloadNow");
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
        m_instances.push_back(std::move(instance));
    }

    HRESULT DynamicDispatcher::LoadClassFactory(REFIID riid)
    {
        HRESULT result = S_OK;
        for (const auto& dynIns : m_instances)
        {
            if (dynIns != nullptr)
            {
                HRESULT localResult = dynIns->LoadClassFactory(riid);
                if (FAILED(localResult))
                {
                    result = localResult;
                }
            }
        }
        return result;
    }

    HRESULT DynamicDispatcher::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        HRESULT result = S_OK;
        for (const auto& dynIns : m_instances)
        {
            if (dynIns != nullptr)
            {
                HRESULT localResult = dynIns->LoadInstance(pUnkOuter, riid);
                if (FAILED(localResult))
                {
                    result = localResult;
                }
            }
        }
        return result;
    }

    HRESULT STDMETHODCALLTYPE DynamicDispatcher::DllCanUnloadNow()
    {
        HRESULT result = S_OK;
        for (const auto& dynIns : m_instances)
        {
            if (dynIns != nullptr)
            {
                HRESULT localResult = dynIns->DllCanUnloadNow();
                if (FAILED(localResult))
                {
                    result = localResult;
                }
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
        for (const auto& dynIns : m_instances)
        {
            if (dynIns != nullptr)
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
        }
        return result;
    }

} // namespace nativeloader
} // namespace datadog