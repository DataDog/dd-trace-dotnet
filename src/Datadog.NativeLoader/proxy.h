#pragma once

#include <corhlpr.h>
#include <corprof.h>
#include "string.h"

namespace datadog
{
namespace nativeloader
{
    const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

    typedef HRESULT(STDMETHODCALLTYPE* dllGetClassObjectPtr)(REFCLSID, REFIID, LPVOID*);
    typedef HRESULT(STDMETHODCALLTYPE* dllCanUnloadNow)();

    class DynamicInstance
    {
    private:
        std::string m_filepath;
        IID m_clsid = IID_IUnknown;
        bool m_loaded;
        HINSTANCE m_instance;
        dllGetClassObjectPtr m_getClassObjectPtr;
        dllCanUnloadNow m_canUnloadNow;
        IClassFactory* m_classFactory;
        ICorProfilerCallback10* m_corProfilerCallback;

        void EnsureDynamicLibrary();
        HRESULT DllGetClassObject(REFIID riid, LPVOID* ppv);

    public:
        DynamicInstance(std::string filePath, REFCLSID clsid);

        HRESULT LoadClassFactory(REFIID riid);
        HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid);
        HRESULT STDMETHODCALLTYPE DllCanUnloadNow();
        ICorProfilerCallback10* GetProfilerCallback();
    };

} // namespace nativeloader
} // namespace datadog