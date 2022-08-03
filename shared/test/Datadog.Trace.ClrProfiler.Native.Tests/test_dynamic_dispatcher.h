#pragma once
#include "../../src/Datadog.Trace.ClrProfiler.Native/dynamic_dispatcher.h"

using namespace datadog::shared::nativeloader;

class TestDynamicDispatcherImpl : public DynamicDispatcherImpl
{
public:
    void LoadConfiguration(std::filesystem::path&& configFilePath) override
    {
        // empty
    }

    void SetContinuousProfilerInstance(std::unique_ptr<IDynamicInstance> dynInstance)
    {
        m_continuousProfilerInstance = std::move(dynInstance);
    }

    void SetTracerInstance(std::unique_ptr<IDynamicInstance> dynInstance)
    {
        m_tracerInstance = std::move(dynInstance);
    }

    void SetCustomInstance(std::unique_ptr<IDynamicInstance> dynInstance)
    {
        m_customInstance = std::move(dynInstance);
    }
};