// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "cor.h"
#include "corprof.h"

#include "RefCountingObject.h"
#include "ResolvedSymbolsCache.h"
#include "Semaphore.h"
#include "shared/src/native-src/string.h"


struct TraceContextTrackingInfo
{
public:
    static constexpr int MinFieldAlignRequirement = 8;
    static constexpr int FieldAlignRequirement = (MinFieldAlignRequirement >= alignof(std::uint64_t)) ? MinFieldAlignRequirement : alignof(std::uint64_t);

    inline TraceContextTrackingInfo() :
        _currentTraceId{0},
        _currentSpanId{0}
    {
    }

    inline void GetFieldPointers(std::uint64_t** ppCurrentTraceId, std::uint64_t** ppCurrentSpanId)
    {
        if (ppCurrentTraceId != nullptr)
        {
            *ppCurrentTraceId = &_currentTraceId;
        }

        if (ppCurrentSpanId != nullptr)
        {
            *ppCurrentSpanId = &_currentSpanId;
        }
    }

    inline void Set(std::uint64_t currentTraceId, std::uint64_t currentSpanId)
    {
        _currentTraceId = currentTraceId;
        _currentSpanId = currentSpanId;
    }

    alignas(FieldAlignRequirement) std::uint64_t _currentTraceId;
    alignas(FieldAlignRequirement) std::uint64_t _currentSpanId;
};

struct ManagedThreadInfo : public RefCountingObject
{
private:
    ManagedThreadInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING* pThreadName);
    static std::uint32_t GenerateProfilerThreadInfoId(void);

public:
    explicit ManagedThreadInfo(ThreadID clrThreadId);
    ~ManagedThreadInfo() override;

    inline std::uint32_t GetProfilerThreadInfoId(void) const;

    inline ThreadID GetClrThreadId(void) const;

    inline DWORD GetOsThreadId(void) const;
    inline HANDLE GetOsThreadHandle(void) const;
    inline void SetOsInfo(DWORD osThreadId, HANDLE osThreadHandle);

    inline const shared::WSTRING& GetThreadName(void) const;
    inline void SetThreadName(shared::WSTRING* pThreadName);

    inline std::uint64_t GetLastSampleHighPrecisionTimestampNanoseconds(void) const;
    inline std::uint64_t SetLastSampleHighPrecisionTimestampNanoseconds(std::uint64_t value);

    inline void GetLastKnownSampleUnixTimestamp(std::uint64_t* realUnixTimeUtc, std::int64_t* highPrecisionNanosecsAtLastUnixTimeUpdate) const;
    inline void SetLastKnownSampleUnixTimestamp(std::uint64_t realUnixTimeUtc, std::int64_t highPrecisionNanosecsAtThisUnixTimeUpdate);

    inline std::uint64_t GetSnapshotsPerformedSuccessCount() const;
    inline std::uint64_t GetSnapshotsPerformedFailureCount() const;
    inline std::uint64_t IncSnapshotsPerformedCount(const bool isStackSnapshotSuccessful);

    inline void GetOrResetDeadlocksCount(std::uint64_t deadlocksAggregationPeriodIndex,
                                         std::uint64_t* pPrevCount,
                                         std::uint64_t* pNewCount);
    inline void IncDeadlocksCount(void);
    inline void GetDeadlocksCount(std::uint64_t* deadlockDetectionsTotalCount,
                                  std::uint64_t* deadlockDetectionsInAggregationPeriodCount,
                                  std::uint64_t* usedDeadlockDetectionsAggregationPeriodIndex) const;

    inline Semaphore& GetStackWalkLock();

    inline bool IsThreadDestroyed(void);
    inline bool IsDestroyed();
    inline void SetThreadDestroyed();

    inline void GetTraceContextInfoFieldPointers(std::uint64_t** ppCurrentTraceId, std::uint64_t** ppCurrentSpanId);
    inline std::uint64_t GetTraceContextTraceId(void) const;
    inline std::uint64_t GetTraceContextSpanId(void) const;

private:
    static constexpr std::uint32_t MaxProfilerThreadInfoId = 0xFFFFFF; // = 16,777,215
    static std::atomic<std::uint32_t> s_nextProfilerThreadInfoId;

    std::uint32_t _profilerThreadInfoId;
    ThreadID _clrThreadId;
    DWORD _osThreadId;
    HANDLE _osThreadHandle;
    shared::WSTRING* _pThreadName;

    std::uint64_t _lastSampleHighPrecisionTimestampNanoseconds;
    std::uint64_t _lastKnownSampleUnixTimeUtc;
    std::int64_t _highPrecisionNanosecsAtLastUnixTimeUpdate;

    std::uint64_t _snapshotsPerformedSuccessCount;
    std::uint64_t _snapshotsPerformedFailureCount;

    std::uint64_t _deadlockTotalCount;
    std::uint64_t _deadlockInPeriodCount;
    std::uint64_t _deadlockDetectionPeriod;

    Semaphore _stackWalkLock;
    bool _isThreadDestroyed;
    TraceContextTrackingInfo _traceContextTrackingInfo;
};

std::uint32_t ManagedThreadInfo::GetProfilerThreadInfoId(void) const
{
    return _profilerThreadInfoId;
}

inline ThreadID ManagedThreadInfo::GetClrThreadId(void) const
{
    return _clrThreadId;
}

inline DWORD ManagedThreadInfo::GetOsThreadId(void) const
{
    return _osThreadId;
}

inline HANDLE ManagedThreadInfo::GetOsThreadHandle(void) const
{
    return _osThreadHandle;
}

inline void ManagedThreadInfo::SetOsInfo(DWORD osThreadId, HANDLE osThreadHandle)
{
    _osThreadId = osThreadId;
    _osThreadHandle = osThreadHandle;
}

inline const shared::WSTRING& ManagedThreadInfo::GetThreadName(void) const
{
    return *_pThreadName;
}

inline void ManagedThreadInfo::SetThreadName(shared::WSTRING* pThreadName)
{
    if (pThreadName == nullptr)
    {
        pThreadName = const_cast<shared::WSTRING*>(&ResolvedSymbolsCache::UnknownThreadName);
    }

    shared::WSTRING* prevPThreadName = _pThreadName;
    _pThreadName = pThreadName;

    if (prevPThreadName != nullptr && !ResolvedSymbolsCache::IsSharedStaticConstant(prevPThreadName))
    {
        delete prevPThreadName;
    }
}

inline std::uint64_t ManagedThreadInfo::GetLastSampleHighPrecisionTimestampNanoseconds(void) const
{
    return _lastSampleHighPrecisionTimestampNanoseconds;
}

inline std::uint64_t ManagedThreadInfo::SetLastSampleHighPrecisionTimestampNanoseconds(std::uint64_t value)
{
    std::uint64_t prevValue = _lastSampleHighPrecisionTimestampNanoseconds;
    _lastSampleHighPrecisionTimestampNanoseconds = value;
    return prevValue;
}

inline void ManagedThreadInfo::GetLastKnownSampleUnixTimestamp(std::uint64_t* realUnixTimeUtc, std::int64_t* highPrecisionNanosecsAtLastUnixTimeUpdate) const
{
    if (realUnixTimeUtc != nullptr)
    {
        *realUnixTimeUtc = _lastKnownSampleUnixTimeUtc;
    }

    if (highPrecisionNanosecsAtLastUnixTimeUpdate != nullptr)
    {
        *highPrecisionNanosecsAtLastUnixTimeUpdate = _highPrecisionNanosecsAtLastUnixTimeUpdate;
    }
}

inline void ManagedThreadInfo::SetLastKnownSampleUnixTimestamp(std::uint64_t realUnixTimeUtc, std::int64_t highPrecisionNanosecsAtThisUnixTimeUpdate)
{
    _lastKnownSampleUnixTimeUtc = realUnixTimeUtc;
    _highPrecisionNanosecsAtLastUnixTimeUpdate = highPrecisionNanosecsAtThisUnixTimeUpdate;
}

inline std::uint64_t ManagedThreadInfo::GetSnapshotsPerformedSuccessCount() const
{
    return _snapshotsPerformedSuccessCount;
}

inline std::uint64_t ManagedThreadInfo::GetSnapshotsPerformedFailureCount() const
{
    return _snapshotsPerformedFailureCount;
}

inline std::uint64_t ManagedThreadInfo::IncSnapshotsPerformedCount(const bool isStackSnapshotSuccessful)
{
    if (isStackSnapshotSuccessful)
    {
        return _snapshotsPerformedSuccessCount++;
    }
    else
    {
        return _snapshotsPerformedFailureCount++;
    }
}

inline void ManagedThreadInfo::GetOrResetDeadlocksCount(
    std::uint64_t deadlockDetectionPeriod,
    std::uint64_t* pPrevCount,
    std::uint64_t* pNewCount)
{
    *pPrevCount = _deadlockInPeriodCount;

    if (_deadlockDetectionPeriod != deadlockDetectionPeriod)
    {
        _deadlockInPeriodCount = 0;
        _deadlockDetectionPeriod = deadlockDetectionPeriod;
    }

    *pNewCount = _deadlockInPeriodCount;
}

inline void ManagedThreadInfo::IncDeadlocksCount(void)
{
    _deadlockTotalCount++;
    _deadlockInPeriodCount++;
}

inline void ManagedThreadInfo::GetDeadlocksCount(std::uint64_t* deadlockTotalCount,
                                                 std::uint64_t* deadlockInPeriodCount,
                                                 std::uint64_t* deadlockDetectionPeriod) const
{
    if (nullptr != deadlockTotalCount)
    {
        *deadlockTotalCount = _deadlockTotalCount;
    }

    if (nullptr != deadlockInPeriodCount)
    {
        *deadlockInPeriodCount = _deadlockInPeriodCount;
    }

    if (nullptr != deadlockDetectionPeriod)
    {
        *deadlockDetectionPeriod = _deadlockDetectionPeriod;
    }
}

inline Semaphore& ManagedThreadInfo::GetStackWalkLock()
{
    return _stackWalkLock;
}

// TODO: this does not seem to be needed
inline bool ManagedThreadInfo::IsThreadDestroyed(void)
{
    SemaphoreScope guardedLock(_stackWalkLock);
    return _isThreadDestroyed;
}

// This is not synchronized and must be called under the _stackWalkLock lock
inline bool ManagedThreadInfo::IsDestroyed()
{
    return _isThreadDestroyed;
}

inline void ManagedThreadInfo::SetThreadDestroyed()
{
    SemaphoreScope guardedLock(_stackWalkLock);
    _isThreadDestroyed = true;
}

inline void ManagedThreadInfo::GetTraceContextInfoFieldPointers(std::uint64_t** ppCurrentTraceId, std::uint64_t** ppCurrentSpanId)
{
    _traceContextTrackingInfo.GetFieldPointers(ppCurrentTraceId, ppCurrentSpanId);
}

inline std::uint64_t ManagedThreadInfo::GetTraceContextTraceId(void) const
{
    return _traceContextTrackingInfo._currentTraceId;
}

inline std::uint64_t ManagedThreadInfo::GetTraceContextSpanId(void) const
{
    return _traceContextTrackingInfo._currentSpanId;
}
