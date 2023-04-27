// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "cor.h"
#include "corprof.h"

#include "Semaphore.h"
#include "shared/src/native-src/string.h"

#include <atomic>
#include <memory>


static constexpr int32_t MinFieldAlignRequirement = 8;
static constexpr int32_t FieldAlignRequirement = (MinFieldAlignRequirement >= alignof(std::uint64_t)) ? MinFieldAlignRequirement : alignof(std::uint64_t);

struct alignas(FieldAlignRequirement) TraceContextTrackingInfo
{
public:
    std::uint64_t _writeGuard;
    std::uint64_t _currentLocalRootSpanId;
    std::uint64_t _currentSpanId;
};

struct ManagedThreadInfo
{
private:
    ManagedThreadInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING pThreadName);
    static std::uint32_t GenerateProfilerThreadInfoId();

public:
    explicit ManagedThreadInfo(ThreadID clrThreadId);
    ~ManagedThreadInfo() = default;

    inline std::uint32_t GetProfilerThreadInfoId() const;

    inline ThreadID GetClrThreadId() const;

    inline DWORD GetOsThreadId() const;
    inline HANDLE GetOsThreadHandle() const;
    inline void SetOsInfo(DWORD osThreadId, HANDLE osThreadHandle);

    inline const shared::WSTRING& GetThreadName() const;
    inline void SetThreadName(shared::WSTRING pThreadName);

    inline std::uint64_t GetLastSampleHighPrecisionTimestampNanoseconds() const;
    inline std::uint64_t SetLastSampleHighPrecisionTimestampNanoseconds(std::uint64_t value);
    inline std::uint64_t GetCpuConsumptionMilliseconds() const;
    inline std::uint64_t SetCpuConsumptionMilliseconds(std::uint64_t value, std::int64_t timestamp);
    inline std::int64_t GetCpuTimestamp() const;

    inline void GetLastKnownSampleUnixTimestamp(std::uint64_t* realUnixTimeUtc, std::int64_t* highPrecisionNanosecsAtLastUnixTimeUpdate) const;
    inline void SetLastKnownSampleUnixTimestamp(std::uint64_t realUnixTimeUtc, std::int64_t highPrecisionNanosecsAtThisUnixTimeUpdate);

    inline std::uint64_t GetSnapshotsPerformedSuccessCount() const;
    inline std::uint64_t GetSnapshotsPerformedFailureCount() const;
    inline std::uint64_t IncSnapshotsPerformedCount(bool isStackSnapshotSuccessful);

    inline void GetOrResetDeadlocksCount(std::uint64_t deadlocksAggregationPeriodIndex,
                                         std::uint64_t* pPrevCount,
                                         std::uint64_t* pNewCount);
    inline void IncDeadlocksCount();
    inline void GetDeadlocksCount(std::uint64_t* deadlockDetectionsTotalCount,
                                  std::uint64_t* deadlockDetectionsInAggregationPeriodCount,
                                  std::uint64_t* usedDeadlockDetectionsAggregationPeriodIndex) const;

    inline Semaphore& GetStackWalkLock();

    inline bool IsThreadDestroyed();
    inline bool IsDestroyed();
    inline void SetThreadDestroyed();

    inline TraceContextTrackingInfo* GetTraceContextPointer();
    inline std::uint64_t GetLocalRootSpanId() const;
    inline std::uint64_t GetSpanId() const;
    inline bool CanReadTraceContext() const;
    inline bool HasTraceContext() const;

    inline std::string GetProfileThreadId();
    inline std::string GetProfileThreadName();

private:
    inline void BuildProfileThreadId();
    inline void BuildProfileThreadName();

private:
    class ScopedHandle
    {
    public:
        explicit ScopedHandle(HANDLE hnd) :
            _handle(hnd)
            {}

        ~ScopedHandle()
        {
#ifdef _WINDOWS
            ::CloseHandle(_handle);
#endif
        }

        // Make it non copyable
        ScopedHandle(ScopedHandle&) = delete;
        ScopedHandle& operator=(ScopedHandle&) = delete;

        ScopedHandle(ScopedHandle&& other) noexcept
        {
            // set the other handle to NULL and store its value in _handle
            _handle = std::exchange(other._handle, NULL);
        }

        ScopedHandle& operator=(ScopedHandle&& other) noexcept
        {
            if (this != &other)
            {
                // set the other handle to NULL and store its value in _handle
                _handle = std::exchange(other._handle, NULL);
            }
            return *this;
        }

        operator HANDLE() const
        {
            return _handle;
        }

    private:
        HANDLE _handle;
    };

    static constexpr std::uint32_t MaxProfilerThreadInfoId = 0xFFFFFF; // = 16,777,215
    static std::atomic<std::uint32_t> s_nextProfilerThreadInfoId;

    std::uint32_t _profilerThreadInfoId;
    ThreadID _clrThreadId;
    DWORD _osThreadId;
    ScopedHandle _osThreadHandle;
    shared::WSTRING _pThreadName;

    std::uint64_t _lastSampleHighPrecisionTimestampNanoseconds;
    std::uint64_t _cpuConsumptionMilliseconds;
    std::int64_t _timestamp;
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

    //  strings to be used by samples: avoid allocations when rebuilding them over and over again
    std::string _profileThreadId;
    std::string _profileThreadName;
};



std::string ManagedThreadInfo::GetProfileThreadId()
{
    if (_profileThreadId.empty())
    {
        BuildProfileThreadId();
    }

    return _profileThreadId;
}

std::string ManagedThreadInfo::GetProfileThreadName()
{
    if (_profileThreadName.empty())
    {
        BuildProfileThreadName();
    }

    return _profileThreadName;
}

inline void ManagedThreadInfo::BuildProfileThreadId()
{
    std::stringstream builder;
    builder << "<" << std::dec << _profilerThreadInfoId << "> [#" << _osThreadId << "]";
    _profileThreadId = std::move(builder.str());
}

inline void ManagedThreadInfo::BuildProfileThreadName()
{
    std::stringstream nameBuilder;
    if (GetThreadName().empty())
    {
        nameBuilder << "Managed thread (name unknown)";
    }
    else
    {
        nameBuilder << shared::ToString(GetThreadName());
    }
    nameBuilder << " [#" << _osThreadId << "]";

    _profileThreadName = nameBuilder.str();
}

std::uint32_t ManagedThreadInfo::GetProfilerThreadInfoId() const
{
    return _profilerThreadInfoId;
}

inline ThreadID ManagedThreadInfo::GetClrThreadId() const
{
    return _clrThreadId;
}

inline DWORD ManagedThreadInfo::GetOsThreadId() const
{
    return _osThreadId;
}

inline HANDLE ManagedThreadInfo::GetOsThreadHandle() const
{
    return _osThreadHandle;
}

inline void ManagedThreadInfo::SetOsInfo(DWORD osThreadId, HANDLE osThreadHandle)
{
    _osThreadId = osThreadId;
    _osThreadHandle = ScopedHandle(osThreadHandle);

    BuildProfileThreadId();
}

inline const shared::WSTRING& ManagedThreadInfo::GetThreadName() const
{
    return _pThreadName;
}

inline void ManagedThreadInfo::SetThreadName(shared::WSTRING pThreadName)
{
    _pThreadName = std::move(pThreadName);
    BuildProfileThreadName();
}

inline std::uint64_t ManagedThreadInfo::GetLastSampleHighPrecisionTimestampNanoseconds() const
{
    return _lastSampleHighPrecisionTimestampNanoseconds;
}

inline std::uint64_t ManagedThreadInfo::SetLastSampleHighPrecisionTimestampNanoseconds(std::uint64_t value)
{
    std::uint64_t prevValue = _lastSampleHighPrecisionTimestampNanoseconds;
    _lastSampleHighPrecisionTimestampNanoseconds = value;
    return prevValue;
}

inline std::uint64_t ManagedThreadInfo::GetCpuConsumptionMilliseconds() const
{
    return _cpuConsumptionMilliseconds;
}

inline std::int64_t ManagedThreadInfo::GetCpuTimestamp() const
{
    return _timestamp;
}

inline std::uint64_t ManagedThreadInfo::SetCpuConsumptionMilliseconds(std::uint64_t value, std::int64_t timestamp)
{
    _timestamp = timestamp;

    std::uint64_t prevValue = _cpuConsumptionMilliseconds;
    _cpuConsumptionMilliseconds = value;
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

inline void ManagedThreadInfo::IncDeadlocksCount()
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
inline bool ManagedThreadInfo::IsThreadDestroyed()
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

inline TraceContextTrackingInfo* ManagedThreadInfo::GetTraceContextPointer()
{
    return &_traceContextTrackingInfo;
}

inline std::uint64_t ManagedThreadInfo::GetLocalRootSpanId() const
{
    return _traceContextTrackingInfo._currentLocalRootSpanId;
}

inline std::uint64_t ManagedThreadInfo::GetSpanId() const
{
    return _traceContextTrackingInfo._currentSpanId;
}

inline bool ManagedThreadInfo::CanReadTraceContext() const
{
    bool canReadTraceContext = _traceContextTrackingInfo._writeGuard;

    // As said in the doc, on x86 (x86_64 including) this is a compiler fence.
    // In our case, it suffices. We have to make sure that reading this field is done
    // before reading the _currentLocalRootSpanId and _currentSpandId.
    // On Arm the __sync_synchronize is generated.
    std::atomic_thread_fence(std::memory_order_acquire);
    return canReadTraceContext == 0;
}

inline bool ManagedThreadInfo::HasTraceContext() const
{
    if (CanReadTraceContext())
    {
        std::uint64_t localRootSpanId = GetLocalRootSpanId();
        std::uint64_t spanId = GetSpanId();

        return localRootSpanId != 0 && spanId != 0;
    }
    return false;
}
