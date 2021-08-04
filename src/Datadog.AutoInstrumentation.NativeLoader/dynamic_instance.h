#pragma once
#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <vector>

#include "string.h"

namespace datadog::shared::nativeloader
{
    const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

    typedef HRESULT(STDMETHODCALLTYPE* dllGetClassObjectPtr)(REFCLSID, REFIID, LPVOID*);
    typedef HRESULT(STDMETHODCALLTYPE* dllCanUnloadNow)();

    //
    // IDynamicInstance interface
    //
    class IDynamicInstance
    {
    public:
        virtual HRESULT LoadClassFactory(REFIID riid) = 0;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) = 0;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() = 0;
        virtual ICorProfilerCallback10* GetProfilerCallback() = 0;
        virtual std::string GetFilePath() = 0;
        virtual std::string GetClsId() = 0;
    };

    //
    // Default implementation of the IDynamicInstance
    //
    class DynamicInstanceImpl : public IDynamicInstance
    {
    protected:
        std::string m_filepath;
        std::string m_clsid_string;
        IID m_clsid = IID_IUnknown;
        bool m_loaded;
        void* m_instance;
        dllGetClassObjectPtr m_getClassObjectPtr;
        dllCanUnloadNow m_canUnloadNow;
        IClassFactory* m_classFactory;
        ICorProfilerCallback10* m_corProfilerCallback;

        HRESULT EnsureDynamicLibraryIsLoaded();
        HRESULT DllGetClassObject(REFIID riid, LPVOID* ppv);

    public:
        DynamicInstanceImpl(std::string filePath, std::string clsid);
        ~DynamicInstanceImpl();
        virtual HRESULT LoadClassFactory(REFIID riid) override;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) override;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() override;
        virtual ICorProfilerCallback10* GetProfilerCallback() override;
        virtual std::string GetFilePath() override;
        virtual std::string GetClsId() override;
    };

} // namespace datadog::shared::nativeloader