// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"

std::atomic<std::uint32_t> ManagedThreadInfo::s_nextProfilerThreadInfoId{1};
thread_local std::shared_ptr<ManagedThreadInfo> ManagedThreadInfo::CurrentThreadInfo{nullptr};

std::uint32_t ManagedThreadInfo::GenerateProfilerThreadInfoId()
{
    std::uint32_t newId = s_nextProfilerThreadInfoId.fetch_add(1);
    while (newId >= MaxProfilerThreadInfoId)
    {
        newId = s_nextProfilerThreadInfoId.compare_exchange_strong(newId, 1)
                    ? 1
                    : s_nextProfilerThreadInfoId.fetch_add(1);
    }

    return newId;
}

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId, ICorProfilerInfo4* pCorProfilerInfo) :
    ManagedThreadInfo(clrThreadId,pCorProfilerInfo, 0, static_cast<HANDLE>(0), shared::WSTRING())
{
}

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId, ICorProfilerInfo4* pCorProfilerInfo, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING pThreadName) :
    _profilerThreadInfoId{GenerateProfilerThreadInfoId()},
    _clrThreadId(clrThreadId),
    _osThreadId(osThreadId),
    _osThreadHandle(osThreadHandle),
    _pThreadName(std::move(pThreadName)),
    _lastSampleHighPrecisionTimestampNanoseconds{0},
    _cpuConsumptionMilliseconds{0},
    _timestamp{0},
    _lastKnownSampleUnixTimeUtc{0},
    _highPrecisionNanosecsAtLastUnixTimeUpdate{0},
    _snapshotsPerformedSuccessCount{0},
    _snapshotsPerformedFailureCount{0},
    _deadlockTotalCount{0},
    _deadlockInPeriodCount{0},
    _deadlockDetectionPeriod{0},
    _isThreadDestroyed{false},
    _traceContext{},
#ifdef LINUX
    _sharedMemoryArea{nullptr},
#endif
    _info{pCorProfilerInfo},
    _blockingThreadId{0}
{
}
