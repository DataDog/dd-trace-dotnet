// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for WINDOWS

#include "resource.h"

#include <memory>
#include "OsSpecificApi.h"

#include "IConfiguration.h"
#include "IThreadInfo.h"
#include "StackFramesCollectorBase.h"
#include "SystemTime.h"
#include "Windows32BitStackFramesCollector.h"
#include "Windows64BitStackFramesCollector.h"
#include "Log.h"
#include "ScopeFinalizer.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {

std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration const* const pConfiguration)
{
#ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return std::make_unique<Windows64BitStackFramesCollector>(pCorProfilerInfo);
#else
    assert(8 * sizeof(void*) == 32);
    return std::make_unique<Windows32BitStackFramesCollector>(pCorProfilerInfo);
#endif
}

uint64_t GetThreadCpuTime(IThreadInfo* pThreadInfo)
{
    FILETIME creationTime, exitTime = {}; // not used here
    FILETIME kernelTime = {};
    FILETIME userTime = {};
    static bool isFirstError = true;

    if (::GetThreadTimes(pThreadInfo->GetOsThreadHandle(), &creationTime, &exitTime, &kernelTime, &userTime))
    {
        uint64_t milliseconds = GetTotalMilliseconds(userTime) + GetTotalMilliseconds(kernelTime);
        return milliseconds;
    }
    else
    {
        DWORD errorCode = GetLastError();
        if (isFirstError && (errorCode != ERROR_INVALID_HANDLE)) // expected invalid handle case
        {
            isFirstError = false;
            LPVOID msgBuffer;

            if (errorCode == 0)
            {
                Log::Error("GetThreadCpuTime() error calling GetThreadTimes (last error = 0)");
            }
            else
            {
                FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                              NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR)&msgBuffer, 0, NULL);

                if (msgBuffer != NULL)
                {
                    Log::Error("GetThreadCpuTime() error calling GetThreadTimes (last error = 0x", std::hex, errorCode, std::dec, "): ", (LPCTSTR)msgBuffer);
                    LocalFree(msgBuffer);
                }
            }
        }
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

typedef BOOL(WINAPI* GetLogicalProcessorInformation_)(PSYSTEM_LOGICAL_PROCESSOR_INFORMATION, PDWORD);
GetLogicalProcessorInformation_ GetLogicalProcessorInformation = nullptr;


bool InitializeNtQueryInformationThreadCallback()
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

bool IsRunning(IThreadInfo* pThreadInfo, uint64_t& cpuTime, bool& failed)
{
    failed = true;
    cpuTime = 0;

    if (NtQueryInformationThread == nullptr)
    {
        if (!InitializeNtQueryInformationThreadCallback())
        {
            return false;
        }
    }

    SYSTEM_THREAD_INFORMATION sti = {0};
    auto size = sizeof(SYSTEM_THREAD_INFORMATION);
    ULONG buflen = 0;
    static bool isFirstError = true;
    NTSTATUS lResult = NtQueryInformationThread(pThreadInfo->GetOsThreadHandle(), SYSTEMTHREADINFORMATION, &sti, static_cast<ULONG>(size), &buflen);
    // deal with an invalid thread handle case (thread might have died)
    if (lResult != 0)
    {
#if BIT64 // Windows 64-bit
        if (isFirstError && (lResult != STATUS_INVALID_HANDLE))
        {
            isFirstError = false;
            LPVOID msgBuffer;
            DWORD errorCode = GetLastError();

            if (errorCode == 0)
            {
                Log::Error("IsRunning() error 0x", std::hex, lResult, " calling NtQueryInformationThread");
            }
            else
            {
                FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                              NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR)&msgBuffer, 0, NULL);

                if (msgBuffer != NULL)
                {
                    Log::Error("IsRunning() error 0x", std::hex, lResult, " calling NtQueryInformationThread (last error =  0x", std::hex, errorCode, std::dec, "): ", (LPTSTR)msgBuffer);
                    LocalFree(msgBuffer);
                }
            }
        }
#endif
        // This always happens in 32 bit so uses another API to at least get the CPU consumption
        cpuTime = GetThreadCpuTime(pThreadInfo);
        return false;
    }

    failed = false;
    cpuTime = GetTotalMilliseconds(sti.UserTime) + GetTotalMilliseconds(sti.KernelTime);

    return IsRunning(sti.ThreadState);
}

// https://devblogs.microsoft.com/oldnewthing/20200824-00/?p=104116

int32_t GetProcessorCount()
{
    auto nbProcs = GetActiveProcessorCount(ALL_PROCESSOR_GROUPS);
    if (nbProcs == 0)
    {
        DWORD errorMessageID = ::GetLastError();

        LPSTR messageBuffer = nullptr;
        // Free the Win32's string's buffer.
        on_leave { LocalFree(messageBuffer); };

        // Ask Win32 to give us the string version of that message ID.
        // The parameters we pass in, tell Win32 to create the buffer that holds the message for us (because we don't yet know how long the message string will be).
        size_t size = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                                     NULL, errorMessageID, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 0, NULL);

        // Copy the error message into a std::string.
        std::string message(messageBuffer, size);
        Log::Info("An error occured and we were unable to retrieve the number of processors (Error: ", message, ")");
        return 1;
    }
    return nbProcs;
}

} // namespace OsSpecificApi