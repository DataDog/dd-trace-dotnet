// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for WINDOWS

#include "resource.h"

#include "OsSpecificApi.h"

#include "IConfiguration.h"
#include "IThreadInfo.h"
#include "Log.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"
#include "ScopedHandle.h"
#include "StackFramesCollectorBase.h"
#include "SystemTime.h"
#include "Windows32BitStackFramesCollector.h"
#include "Windows64BitStackFramesCollector.h"
#include "WindowsThreadInfo.h"
#include "EtwEventsManager.h"

#include "shared/src/native-src/loader.h"

#include <memory>
#include <sstream>

#include <tlhelp32.h>
#include <windows.h>

class CallstackProvider;

namespace OsSpecificApi {

// if a system message was not found for the last error code the message will contain GetLastError between ()
std::pair<DWORD, std::string> GetLastErrorMessage()
{
    std::string message;
    LPVOID pBuffer;
    DWORD errorCode = GetLastError();
    DWORD length = FormatMessageA(
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
            FORMAT_MESSAGE_FROM_SYSTEM |
            FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL,
        errorCode,
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        (char*)&pBuffer,
        0, NULL);

    std::stringstream builder;
    builder << "(error code = 0x" << std::hex << errorCode << ")";

    if (length == 0)
    {
        // only format the error code into the message if no system message available
        message = builder.str();
        return std::make_pair(errorCode, message);
    }

    // otherwise, concat the system message to the error code
    char* sMsg = (char*)pBuffer;
    builder << ": " << sMsg;
    LocalFree(pBuffer);
    message = builder.str();
    return std::make_pair(errorCode, message);
}

std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration const* const pConfiguration, CallstackProvider* callstackProvider)
{
#ifdef BIT64
    static_assert(8 * sizeof(void*) == 64);
    return std::make_unique<Windows64BitStackFramesCollector>(pCorProfilerInfo, pConfiguration, callstackProvider);
#else
    assert(8 * sizeof(void*) == 32);
    return std::make_unique<Windows32BitStackFramesCollector>(pCorProfilerInfo, pConfiguration, callstackProvider);
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
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        if (isFirstError && (errorCode != ERROR_INVALID_HANDLE)) // expected invalid handle case
        {
            isFirstError = false;

            if (errorCode == 0)
            {
                Log::Error("GetThreadCpuTime() error calling GetThreadTimes (error code = 0x0)");
            }
            else
            {
                Log::Error("GetThreadCpuTime() error calling GetThreadTimes ", message);
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
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Error("Impossible to load ntdll.dll ", message);
        return false;
    }

    NtQueryInformationThread = (NtQueryInformationThread_)GetProcAddress(hModule, "NtQueryInformationThread");
    if (NtQueryInformationThread == nullptr)
    {
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Error("Impossible to get NtQueryInformationThread ", message);
        return false;
    }

    return true;
}

bool IsRunning(ULONG threadState)
{
    return (THREAD_STATE::Running == threadState) ||
           (THREAD_STATE::DeferredReady == threadState) ||
           (THREAD_STATE::Standby == threadState);

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
            auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
            if (errorCode == 0)
            {
                Log::Error("IsRunning() - NtQueryInformationThread failure 0x", std::hex, lResult);
            }
            else
            {
                Log::Error("IsRunning() - NtQueryInformationThread failure 0x", std::hex, lResult, " ", message);
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
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Info("Impossible to retrieve the number of processors ", message);

        return 1;
    }

    return nbProcs;
}

ScopedHandle GetThreadHandle(DWORD threadId)
{
    auto handle = ScopedHandle(::OpenThread(THREAD_QUERY_INFORMATION, FALSE, threadId));
    if (handle == NULL)
    {
        auto [errorCode, message] = OsSpecificApi::GetLastErrorMessage();
        Log::Debug("GetThreadHandle: Error getting thread handle for thread id '", threadId, "' ", message);
    }
    return handle;
}

std::vector<std::shared_ptr<IThreadInfo>> GetProcessThreads()
{
    std::vector<std::shared_ptr<IThreadInfo>> result;
    result.reserve(1024);

    auto h = ScopedHandle(CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, OpSysTools::GetProcId()));

    if (h.IsValid())
    {
        THREADENTRY32 te{};
        te.dwSize = sizeof(te);
        if (Thread32First(h, &te))
        {
            auto processId = OpSysTools::GetProcId();
            do
            {
                if (te.dwSize >= FIELD_OFFSET(THREADENTRY32, th32OwnerProcessID) +
                                     sizeof(te.th32OwnerProcessID) &&
                    te.th32ThreadID != 0 && te.th32OwnerProcessID == processId)
                {
                    auto threadHnd = GetThreadHandle(te.th32ThreadID);

                    if (threadHnd.IsValid())
                    {
                        auto name = OpSysTools::GetNativeThreadName(threadHnd);
                        result.push_back(std::make_shared<WindowsThreadInfo>(te.th32ThreadID, std::move(threadHnd), std::move(name)));
                    }
                }
                te.dwSize = sizeof(te);
            } while (Thread32Next(h, &te));
        }
    }
    return result;
}

std::string GetProcessStartTime()
{
    HANDLE hProcess = ::GetCurrentProcess();
    FILETIME creationTime;
    FILETIME exitTime;
    FILETIME userTime;
    FILETIME kernelTime;
    if (!::GetProcessTimes(hProcess, &creationTime, &exitTime, &kernelTime, &userTime))
    {
        return "";
    }

    SYSTEMTIME sCreationTime;
    if (!::FileTimeToSystemTime(&creationTime, &sCreationTime))
    {
        return "";
    }

    std::stringstream builder;
    builder
        << sCreationTime.wYear
        << "-" << std::setfill('0') << std::setw(2) << sCreationTime.wMonth
        << "-" << std::setfill('0') << std::setw(2) << sCreationTime.wDay
        << "T" << std::setfill('0') << std::setw(2) << sCreationTime.wHour
        << ":" << std::setfill('0') << std::setw(2) << sCreationTime.wMinute
        << ":" << std::setfill('0') << std::setw(2) << sCreationTime.wSecond
        << "Z"; // for UTC
    return builder.str();

}

std::unique_ptr<IEtwEventsManager> CreateEtwEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener,
    IConfiguration* pConfiguration)
{
    auto manager = std::make_unique<EtwEventsManager>(pAllocationListener, pContentionListener, pGCSuspensionsListener, pConfiguration);
    return manager;
}

double GetProcessLifetime()
{
    FILETIME creationTime;
    FILETIME exitTime;
    FILETIME userTime;
    FILETIME kernelTime;
    if (!::GetProcessTimes(::GetCurrentProcess(), &creationTime, &exitTime, &kernelTime, &userTime))
    {
        return 0;
    }

    ::GetSystemTimeAsFileTime(&exitTime);

    // Convert the FILETIME structures to ULARGE_INTEGER to make arithmetic calculations easier.
    ULARGE_INTEGER start;
    ULARGE_INTEGER end;
    start.LowPart = creationTime.dwLowDateTime;
    start.HighPart = creationTime.dwHighDateTime;
    end.LowPart = exitTime.dwLowDateTime;
    end.HighPart = exitTime.dwHighDateTime;

    // Calculate the difference and convert it from 100-nanosecond intervals to seconds.
    double duration = static_cast<double>(end.QuadPart - start.QuadPart) / 10000000.0;
    return duration;
}

} // namespace OsSpecificApi