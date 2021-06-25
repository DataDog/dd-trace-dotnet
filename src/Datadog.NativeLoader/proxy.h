#pragma once
#include "pal.h"

#include <corhlpr.h>
#include <corprof.h>
#include "logging.h"

typedef HRESULT(STDMETHODCALLTYPE* dllGetClassObjectPtr)(REFCLSID, REFIID, LPVOID*);
typedef HRESULT(STDMETHODCALLTYPE* dllCanUnloadNow)();

class DynamicInstance
{
private:
    const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
    std::string m_filepath;
    IID m_clsid = IID_IUnknown;
    bool m_loaded;
    HINSTANCE m_instance;
    dllGetClassObjectPtr m_getClassObjectPtr;
    dllCanUnloadNow m_canUnloadNow;
    IClassFactory* m_classFactory;
    LPVOID m_corProfilerCallback;

    void EnsureInstance()
    {
        if (!m_loaded)
        {
            m_instance = datadog::nativeloader::LoadDynamicLibrary(m_filepath);
        }
    }

    HRESULT DllGetClassObject(REFIID riid, LPVOID* ppv)
    {
        EnsureInstance();

        if (m_getClassObjectPtr == nullptr)
        {
            m_getClassObjectPtr =
                (dllGetClassObjectPtr) datadog::nativeloader::GetExternalFunction(m_instance, "DllGetClassObject");
        }

        return m_getClassObjectPtr(m_clsid, riid, ppv);
    }

public:
    DynamicInstance(std::string filePath, REFCLSID clsid)
    {
        m_filepath = filePath;
        m_clsid = clsid;
        m_loaded = false;
        m_getClassObjectPtr = nullptr;
        m_canUnloadNow = nullptr;
    }

    HRESULT LoadClassFactory(REFIID riid)
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

    HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        Debug("Running LoadInstance");

        HRESULT res = m_classFactory->CreateInstance(pUnkOuter, riid, &m_corProfilerCallback);
        if (FAILED(res))
        {
            m_corProfilerCallback = nullptr;
            Warn("Error getting ICorProfilerCallback10 from: ", m_filepath);
        }

        Debug("LoadInstance: ", res);
        return res;
    }

    HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
    {
        EnsureInstance();

        if (m_canUnloadNow == nullptr)
        {
            m_canUnloadNow = (dllCanUnloadNow) datadog::nativeloader::GetExternalFunction(m_instance, "DllCanUnloadNow");
        }

        return m_canUnloadNow();
    }
};