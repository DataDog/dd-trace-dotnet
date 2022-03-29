// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"

std::atomic<std::uint32_t> ManagedThreadInfo::s_nextProfilerThreadInfoId{1};

std::uint32_t ManagedThreadInfo::GenerateProfilerThreadInfoId(void)
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

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId) :
    ManagedThreadInfo(clrThreadId, 0, static_cast<HANDLE>(0), const_cast<shared::WSTRING*>(&ResolvedSymbolsCache::UnknownThreadName))
{
}

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING* pThreadName) :
    _profilerThreadInfoId{GenerateProfilerThreadInfoId()},
    _clrThreadId(clrThreadId),
    _osThreadId(osThreadId),
    _osThreadHandle(osThreadHandle),
    _pThreadName(pThreadName),
    _lastSampleHighPrecisionTimestampNanoseconds{0},
    _lastKnownSampleUnixTimeUtc{0},
    _highPrecisionNanosecsAtLastUnixTimeUpdate{0},
    _snapshotsPerformedSuccessCount{0},
    _snapshotsPerformedFailureCount{0},
    _deadlockTotalCount{0},
    _deadlockInPeriodCount{0},
    _deadlockDetectionPeriod{0},
    _stackWalkLock(1),
    _isThreadDestroyed{false},
    _traceContextTrackingInfo{}
{
}

ManagedThreadInfo::~ManagedThreadInfo()
{
    shared::WSTRING* pThreadName = _pThreadName;
    if (pThreadName != nullptr && pThreadName != &ResolvedSymbolsCache::UnknownThreadName)
    {
        _pThreadName = nullptr;
        delete pThreadName;
    }
}