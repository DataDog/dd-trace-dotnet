// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "cor.h"
#include "corprof.h"

#include "IThreadInfo.h"
#include "ScopedHandle.h"
#include "shared/src/native-src/string.h"

#include <atomic>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <utility>

static constexpr int32_t MinFieldAlignRequirement = 8;
static constexpr int32_t FieldAlignRequirement = (MinFieldAlignRequirement >= alignof(std::uint64_t)) ? MinFieldAlignRequirement : alignof(std::uint64_t);

struct alignas(FieldAlignRequirement) TraceContextTrackingInfo
{
public:
    std::uint64_t _writeGuard;
    std::uint64_t _currentLocalRootSpanId;
    std::uint64_t _currentSpanId;
};

struct ManagedThreadInfo : public IThreadInfo
{
private:
    ManagedThreadInfo(ThreadID clrThreadId, ICorProfilerInfo4* pCorProfilerInfo, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING pThreadName);
    static std::uint32_t GenerateProfilerThreadInfoId();

public:
    explicit ManagedThreadInfo(ThreadID clrThreadId, ICorProfilerInfo4* pCorProfilerInfo);
    ~ManagedThreadInfo() = default;

    // This field is set in the CorProfilerCallback. It's based on the assumption that the thread's calling ThreadAssignedToOSThread
    // is the same native thread assigned to the managed thread.
    static thread_local std::shared_ptr<ManagedThreadInfo> CurrentThreadInfo;

    inline std::uint32_t GetProfilerThreadInfoId() const;

    inline ThreadID GetClrThreadId() const;

    inline DWORD GetOsThreadId() const override;
    inline HANDLE GetOsThreadHandle() const override;
    inline void SetOsInfo(DWORD osThreadId, HANDLE osThreadHandle);

    inline const shared::WSTRING& GetThreadName() const override;
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

    inline bool IsThreadDestroyed();
    inline bool IsDestroyed();
    inline void SetThreadDestroyed();
    inline std::pair<uint64_t, shared::WSTRING> SetBlockingThread(uint64_t osThreadId, shared::WSTRING name);

    inline TraceContextTrackingInfo* GetTraceContextPointer();
    inline bool HasTraceContext() const;

    inline std::string GetProfileThreadId() override;
    inline std::string GetProfileThreadName() override;
    inline void AcquireLock();
    inline void ReleaseLock();

#ifdef LINUX
    inline void SetSharedMemory(volatile int* memoryArea);
    inline void MarkAsInterrupted();
    inline int32_t SetTimerId(int32_t timerId);
    inline int32_t GetTimerId() const;
    inline bool CanBeInterrupted() const;
#endif

    inline AppDomainID GetAppDomainId();

    inline std::pair<std::uint64_t, std::uint64_t> GetTracingContext() const;

private:
    inline std::string BuildProfileThreadId();
    inline std::string BuildProfileThreadName();
    inline bool CanReadTraceContext() const;

private:
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

    bool _isThreadDestroyed;

    TraceContextTrackingInfo _traceContext;

    //  strings to be used by samples: avoid allocations when rebuilding them over and over again
    std::string _profileThreadId;
    std::string _profileThreadName;

    ICorProfilerInfo4* _info;
    std::shared_mutex _threadIdMutex;
    std::shared_mutex _threadNameMutex;
#ifdef LINUX
    // Linux only
    // This is pointer to a shared memory area coming from the Datadog.Linux.ApiWrapper library.
    // This establishes a simple communication channel between the profiler and this library
    // to know (for now, maybe more later) if the profiler interrupted a thread which was
    // doing a syscalls.
    volatile int* _sharedMemoryArea;
    std::int32_t _timerId;
#endif
    uint64_t _blockingThreadId;
    shared::WSTRING _blockingThreadName;
    std::mutex _objLock;
};

std::string ManagedThreadInfo::GetProfileThreadId()
{
    {
        auto l = std::shared_lock(_threadIdMutex);
        if (!_profileThreadId.empty())
        {
            return _profileThreadId;
        }
    }

    auto id = BuildProfileThreadId();
    std::unique_lock l(_threadIdMutex);
    if (_profileThreadId.empty())
    {
        _profileThreadId = std::move(id);
    }

    return _profileThreadId;
}

std::string ManagedThreadInfo::GetProfileThreadName()
{
    {
        std::shared_lock l(_threadNameMutex);
        if (!_profileThreadName.empty())
        {
            return _profileThreadName;
        }
    }

    auto s = BuildProfileThreadName();
    std::unique_lock l(_threadNameMutex);
    if (_profileThreadName.empty())
    {
        _profileThreadName = std::move(s);
    }
    return _profileThreadName;
}

inline void ManagedThreadInfo::AcquireLock()
{
    _objLock.lock();
}

inline void ManagedThreadInfo::ReleaseLock()
{
    _objLock.unlock();
}

inline std::string ManagedThreadInfo::BuildProfileThreadId()
{
    std::stringstream builder;
    builder << "<" << std::dec << _profilerThreadInfoId << "> [#" << _osThreadId << "]";

    return builder.str();
}

inline std::string ManagedThreadInfo::BuildProfileThreadName()
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

    return nameBuilder.str();
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

    auto id = BuildProfileThreadId();
    std::unique_lock l(_threadIdMutex);
    if (_profileThreadId.empty())
    {
        _profileThreadId = std::move(id);
    }
}

inline const shared::WSTRING& ManagedThreadInfo::GetThreadName() const
{
    return _pThreadName;
}

inline void ManagedThreadInfo::SetThreadName(shared::WSTRING pThreadName)
{
    _pThreadName = std::move(pThreadName);

    auto s = BuildProfileThreadName();
    std::unique_lock l(_threadNameMutex);
    if (_profileThreadName.empty())
    {
        _profileThreadName = std::move(s);
    }
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

// TODO: this does not seem to be needed
inline bool ManagedThreadInfo::IsThreadDestroyed()
{
    std::unique_lock l(_objLock);
    return _isThreadDestroyed;
}

// This is not synchronized and must be called under the _stackWalkLock lock
inline bool ManagedThreadInfo::IsDestroyed()
{
    return _isThreadDestroyed;
}

inline void ManagedThreadInfo::SetThreadDestroyed()
{
    std::unique_lock l(_objLock);
    _isThreadDestroyed = true;
}

inline std::pair<uint64_t, shared::WSTRING> ManagedThreadInfo::SetBlockingThread(uint64_t osThreadId, shared::WSTRING name)
{
    auto oldId = std::exchange(_blockingThreadId, osThreadId);
    auto oldName = std::exchange(_blockingThreadName, std::move(name));
    return {oldId, oldName};
}

inline TraceContextTrackingInfo* ManagedThreadInfo::GetTraceContextPointer()
{
    return &_traceContext;
}

inline bool ManagedThreadInfo::CanReadTraceContext() const
{
    bool canReadTraceContext = _traceContext._writeGuard;

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
        auto [localRootSpanId, spanId] = GetTracingContext();

        return localRootSpanId != 0 && spanId != 0;
    }
    return false;
}

#ifdef LINUX

inline bool ManagedThreadInfo::CanBeInterrupted() const
{
    return _sharedMemoryArea == nullptr;
}

// This method is called by the signal handler, when the thread has already been interrupted.
// There is no race and it's safe to call it there.
inline void ManagedThreadInfo::MarkAsInterrupted()
{
    if (_sharedMemoryArea != nullptr)
    {
        *_sharedMemoryArea = 1;
    }
}

inline void ManagedThreadInfo::SetSharedMemory(volatile int* memoryArea)
{
    _sharedMemoryArea = memoryArea;
}

inline std::int32_t ManagedThreadInfo::SetTimerId(std::int32_t timerId)
{
    return std::exchange(_timerId, timerId);
}

inline std::int32_t ManagedThreadInfo::GetTimerId() const
{
    return _timerId;
}
#endif

inline AppDomainID ManagedThreadInfo::GetAppDomainId()
{
    // This function will be called in the signal handler.
    // As far as I saw, this function is safe'ish to be called from a signal handler.
    // If at some point, it's not safe anymore, we will have to rethink how we get
    // the AppDomainID from a signal handler.
    AppDomainID appDomainId{0};
    HRESULT hr = _info->GetThreadAppDomain(_clrThreadId, &appDomainId);
    return appDomainId;
}

inline std::pair<std::uint64_t, std::uint64_t> ManagedThreadInfo::GetTracingContext() const
{
    std::uint64_t localRootSpanId = 0;
    std::uint64_t spanId = 0;

    if (CanReadTraceContext())
    {
        localRootSpanId = _traceContext._currentLocalRootSpanId;
        spanId = _traceContext._currentSpanId;
    }

    return {localRootSpanId, spanId};
}