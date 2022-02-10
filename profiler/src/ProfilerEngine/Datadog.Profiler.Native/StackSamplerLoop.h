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

#include "shared/src/native-src/string.h"

class StackFramesCollectorBase;
class StackSnapshotResultBuffer;
class StackSamplerLoopManager;

class StackSamplerLoop
{
    friend StackSamplerLoopManager;

public:
    StackSamplerLoop(ICorProfilerInfo4* pCorProfilerInfo, StackFramesCollectorBase* pStackFramesCollector, StackSamplerLoopManager* pManager);
    ~StackSamplerLoop();
    StackSamplerLoop(StackSamplerLoop const&) = delete;
    StackSamplerLoop& operator=(StackSamplerLoop const&) = delete;

    void Join();
    void RequestShutdown();

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    StackFramesCollectorBase* _pStackFramesCollector;
    StackSamplerLoopManager* _pManager;

    std::thread* _pLoopThread;
    DWORD _loopThreadOsId;
    volatile bool _shutdownRequested = false;

    ManagedThreadInfo* _targetThread;

private:
    std::unordered_map<HRESULT, std::uint64_t> _encounteredStackSnapshotHRs;
    std::unordered_map<std::uint16_t, std::uint64_t> _encounteredStackSnapshotDepths;
    std::uint64_t _totalStacksCollectedCount{0};
    std::uint64_t _lastStackSnapshotResultsStats_LogTimestampNS{0};
    std::unordered_map<shared::WSTRING, std::uint64_t> _encounteredStackCountsForDebug;

    void MainLoop(void);
    void WaitOnePeriod(void);
    void MainLoopIteration(void);
    void CollectOneThreadStackSample(ManagedThreadInfo* pThreadInfo);
    void LogEncounteredStackSnapshotResultStatistics(std::int64_t thisSampleTimestampNanosecs, bool useStdOutInsteadOfLog = false);
    void DetermineSampledStackFrameCodeKinds(StackSnapshotResultBuffer* _pStackSnapshotResult);
    void DetermineAppDomain(ThreadID threadId, StackSnapshotResultBuffer* const pStackSnapshotResult);
    std::int64_t ComputeWallTime(std::int64_t thisSampleTimestampNanosecs, std::int64_t prevSampleTimestampNanosecs);
    void UpdateSnapshotInfos(StackSnapshotResultBuffer* const pStackSnapshotResult, std::int64_t representedDurationNanosecs, time_t currentUnixTimestamp);
    void UpdateStatistics(HRESULT hrCollectStack, std::uint16_t countCollectedStackFrames);
    time_t GetCurrentTimestamp();
    void PersistStackSnapshotResults(StackSnapshotResultBuffer const* pSnapshotResult, ManagedThreadInfo* pThreadInfo);
    void PrintStackSnapshotResultsForDebug(StackSnapshotResultBuffer const* pSnapshotResult,
                                           ManagedThreadInfo* pThreadInfo,
                                           std::int64_t thisSampleTimestampNanosecs);
};
