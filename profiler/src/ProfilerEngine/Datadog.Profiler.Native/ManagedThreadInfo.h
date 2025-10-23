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

#ifdef LINUX
#include "SpinningMutex.hpp"
using dd_mutex_t = SpinningMutex;
#else
using dd_mutex_t = std::mutex;
#endif

static constexpr int32_t MinFieldAlignRequirement = 8;
static constexpr int32_t FieldAlignRequirement = (MinFieldAlignRequirement >= alignof(std::uint64_t)) ? MinFieldAlignRequirement : alignof(std::uint64_t);

struct alignas(FieldAlignRequirement) TraceContextTrackingInfo
{
public:
    std::uint64_t _writeGuard;
    std::uint64_t _currentLocalRootSpanId;
    std::uint64_t _currentSpanId;
};


enum class ContentionType {
    Unknown = 0,
    Lock = 1,
    Wait = 2,

    ContentionTypeCount = 3 // This is used to know the last element in the enum
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

    inline std::chrono::nanoseconds SetLastSampleTimestamp(std::chrono::nanoseconds value);
    inline std::chrono::milliseconds GetCpuConsumption() const;
    inline std::chrono::milliseconds SetCpuConsumption(std::chrono::milliseconds value, std::chrono::nanoseconds timestamp);
    inline std::chrono::nanoseconds GetCpuTimestamp() const;

    inline void GetOrResetDeadlocksCount(std::uint64_t deadlocksAggregationPeriodIndex,
                                         std::uint64_t* pPrevCount,
                                         std::uint64_t* pNewCount);
    inline void IncDeadlocksCount();
    inline void GetDeadlocksCount(std::uint64_t* deadlockDetectionsTotalCount,
                                  std::uint64_t* deadlockDetectionsInAggregationPeriodCount,
                                  std::uint64_t* usedDeadlockDetectionsAggregationPeriodIndex) const;

    inline bool IsDestroyed();
    inline void SetThreadDestroyed();
    inline std::pair<uint64_t, shared::WSTRING> SetBlockingThread(uint64_t osThreadId, shared::WSTRING name);

    inline TraceContextTrackingInfo* GetTraceContextPointer();
    inline bool HasTraceContext() const;

    inline std::string GetProfileThreadId() override;
    inline std::string GetProfileThreadName() override;
    inline void AcquireLock();
    inline bool TryAcquireLock();
    inline void ReleaseLock();

#ifdef LINUX
    inline void SetSharedMemory(volatile int* memoryArea);
    inline void MarkAsInterrupted();
    inline int32_t SetTimerId(int32_t timerId);
    inline int32_t GetTimerId() const;
    inline bool CanBeInterrupted() const;
#endif

#ifdef DD_TEST
    inline static std::unique_ptr<ManagedThreadInfo>CreateForTest(std::uint32_t osThreadId)
    {
        ManagedThreadInfo::s_nextProfilerThreadInfoId = 1;
        auto threadInfo = std::make_unique<ManagedThreadInfo>(1, nullptr);
        threadInfo->SetOsInfo(osThreadId, HANDLE());
        return threadInfo;
    }
#endif

    inline AppDomainID GetAppDomainId();

    inline std::pair<std::uint64_t, std::uint64_t> GetTracingContext() const;

    // TODO: check if we need to create a dedicated dictionary for WaitHandle profiling
    //       --> this would reduce memory consumption
    inline void SetWaitStart(std::chrono::nanoseconds timestamp) { _waitStartTimestamp = timestamp; }
    inline std::chrono::nanoseconds GetWaitStart() { return _waitStartTimestamp; }
    inline void SetContentionType(ContentionType contentionType) { _contentionType = contentionType; }
    ContentionType GetContentionType() { return _contentionType; }

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
    shared::WSTRING _threadName;
    std::once_flag _profileThreadIdOnceFlag;
    std::once_flag _profileThreadNameOnceFlag;

    std::chrono::nanoseconds _lastSampleHighPrecisionTimestamp;
    std::chrono::milliseconds _cpuConsumption;
    std::chrono::nanoseconds _timestamp;

    std::uint64_t _deadlockTotalCount;
    std::uint64_t _deadlockInPeriodCount;
    std::uint64_t _deadlockDetectionPeriod;

    bool _isThreadDestroyed;

    TraceContextTrackingInfo _traceContext;

    //  strings to be used by samples: avoid allocations when rebuilding them over and over again
    std::string _profileThreadId;
    std::string _profileThreadName;

    ICorProfilerInfo4* _info;
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
    dd_mutex_t _objLock;

    // for WaitHandle profiling, keep track of the wait start timestamp
    std::chrono::nanoseconds _waitStartTimestamp;
    ContentionType _contentionType;
};

std::string ManagedThreadInfo::GetProfileThreadId()
{
    // PERF: use once flag to compute the profile thread id only once.
    // This is safe in case of multiple threads calling this method.
    // Only one thread will compute the profile thread id, and
    // the other threads will wait for the thread id to be computed.
    std::call_once(_profileThreadIdOnceFlag, [this]() {
        _profileThreadId = BuildProfileThreadId();
    });

    return _profileThreadId;
}

std::string ManagedThreadInfo::GetProfileThreadName()
{
    // PERF: use once flag to compute the profile thread name only once.
    // This is safe in case of multiple threads calling this method.
    // Only one thread will compute the profile thread name, and
    // the other threads will wait for the thread name to be computed.
    std::call_once(_profileThreadNameOnceFlag, [this]() {
        _profileThreadName = BuildProfileThreadName();
    });
    return _profileThreadName;
}

inline void ManagedThreadInfo::AcquireLock()
{
    _objLock.lock();
}

inline bool ManagedThreadInfo::TryAcquireLock()
{
    return _objLock.try_lock();
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
    auto threadName = _threadName;
    if (threadName.empty())
    {
        nameBuilder << "Managed thread (name unknown)";
    }
    else
    {
        nameBuilder << shared::ToString(std::move(threadName));
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

    // why do we compute the profile thread id here ?
    GetProfileThreadId();
}

inline const shared::WSTRING& ManagedThreadInfo::GetThreadName() const
{
    return _threadName;
}

inline void ManagedThreadInfo::SetThreadName(shared::WSTRING pThreadName)
{
    _threadName = std::move(pThreadName);

    // why computing thread name here ?
    GetProfileThreadName();
}

inline std::chrono::nanoseconds ManagedThreadInfo::SetLastSampleTimestamp(std::chrono::nanoseconds value)
{
    auto prevValue = _lastSampleHighPrecisionTimestamp;
    _lastSampleHighPrecisionTimestamp = value;
    return prevValue;
}

inline std::chrono::milliseconds ManagedThreadInfo::GetCpuConsumption() const
{
    return _cpuConsumption;
}

inline std::chrono::nanoseconds ManagedThreadInfo::GetCpuTimestamp() const
{
    return _timestamp;
}

inline std::chrono::milliseconds ManagedThreadInfo::SetCpuConsumption(std::chrono::milliseconds value, std::chrono::nanoseconds timestamp)
{
    _timestamp = timestamp;

    auto prevValue = _cpuConsumption;
    _cpuConsumption = value;
    return prevValue;
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