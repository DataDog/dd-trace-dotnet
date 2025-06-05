#pragma once
#include <atomic>
#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <vector>
#include <string>

#include "../../../shared/src/native-src/dynamic_com_library.h"

namespace datadog::shared::nativeloader
{
    const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

    //
    // IDynamicInstance interface
    //
    class IDynamicInstance
    {
    public:
        virtual ~IDynamicInstance() = default;
        virtual HRESULT LoadClassFactory(REFIID riid) = 0;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) = 0;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() = 0;
        virtual ICorProfilerCallback10* GetProfilerCallback() = 0;
        virtual std::string GetFilePath() = 0;
    };

    //
    // Default implementation of the IDynamicInstance
    //
    class DynamicInstanceImpl : public IDynamicInstance
    {
    // protected for testing purpose only
    protected:
        IID m_clsid = IID_IUnknown;
        bool m_loaded;
        IClassFactory* m_classFactory;
        ICorProfilerCallback10* m_corProfilerCallback;

    public:
        DynamicInstanceImpl(const std::string& filePath, const std::string& clsid);
        ~DynamicInstanceImpl() override;
        HRESULT LoadClassFactory(REFIID riid) override;
        HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) override;
        HRESULT STDMETHODCALLTYPE DllCanUnloadNow() override;
        ICorProfilerCallback10* GetProfilerCallback() override;
        std::string GetFilePath() override;

    private:
        DynamicCOMLibrary m_mainLibrary;
    };

} // namespace datadog::shared::nativeloader