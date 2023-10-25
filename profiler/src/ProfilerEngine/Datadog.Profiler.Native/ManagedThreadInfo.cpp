// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedThreadInfo.h"
#include "shared/src/native-src/string.h"

std::atomic<std::uint32_t> ManagedThreadInfo::s_nextProfilerThreadInfoId{1};

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

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId) :
    ManagedThreadInfo(clrThreadId, 0, static_cast<HANDLE>(0), shared::WSTRING())
{
}

ManagedThreadInfo::ManagedThreadInfo(ThreadID clrThreadId, DWORD osThreadId, HANDLE osThreadHandle, shared::WSTRING pThreadName) :
    _profilerThreadInfoId{GenerateProfilerThreadInfoId()},
    _clrThreadId(clrThreadId),
    _osThreadId(osThreadId),
    _osThreadHandle(osThreadHandle),
    _pThreadName(std::move(pThreadName)),
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
    _traceContextTrackingInfo{},
    _cpuConsumptionMilliseconds{0},
    _timestamp{0},
    _sharedMemoryArea{nullptr}
#ifdef LINUX
    ,
    _stackBaseAddress{0}
#endif
{
}

#ifdef LINUX
namespace {
union pthread_attr_safe_t
{
    pthread_attr_t attrs;

    // extra size to match glibc size
    // cppcheck-suppress unusedStructMember
    char reserved[64];
};
} // namespace

// Safe version of pthread_getattr_np
int pthread_getattr_np_safe(pthread_t th, pthread_attr_t* attr)
{
    // pad pthread_getattr_np argument with extra space to avoid out-of-bound
    // write on the stack
    pthread_attr_safe_t safe_attrs;
    int res = pthread_getattr_np(th, &safe_attrs.attrs);
    if (!res)
    {
        *attr = safe_attrs.attrs;
    }
    return res;
}

std::uintptr_t ManagedThreadInfo::RetrieveStackBaseAdress()
{
    void* stack_addr;
    size_t stack_size;
    pthread_attr_t attrs;
    if (pthread_getattr_np_safe(pthread_self(), &attrs) != 0)
    {
        return 0;
    }

    // pthread_attr_destroy(&attrs);
    if (pthread_attr_getstack(&attrs, &stack_addr, &stack_size) != 0)
    {
        return 0;
    }
    return (std::uintptr_t)((std::byte*)stack_addr + stack_size);
}

void ManagedThreadInfo::InitializeStackBoudaries()
{
    _stackBaseAddress = RetrieveStackBaseAdress();
}

#endif
