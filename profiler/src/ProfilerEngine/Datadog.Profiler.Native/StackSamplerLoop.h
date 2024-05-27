// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <thread>
#ifdef _WINDOWS
#include <atlcomcli.h>
#endif

#include <memory>
#include <unordered_map>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "ManagedThreadInfo.h"
#include "ICollector.h"
#include "ServiceBase.h"
#include "RawCpuSample.h"
#include "RawWallTimeSample.h"
#include "MetricsRegistry.h"
#include "MeanMaxMetric.h"
#include "shared/src/native-src/string.h"

// forward declarations
class StackFramesCollectorBase;
class StackSnapshotResultBuffer;
class StackSamplerLoopManager;
class IThreadsCpuManager;
class IManagedThreadList;
class IConfiguration;


typedef enum
{
    WallTime,
    CpuTime
} PROFILING_TYPE;

class StackSamplerLoop : public ServiceBase
{
    friend StackSamplerLoopManager;

public:
    StackSamplerLoop(
        ICorProfilerInfo4* pCorProfilerInfo,
        IConfiguration* pConfiguration,
        StackFramesCollectorBase* pStackFramesCollector,
        StackSamplerLoopManager* pManager,
        IThreadsCpuManager* pThreadsCpuManager,
        IManagedThreadList* pManagedThreadList,
        IManagedThreadList* pCodeHotspotThreadList,
        ICollector<RawWallTimeSample>* pWallTimeCollector,
        ICollector<RawCpuSample>* pCpuTimeCollector,
        MetricsRegistry& metricsRegistry
        );
    ~StackSamplerLoop();
    StackSamplerLoop(StackSamplerLoop const&) = delete;
    StackSamplerLoop& operator=(StackSamplerLoop const&) = delete;

    // Inherited via IService
    const char* GetName() override;

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    StackFramesCollectorBase* _pStackFramesCollector;
    StackSamplerLoopManager* _pManager;
    IConfiguration* _pConfiguration;
    IThreadsCpuManager* _pThreadsCpuManager;
    IManagedThreadList* _pManagedThreadList;
    IManagedThreadList* _pCodeHotspotsThreadList;
    ICollector<RawWallTimeSample>* _pWallTimeCollector;
    ICollector<RawCpuSample>* _pCpuTimeCollector;

    std::unique_ptr<std::thread> _pLoopThread;
    DWORD _loopThreadOsId;
    volatile bool _shutdownRequested = false;
    std::shared_ptr<ManagedThreadInfo> _targetThread;
    uint32_t _iteratorWallTime;
    uint32_t _iteratorCpuTime;
    uint32_t _iteratorCodeHotspot;
    int32_t _walltimeThreadsThreshold;
    int32_t _cpuThreadsThreshold;
    int32_t _codeHotspotsThreadsThreshold;

private:
    std::chrono::nanoseconds _samplingPeriod;
    uint32_t _nbCores;
    bool _isWalltimeEnabled;
    bool _isCpuEnabled;
    bool _areInternalMetricsEnabled;
    std::shared_ptr<MeanMaxMetric> _walltimeDurationMetric;
    std::shared_ptr<MeanMaxMetric> _cpuDurationMetric;

private:
    void MainLoop();
    void MainLoopIteration();
    void CpuProfilingIteration();
    void WalltimeProfilingIteration();
    void CodeHotspotIteration();
    void CollectOneThreadStackSample(std::shared_ptr<ManagedThreadInfo>& pThreadInfo,
                                     int64_t thisSampleTimestampNanosecs,
                                     int64_t duration,
                                     PROFILING_TYPE profilingType);
    int64_t ComputeWallTime(int64_t currentTimestampNs, int64_t prevTimestampNs);
    static void UpdateSnapshotInfos(StackSnapshotResultBuffer* pStackSnapshotResult, int64_t representedDurationNanosecs, time_t currentUnixTimestamp);
    void UpdateStatistics(HRESULT hrCollectStack, std::size_t countCollectedStackFrames);
    static time_t GetCurrentTimestamp();
    void PersistStackSnapshotResults(StackSnapshotResultBuffer* pSnapshotResult,
                                     std::shared_ptr<ManagedThreadInfo>& pThreadInfo,
                                     PROFILING_TYPE profilingType);

    bool StartImpl() override;
    bool StopImpl() override;
};
