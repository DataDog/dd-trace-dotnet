// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for WINDOWS

#include "resource.h"

#include "OsSpecificApi.h"

#include "StackFramesCollectorBase.h"
#include "SystemTime.h"
#include "Windows32BitStackFramesCollector.h"
#include "Windows64BitStackFramesCollector.h"
#include "Log.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {

std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
#ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return std::make_unique<Windows64BitStackFramesCollector>(pCorProfilerInfo);
#else
    assert(8 * sizeof(void*) == 32);
    return std::make_unique<Windows32BitStackFramesCollector>(pCorProfilerInfo);
#endif
}

uint64_t GetThreadCpuTime(ManagedThreadInfo* pThreadInfo)
{
    FILETIME creationTime, exitTime = {}; // not used here
    FILETIME kernelTime = {};
    FILETIME userTime = {};

    if (::GetThreadTimes(pThreadInfo->GetOsThreadHandle(), &creationTime, &exitTime, &kernelTime, &userTime))
    {
        uint64_t milliseconds = GetTotalMilliseconds(userTime) + GetTotalMilliseconds(kernelTime);
        return milliseconds;
    }

    return 0;
}


typedef LONG KPRIORITY;

struct CLIENT_ID
{
    DWORD UniqueProcess; // Process ID
#ifdef BIT64
    ULONG pad1;
#endif
    DWORD UniqueThread; // Thread ID
#ifdef BIT64
    ULONG pad2;
#endif
};

typedef struct
{
    FILETIME KernelTime;
    FILETIME UserTime;
    FILETIME CreateTime;
    ULONG WaitTime;
#ifdef BIT64
    ULONG pad1;
#endif
    PVOID StartAddress;
    CLIENT_ID Client_Id;
    KPRIORITY CurrentPriority;
    KPRIORITY BasePriority;
    ULONG ContextSwitchesPerSec;
    ULONG ThreadState;
    ULONG ThreadWaitReason;
    ULONG pad2;
} SYSTEM_THREAD_INFORMATION;

typedef enum
{
    Initialized,
    Ready,
    Running,
    Standby,
    Terminated,
    Waiting,
    Transition,
    DeferredReady
} THREAD_STATE;

#define SYSTEMTHREADINFORMATION 40
typedef NTSTATUS(WINAPI* NtQueryInformationThread_)(HANDLE, int, PVOID, ULONG, PULONG);

NtQueryInformationThread_ NtQueryInformationThread = nullptr;

bool InitializeCallback()
{
    auto hModule = GetModuleHandleA("NtDll.dll");
    if (hModule == nullptr)
    {
        Log::Error("Impossible to load ntdll.dll: 0x", std::hex, GetLastError());
        return false;
    }

    NtQueryInformationThread = (NtQueryInformationThread_)GetProcAddress(hModule, "NtQueryInformationThread");
    if (NtQueryInformationThread == nullptr)
    {
        Log::Error("Impossible to get NtQueryInformationThread: 0x", std::hex, GetLastError());
        return false;
    }

    return true;
}

bool IsRunning(ULONG threadState)
{
    return
        (THREAD_STATE::Running == threadState) ||
        (THREAD_STATE::DeferredReady == threadState) ||
        (THREAD_STATE::Standby == threadState)
        ;

    // Note that THREAD_STATE::Standby, THREAD_STATE::Ready and THREAD_STATE::DeferredReady
    // indicate that threads are simply waiting for an available core to run.
    // If some callstacks show non cpu-bound frames at the top, return true only for Running state
}

bool IsRunning(ManagedThreadInfo* pThreadInfo, uint64_t& cpuTime)
{
    if (NtQueryInformationThread == nullptr)
    {
        if (!InitializeCallback())
        {
            return false;
        }
    }

    SYSTEM_THREAD_INFORMATION sti = {0};
    auto size = sizeof(SYSTEM_THREAD_INFORMATION);
    ULONG buflen = 0;
    NTSTATUS lResult = NtQueryInformationThread(pThreadInfo->GetOsThreadHandle(), SYSTEMTHREADINFORMATION, &sti, size, &buflen);
    if (lResult != 0)
    {
        // This always happens in 32 bit so uses another API to at least get the CPU consumption
        cpuTime = GetThreadCpuTime(pThreadInfo);
        return false;
    }

    cpuTime = GetTotalMilliseconds(sti.UserTime) + GetTotalMilliseconds(sti.KernelTime);

    return IsRunning(sti.ThreadState);
}


} // namespace OsSpecificApi