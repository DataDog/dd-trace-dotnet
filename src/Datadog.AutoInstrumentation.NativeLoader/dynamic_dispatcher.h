#pragma once
#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <vector>

#include "dynamic_instance.h"
#include "string.h"

namespace datadog::shared::nativeloader
{
    //
    // DynamicDispatcher base class
    //
    class DynamicDispatcher
    {
    public:
        virtual void LoadConfiguration(std::string configFilePath) = 0;
        virtual HRESULT LoadClassFactory(REFIID riid) = 0;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) = 0;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() = 0;
        virtual DynamicInstance* GetContinuousProfilerInstance() = 0;
        virtual DynamicInstance* GetTracerInstance() = 0;
        virtual DynamicInstance* GetCustomInstance() = 0;
    };

    //
    // Default implementation of the DynamicDispatcher
    //
    class DynamicDispatcherImpl : public DynamicDispatcher
    {
    protected:
        std::unique_ptr<DynamicInstance> m_continuousProfilerInstance;
        std::unique_ptr<DynamicInstance> m_tracerInstance;
        std::unique_ptr<DynamicInstance> m_customInstance;

    public:
        DynamicDispatcherImpl();
        virtual void LoadConfiguration(std::string configFilePath) override;
        virtual HRESULT LoadClassFactory(REFIID riid) override;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) override;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() override;
        virtual DynamicInstance* GetContinuousProfilerInstance() override;
        virtual DynamicInstance* GetTracerInstance() override;
        virtual DynamicInstance* GetCustomInstance() override;
    };

} // namespace datadog::shared::nativeloader