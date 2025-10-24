// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"

#include <chrono>

using namespace std::chrono_literals;

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
    _threadName(std::move(pThreadName)),
    _lastSampleHighPrecisionTimestamp{0ns},
    _cpuConsumption{0ms},
    _timestamp{0ns},
    _deadlockTotalCount{0},
    _deadlockInPeriodCount{0},
    _deadlockDetectionPeriod{0},
    _isThreadDestroyed{false},
    _traceContext{},
#ifdef LINUX
    _sharedMemoryArea{nullptr},
    _timerId{-1},
#endif
    _info{pCorProfilerInfo},
    _blockingThreadId{0},
    _waitStartTimestamp{0ns},
    _contentionType{ContentionType::Unknown}
{
}
