// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"

#include "MetricsRegistry.h"
#include "StackFramesCollectorBase.h"

#include <chrono>
#include <tuple>

// forward declarations
namespace shared {
struct LoaderResourceMonikerIDs;
}
class IConfiguration;
class IThreadInfo;
class IEtwEventsManager;
class IAllocationsListener;
class IContentionListener;
class IGCSuspensionsListener;
class CallstackProvider;

// Those functions must be defined in the main projects (Linux and Windows)
// Here are forward declarations to avoid hard coupling
namespace OsSpecificApi
{
    std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(
        ICorProfilerInfo4* pCorProfilerInfo,
        IConfiguration const* pConfiguration,
        CallstackProvider* callstackProvider,
        MetricsRegistry& metricsRegistry);

    std::chrono::milliseconds GetThreadCpuTime(IThreadInfo* pThreadInfo);

    //    isRunning,        cpu time          , failed 
    std::tuple<bool, std::chrono::milliseconds, bool> IsRunning(IThreadInfo* pThreadInfo);

    int32_t GetProcessorCount();

    std::vector<std::shared_ptr<IThreadInfo>> GetProcessThreads();

    std::pair<DWORD, std::string> GetLastErrorMessage();

    std::string GetProcessStartTime();

    std::unique_ptr<IEtwEventsManager> CreateEtwEventsManager(
        IAllocationsListener* pAllocationListener,
        IContentionListener* pContentionListener,
        IGCSuspensionsListener* pGCSuspensionsListener,
        IConfiguration* pConfiguration
        );

    double GetProcessLifetime();
 } // namespace OsSpecificApi
