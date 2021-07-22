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
        std::vector<std::unique_ptr<DynamicInstance>> m_instances;

    public:
        DynamicDispatcher();
        void Add(std::unique_ptr<DynamicInstance>& instance);
        void LoadConfiguration(std::string configFilePath);
        HRESULT LoadClassFactory(REFIID riid);
        HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid);
        HRESULT STDMETHODCALLTYPE DllCanUnloadNow();
        HRESULT Execute(std::function<HRESULT(ICorProfilerCallback10*)> func);
        std::unique_ptr<DynamicInstance>* GetInstances();
        size_t GetLength();
    };

} // namespace datadog::shared::nativeloader