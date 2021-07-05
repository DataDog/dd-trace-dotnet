#include "proxy.h"

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

    void DynamicInstance::EnsureDynamicLibrary()
    {
        if (!m_loaded)
        {
            m_instance = LoadDynamicLibrary(m_filepath);
            m_loaded = true;
        }
    }

    HRESULT DynamicInstance::DllGetClassObject(REFIID riid, LPVOID* ppv)
    {
        EnsureDynamicLibrary();
        if (m_getClassObjectPtr == nullptr)
        {
            m_getClassObjectPtr = (dllGetClassObjectPtr) GetExternalFunction(m_instance, "DllGetClassObject");
        }
        return m_getClassObjectPtr(m_clsid, riid, ppv);
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
        Debug("m_clasFactory: ", HexStr(m_classFactory, sizeof(IClassFactory*)));

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
        EnsureDynamicLibrary();
        if (m_canUnloadNow == nullptr)
        {
            m_canUnloadNow = (dllCanUnloadNow) GetExternalFunction(m_instance, "DllCanUnloadNow");
        }
        return m_canUnloadNow();
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
        m_instances = std::vector<DynamicInstance*>();
    }

    void DynamicDispatcher::Add(DynamicInstance* instance)
    {
        m_instances.push_back(instance);
    }

    HRESULT DynamicDispatcher::LoadClassFactory(REFIID riid)
    {
        HRESULT result = S_OK;
        for (DynamicInstance* dynIns : m_instances)
        {
            HRESULT localResult = dynIns->LoadClassFactory(riid);
            if (FAILED(localResult))
            {
                result = localResult;
            }
        }
        return result;
    }

    HRESULT DynamicDispatcher::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        HRESULT result = S_OK;
        for (DynamicInstance* dynIns : m_instances)
        {
            HRESULT localResult = dynIns->LoadInstance(pUnkOuter, riid);
            if (FAILED(localResult))
            {
                result = localResult;
            }
        }
        return result;
    }

    HRESULT STDMETHODCALLTYPE DynamicDispatcher::DllCanUnloadNow()
    {
        HRESULT result = S_OK;
        for (DynamicInstance* dynIns : m_instances)
        {
            HRESULT localResult = dynIns->DllCanUnloadNow();
            if (FAILED(localResult))
            {
                result = localResult;
            }
        }
        return result;
    }

    HRESULT DynamicDispatcher::Execute(std::function<HRESULT(ICorProfilerCallback10*)> func)
    {
        HRESULT result = S_OK;
        for (DynamicInstance* dynIns : m_instances)
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

} // namespace nativeloader
} // namespace datadog