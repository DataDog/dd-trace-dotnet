// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "OpSysTools.h"
#ifdef _WINDOWS
#include "shared/src/native-src/string.h"
#include <malloc.h>
#include <mmsystem.h>
#include <processthreadsapi.h>
#include <psapi.h>
#include <wchar.h>
#include <windows.h>
#pragma comment(lib, "winmm.lib")
#else
#include <cstdlib>
#include <dlfcn.h>
#include <fcntl.h>
#include <pthread.h>
#include <stdlib.h>
#include <sys/types.h>
#include <unistd.h>
#define _GNU_SOURCE
#include <errno.h>
#endif

#include <chrono>
#include <thread>

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

#include "Log.h"

#define MAX_CHAR 512

std::int64_t OpSysTools::s_nanosecondsPerHighPrecisionTimerTick = 0;
std::int64_t OpSysTools::s_highPrecisionTimerTicksPerNanosecond = 0;

#ifdef _WINDOWS
bool OpSysTools::s_isRunTimeLinkingThreadDescriptionDone = false;
OpSysTools::SetThreadDescriptionDelegate_t OpSysTools::s_setThreadDescriptionDelegate = nullptr;
OpSysTools::GetThreadDescriptionDelegate_t OpSysTools::s_getThreadDescriptionDelegate = nullptr;
#endif

int OpSysTools::GetProcId()
{
#ifdef _WINDOWS
    return ::GetCurrentProcessId();
#else
    return getpid();
#endif
}

int OpSysTools::GetThreadId()
{
#ifdef _WINDOWS
    return ::GetCurrentThreadId();
#else
    return (SIZE_T)syscall(SYS_gettid);
#endif
}

bool OpSysTools::InitHighPrecisionTimer(void)
{
#ifdef _WINDOWS
    LARGE_INTEGER ticksPerSecond;
    bool counterExists = QueryPerformanceFrequency(&ticksPerSecond);

    if (!counterExists || ticksPerSecond.QuadPart < 1)
    {
        s_nanosecondsPerHighPrecisionTimerTick = 0;
        s_highPrecisionTimerTicksPerNanosecond = 0;
        return false;
    }

    if (NanosecondsPerSecond >= ticksPerSecond.QuadPart)
    {
        s_nanosecondsPerHighPrecisionTimerTick = ticksPerSecond.QuadPart / NanosecondsPerSecond;
        s_highPrecisionTimerTicksPerNanosecond = 0;
    }
    else
    {
        s_nanosecondsPerHighPrecisionTimerTick = 0;
        s_highPrecisionTimerTicksPerNanosecond = ticksPerSecond.QuadPart / NanosecondsPerSecond;
    }

    return true;
#else
    // Need to implement this for Linux!
    s_nanosecondsPerHighPrecisionTimerTick = 0;
    s_highPrecisionTimerTicksPerNanosecond = 0;
    return false;
#endif
}

std::int64_t OpSysTools::GetHighPrecisionNanosecondsFallback(void)
{
    std::chrono::high_resolution_clock::time_point now = std::chrono::high_resolution_clock::now();

    long long totalNanosecs = std::chrono::duration_cast<std::chrono::nanoseconds>(now.time_since_epoch()).count();
    return static_cast<std::int64_t>(totalNanosecs);
}

#ifdef _WINDOWS
void OpSysTools::InitDelegates_GetSetThreadDescription(void)
{
    if (s_isRunTimeLinkingThreadDescriptionDone)
    {
        // s_isRunTimeLinkingThreadDescriptionDone is not volatile. benign init race.
        return;
    }

    // We do not bother unloading KernelBase.dll later. It is prbably already loaded, and if not, we can keep it in mem.
    HMODULE moduleHandle = GetModuleHandle(WStr("KernelBase.dll"));
    if (NULL == moduleHandle)
    {
        moduleHandle = LoadLibrary(WStr("KernelBase.dll"));
    }

    if (NULL != moduleHandle)
    {
        s_setThreadDescriptionDelegate = reinterpret_cast<SetThreadDescriptionDelegate_t>(GetProcAddress(moduleHandle, "SetThreadDescription"));
        ;
        s_getThreadDescriptionDelegate = reinterpret_cast<GetThreadDescriptionDelegate_t>(GetProcAddress(moduleHandle, "GetThreadDescription"));
        ;
    }

    Log::Debug("OpSysTools::InitDelegates_GetSetThreadDescription() completed."
               " s_setThreadDescriptionDelegate set: ",
               (nullptr != s_setThreadDescriptionDelegate),
               "; s_getThreadDescriptionDelegate set: ",
               (nullptr != s_getThreadDescriptionDelegate));

    s_isRunTimeLinkingThreadDescriptionDone = true;
}

OpSysTools::SetThreadDescriptionDelegate_t OpSysTools::GetDelegate_SetThreadDescription(void)
{
    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    if (nullptr == setThreadDescriptionDelegate)
    {
        InitDelegates_GetSetThreadDescription();
        setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    }

    return setThreadDescriptionDelegate;
}

OpSysTools::GetThreadDescriptionDelegate_t OpSysTools::GetDelegate_GetThreadDescription(void)
{
    GetThreadDescriptionDelegate_t getThreadDescriptionDelegate = s_getThreadDescriptionDelegate;
    if (nullptr == getThreadDescriptionDelegate)
    {
        InitDelegates_GetSetThreadDescription();
        getThreadDescriptionDelegate = s_getThreadDescriptionDelegate;
    }

    return getThreadDescriptionDelegate;
}
#endif // #ifdef _WINDOWS

bool OpSysTools::SetNativeThreadName(std::thread* pNativeThread, const WCHAR* description)
{
    if (nullptr == pNativeThread)
    {
        return false;
    }

#ifdef _WINDOWS
    // The SetThreadDescription(..) API is only available on recent Windows versions and must be called dynamically.
    // We attempt to link to it at runtime, and if we do not succeed, this operation is a No-Op.
    // https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setthreaddescription#remarks

    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = GetDelegate_SetThreadDescription();
    if (nullptr == setThreadDescriptionDelegate)
    {
        return false;
    }

    HANDLE windowsThreadHandle = static_cast<HANDLE>(pNativeThread->native_handle());
    HRESULT hr = setThreadDescriptionDelegate(windowsThreadHandle, description);
    return SUCCEEDED(hr);
#else
    return false;
#endif
}

bool OpSysTools::GetNativeThreadName(HANDLE windowsThreadHandle, WCHAR* pThreadDescrBuff, const std::uint32_t threadDescrBuffSize)
{
#ifdef _WINDOWS
    // The SetThreadDescription(..) API is only available on recent Windows versions and must be called dynamically.
    // We attempt to link to it at runtime, and if we do not succeed, this operation is a No-Op.
    // https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getthreaddescription#remarks

    GetThreadDescriptionDelegate_t getThreadDescriptionDelegate = GetDelegate_GetThreadDescription();
    if (nullptr == getThreadDescriptionDelegate)
    {
        return false;
    }

    PWSTR pThreadDescr = nullptr;
    HRESULT hr = getThreadDescriptionDelegate(windowsThreadHandle, &pThreadDescr);
    if (FAILED(hr) || nullptr == pThreadDescr)
    {
        return false;
    }

    wcsncpy_s(pThreadDescrBuff, threadDescrBuffSize, pThreadDescr, threadDescrBuffSize);
    *(pThreadDescrBuff + threadDescrBuffSize) = 0;

    LocalFree(pThreadDescr);

    return true;
#else
    return false;
#endif
}

bool OpSysTools::GetModuleHandleFromInstructionPointer(void* nativeIP, std::uint64_t* pModuleHandle)
{
#ifdef _WINDOWS
    if (nullptr == nativeIP)
    {
        return false;
    }

    HMODULE mh;

    if (!GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           static_cast<LPCTSTR>(nativeIP),
                           &mh))
    {
        return false;
    }

    if (nullptr != pModuleHandle)
    {
        *pModuleHandle = static_cast<uint64_t>(reinterpret_cast<uintptr_t>(mh));
    }

    return true;
#else
    return false;
#endif
}

std::string OpSysTools::GetModuleName(void* nativeIP)
{
#ifdef _WINDOWS
    std::uint64_t hModule;
    if (!GetModuleHandleFromInstructionPointer(nativeIP, &hModule))
    {
        return "";
    }

    char filename[260];
    // https://docs.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulefilenamea
    auto charCount = GetModuleFileNameA((HMODULE)hModule, filename, sizeof(filename)/sizeof(filename[0]));
    if (charCount > 0)
    {
        return filename;
    }

    return "";

#else
    // https://linux.die.net/man/3/dladdr
    Dl_info info;
    if (dladdr((void*)nativeIP, &info))
    {
        return fs::path(info.dli_fname).remove_filename();
    }
    return "";
#endif
}


void* OpSysTools::AlignedMAlloc(size_t alignment, size_t size)
{
#ifdef _WINDOWS
    return _aligned_malloc(size, alignment);
#else
    return aligned_alloc(alignment, size);
#endif
}

void OpSysTools::MemoryBarrierProcessWide(void)
{
#ifdef _WINDOWS
    FlushProcessWriteBuffers();
#else
    // @ToDo: Correctly wrap sys_membarrier() !!
#endif
}

std::string OpSysTools::GetHostname()
{
#ifdef _WINDOWS
    WCHAR hostname[MAX_CHAR];
    DWORD length = MAX_CHAR;
    if (GetComputerName(hostname, &length) != 0)
    {
        return shared::ToString(hostname);
    }
#else
    char hostname[MAX_CHAR];
    if (gethostname(hostname, MAX_CHAR) == 0)
    {
        return hostname;
    }
#endif
    return "Unknown-hostname";
}

std::string OpSysTools::GetProcessName()
{
#ifdef _WINDOWS
    const DWORD length = 260;
    char pathName[length]{};

    const DWORD len = GetModuleFileNameA(nullptr, pathName, length);
    return fs::path(pathName).filename().string();
#elif MACOS
    const int length = 260;
    char* buffer = new char[length];
    proc_name(getpid(), buffer, length);
    return std::string(buffer);
#else
    std::fstream comm("/proc/self/comm");
    std::string name;
    std::getline(comm, name);
    return name;
#endif
}
