#include "dynamic_instance.h"

#include "guid.h"
#include "logging.h"
#include "pal.h"

namespace datadog::shared::nativeloader
{

    // ************************************************************************

    //
    // protected
    //

    HRESULT DynamicInstanceImpl::EnsureDynamicLibraryIsLoaded()
    {
        if (!m_loaded)
        {
            m_instance = LoadDynamicLibrary(m_filepath);
            m_loaded = true;
        }

        return m_instance != nullptr ? S_OK : E_FAIL;
    }

    HRESULT DynamicInstanceImpl::DllGetClassObject(REFIID riid, LPVOID* ppv)
    {
        // Check if the library is loaded
        if (FAILED(EnsureDynamicLibraryIsLoaded()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_dllGetClassObject == nullptr)
        {
            m_dllGetClassObject = (DllGetClassObjectDelegate_t) GetExternalFunction(m_instance, "DllGetClassObject");
        }

        // If we have the function pointer we call the function
        if (m_dllGetClassObject != nullptr)
        {
            return m_dllGetClassObject.load()(m_clsid, riid, ppv);
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    //
    // public
    //

    DynamicInstanceImpl::DynamicInstanceImpl(std::string filePath, std::string clsid)
    {
        m_filepath = filePath;
        m_clsid = guid_parse::make_guid(clsid);
        m_loaded = false;
        m_instance = nullptr;
        m_dllGetClassObject = nullptr;
        m_dllCanUnloadNow = nullptr;
        m_classFactory = nullptr;
        m_corProfilerCallback = nullptr;
    }

    DynamicInstanceImpl::~DynamicInstanceImpl()
    {
        m_corProfilerCallback = nullptr;
        m_classFactory = nullptr;
        m_dllCanUnloadNow = nullptr;
        m_dllGetClassObject = nullptr;
        m_loaded = false;

        if (m_instance != nullptr)
        {
            if (!FreeDynamicLibrary(m_instance))
            {
                Warn("DynamicInstanceImpl::~DynamicInstanceImpl: Error unloading: ", m_filepath, " dynamic library.");
            }
            m_instance = nullptr;
        }
    }

    HRESULT DynamicInstanceImpl::LoadClassFactory(REFIID riid)
    {
        LPVOID ppv;
        HRESULT res = DllGetClassObject(riid, &ppv);
        if (SUCCEEDED(res))
        {
            m_classFactory = static_cast<IClassFactory*>(ppv);
        }
        else
        {
            Warn("DynamicInstanceImpl::LoadClassFactory: Error getting IClassFactory from: ", m_filepath);
        }

        Debug("DynamicInstanceImpl::LoadClassFactory: ", res);
        return res;
    }

    HRESULT DynamicInstanceImpl::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        Debug("DynamicInstanceImpl::LoadInstance");

        // Check if the class factory instance is loaded.
        if (m_classFactory == nullptr)
        {
            return E_FAIL;
        }

        // Creates the profiler callback instance from the class factory
        Debug("DynamicInstanceImpl::LoadInstance: m_classFactory: ", HexStr(m_classFactory, sizeof(IClassFactory*)));
        HRESULT res =
            m_classFactory->CreateInstance(nullptr, __uuidof(ICorProfilerCallback10), (void**) &m_corProfilerCallback);
        if (FAILED(res))
        {
            m_corProfilerCallback = nullptr;
            Warn("DynamicInstanceImpl::LoadInstance: Error getting ICorProfilerCallback10 from: ", m_filepath);
        }

        Debug("DynamicInstanceImpl::LoadInstance: ", res);
        return res;
    }

    HRESULT STDMETHODCALLTYPE DynamicInstanceImpl::DllCanUnloadNow()
    {
        // Check if the library is loaded
        if (FAILED(EnsureDynamicLibraryIsLoaded()))
        {
            return E_FAIL;
        }

        // Check if the function pointer needs to be loaded
        if (m_dllCanUnloadNow == nullptr)
        {
            m_dllCanUnloadNow = (DllCanUnloadNowDelegate_t) GetExternalFunction(m_instance, "DllCanUnloadNow");
        }

        // If we have the function pointer we call the function
        if (m_dllCanUnloadNow != nullptr)
        {
            return m_dllCanUnloadNow.load()();
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    ICorProfilerCallback10* DynamicInstanceImpl::GetProfilerCallback()
    {
        return m_corProfilerCallback;
    }

    std::string DynamicInstanceImpl::GetFilePath()
    {
        return m_filepath;
    }

} // namespace datadog::shared::nativeloader