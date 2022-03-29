#pragma once
#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <vector>
#include <memory>

#include "dynamic_instance.h"

#include "../../../shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "../../../shared/src/native-src/string.h"

namespace datadog::shared::nativeloader
{
    //
    // IDynamicDispatcher interface
    //
    class IDynamicDispatcher
    {
    public:
        virtual void LoadConfiguration(fs::path&& configFilePath) = 0;
        virtual HRESULT LoadClassFactory(REFIID riid) = 0;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) = 0;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() = 0;
        virtual IDynamicInstance* GetContinuousProfilerInstance() = 0;
        virtual IDynamicInstance* GetTracerInstance() = 0;
        virtual IDynamicInstance* GetCustomInstance() = 0;
    };

    //
    // Default implementation of the IDynamicDispatcher
    //
    class DynamicDispatcherImpl : public IDynamicDispatcher
    {
    protected:
        std::unique_ptr<IDynamicInstance> m_continuousProfilerInstance;
        std::unique_ptr<IDynamicInstance> m_tracerInstance;
        std::unique_ptr<IDynamicInstance> m_customInstance;

    public:
        DynamicDispatcherImpl();
        virtual void LoadConfiguration(fs::path&& configFilePath) override;
        virtual HRESULT LoadClassFactory(REFIID riid) override;
        virtual HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) override;
        virtual HRESULT STDMETHODCALLTYPE DllCanUnloadNow() override;
        virtual IDynamicInstance* GetContinuousProfilerInstance() override;
        virtual IDynamicInstance* GetTracerInstance() override;
        virtual IDynamicInstance* GetCustomInstance() override;
    };

} // namespace datadog::shared::nativeloader