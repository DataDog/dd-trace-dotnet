#pragma once
#include "pal.h"

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

    void EnsureInstance()
    {
        if (!m_loaded)
        {
            m_instance = datadog::nativeloader::LoadDynamicLibrary(m_filepath);
        }
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

    HRESULT DllGetClassObject(REFIID riid, LPVOID* ppv)
    {
        EnsureInstance();

        if (m_getClassObjectPtr == nullptr)
        {
            m_getClassObjectPtr = (dllGetClassObjectPtr) datadog::nativeloader::GetExternalFunction(m_instance, "DllGetClassObject");
        }

        return m_getClassObjectPtr(m_clsid, riid, ppv);
    }

    void LoadClassFactory(REFIID riid)
    {
        LPVOID ppv;
        if (SUCCEEDED(DllGetClassObject(riid, &ppv)))
        {
            m_classFactory = (IClassFactory*) ppv;
        }
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