// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackFramesCollectorFactory.h"

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "IConfiguration.h"
#include "CallstackProvider.h"
#include "MetricsRegistry.h"
#include "StackFramesCollectorBase.h"
#ifdef LINUX
#include "LinuxStackFramesCollector.h"
#include "ProfilerSignalManager.h"
#include "Backtrace2Unwinder.h"
#elif _WINDOWS
#include "../Datadog.Profiler.Native.Windows/Windows32BitStackFramesCollector.h"
#include "../Datadog.Profiler.Native.Windows/Windows64BitStackFramesCollector.h"
#else
#error "Unsupported platform"
#endif

StackFramesCollectorFactory::StackFramesCollectorFactory(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration const* pConfiguration, MetricsRegistry& metricsRegistry) :
    _pCorProfilerInfo(pCorProfilerInfo),
    _pConfiguration(pConfiguration),
    _metricsRegistry(metricsRegistry)
{
}

std::unique_ptr<StackFramesCollectorBase> StackFramesCollectorFactory::Create(CallstackProvider* callstackProvider)
{
#ifdef LINUX
    static auto pUnwinder = std::make_unique<Backtrace2Unwinder>();
    return std::make_unique<LinuxStackFramesCollector>(
        ProfilerSignalManager::Get(SIGUSR1), _pConfiguration, callstackProvider, _metricsRegistry, pUnwinder.get());
#elif _WINDOWS
    #ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return std::make_unique<Windows64BitStackFramesCollector>(_pCorProfilerInfo, _pConfiguration, std::move(callstackProvider));
    #else
    assert(8 * sizeof(void*) == 32);
    return std::make_unique<Windows32BitStackFramesCollector>(_pCorProfilerInfo, _pConfiguration, std::move(callstackProvider));
    #endif
#else
#error "Unsupported platform"
#endif
}