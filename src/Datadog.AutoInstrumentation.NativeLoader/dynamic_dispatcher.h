#pragma once
#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <vector>

#include "dynamic_instance.h"
#include "string.h"

namespace datadog::shared::nativeloader
{
    class DynamicDispatcher
    {
    private:
        std::unique_ptr<DynamicInstance> m_continuousProfilerInstance;
        std::unique_ptr<DynamicInstance> m_tracerInstance;
        std::unique_ptr<DynamicInstance> m_customInstance;

    public:
        DynamicDispatcher();
        void LoadConfiguration(std::string configFilePath);
        HRESULT LoadClassFactory(REFIID riid);
        HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid);
        HRESULT STDMETHODCALLTYPE DllCanUnloadNow();
        DynamicInstance* GetContinuousProfilerInstance();
        DynamicInstance* GetTracerInstance();
        DynamicInstance* GetCustomInstance();
    };

} // namespace datadog::shared::nativeloader