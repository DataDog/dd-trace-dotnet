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
#include <fcntl.h>
#include <pthread.h>
#include <stdlib.h>
#include <sys/types.h>
#include <unistd.h>
#define _GNU_SOURCE
#include "cgroup.h"
#include <dlfcn.h>
#include <errno.h>
#include <link.h>
#include <sys/auxv.h>
#include <sys/prctl.h>
#endif

#include "ScopeFinalizer.h"

#include <chrono>
#include <thread>

#include "shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"

#include "Log.h"

#define MAX_CHAR 512

std::int64_t OpSysTools::s_nanosecondsPerHighPrecisionTimerTick = 0;
std::int64_t OpSysTools::s_highPrecisionTimerTicksPerNanosecond = 0;
uint64_t OpSysTools::s_ticksPerSecond = 0;


#ifdef _WINDOWS
bool OpSysTools::s_isRunTimeLinkingThreadDescriptionDone = false;
OpSysTools::SetThreadDescriptionDelegate_t OpSysTools::s_setThreadDescriptionDelegate = nullptr;
OpSysTools::GetThreadDescriptionDelegate_t OpSysTools::s_getThreadDescriptionDelegate = nullptr;
#endif

int32_t OpSysTools::GetProcId()
{
#ifdef _WINDOWS
    return ::GetCurrentProcessId();
#else
    return getpid();
#endif
}

int32_t OpSysTools::GetThreadId()
{
#ifdef _WINDOWS
    return ::GetCurrentThreadId();
#else
    return (SIZE_T)syscall(SYS_gettid);
#endif
}

bool OpSysTools::InitHighPrecisionTimer()
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

    s_ticksPerSecond = ticksPerSecond.QuadPart;

    // TODO: BUG - ticksPerSecond is ALWAYS smaller than NanosecondsPerSecond.
    // so this code is useless: both s_xx will be 0...
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

std::int64_t OpSysTools::GetHighPrecisionNanosecondsFallback()
{
    std::chrono::high_resolution_clock::time_point now = std::chrono::high_resolution_clock::now();

    int64_t totalNanosecs = std::chrono::duration_cast<std::chrono::nanoseconds>(now.time_since_epoch()).count();
    return static_cast<std::int64_t>(totalNanosecs);
}

#ifdef _WINDOWS
void OpSysTools::InitDelegates_GetSetThreadDescription()
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

OpSysTools::SetThreadDescriptionDelegate_t OpSysTools::GetDelegate_SetThreadDescription()
{
    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    if (nullptr == setThreadDescriptionDelegate)
    {
        InitDelegates_GetSetThreadDescription();
        setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    }

    return setThreadDescriptionDelegate;
}

OpSysTools::GetThreadDescriptionDelegate_t OpSysTools::GetDelegate_GetThreadDescription()
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

bool OpSysTools::SetNativeThreadName(const WCHAR* description)
{
#ifdef _WINDOWS
    // The SetThreadDescription(..) API is only available on recent Windows versions and must be called dynamically.
    // We attempt to link to it at runtime, and if we do not succeed, this operation is a No-Op.
    // https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setthreaddescription#remarks

    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = GetDelegate_SetThreadDescription();
    if (nullptr == setThreadDescriptionDelegate)
    {
        return false;
    }

    HRESULT hr = setThreadDescriptionDelegate(GetCurrentThread(), description);
    return SUCCEEDED(hr);
#else
    const auto name = shared::ToString(description);
    prctl(PR_SET_NAME, name.data(), 0, 0, 0);
    return true;
#endif
}

#ifdef _WINDOWS
shared::WSTRING OpSysTools::GetNativeThreadName(HANDLE handle)
{
    // The SetThreadDescription(..) API is only available on recent Windows versions and must be called dynamically.
    // We attempt to link to it at runtime, and if we do not succeed, this operation is a No-Op.
    // https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getthreaddescription#remarks

    GetThreadDescriptionDelegate_t getThreadDescriptionDelegate = GetDelegate_GetThreadDescription();
    if (nullptr == getThreadDescriptionDelegate)
    {
        return {};
    }

    PWSTR pThreadDescr = nullptr;
    HRESULT hr = getThreadDescriptionDelegate(handle, &pThreadDescr);
    if (FAILED(hr))
    {
        return {};
    }
    on_leave
    {
        LocalFree(pThreadDescr);
    };

    return shared::WSTRING(pThreadDescr);
}

ScopedHandle OpSysTools::GetThreadHandle(DWORD threadId)
{
    auto handle = ScopedHandle(::OpenThread(THREAD_QUERY_INFORMATION, FALSE, threadId));
    if (handle == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                      NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR)&msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            Log::Debug("GetThreadHandle: Error getting thread handle for thread id '", threadId, "': ", (LPTSTR)msgBuffer);
            LocalFree(msgBuffer);
        }
        else
        {
            Log::Debug("GetThreadHandle: Error getting thread handle for thread id '", threadId, "' (error = ", errorCode, ")");
        }
    }
    return handle;
}
#else
shared::WSTRING OpSysTools::GetNativeThreadName(pid_t tid)
{
    // TODO refactor this in OsSpecificApi
    char commPath[64] = "/proc/self/task/";

    // Adjust the base
    int base = 1000000000;
    int capacity = 64;
    while (base > tid)
    {
        base /= 10;
    }

    int offset = 16;
    // Write each number to the string
    while (base > 0 && offset < 64)
    {
        commPath[offset++] = (tid / base) + '0';
        tid %= base;
        base /= 10;
    }

    // check in case of misusage
    if (offset >= capacity || offset + 5 >= capacity)
    {
        return WStr("");
    }

    strncpy(commPath + offset, "/comm", 5);

    auto fd = open(commPath, O_RDONLY);
    if (fd == -1)
    {
        return WStr("");
    }
    on_leave
    {
        close(fd);
    };

    char line[16] = {0};

    auto length = read(fd, line, sizeof(line) - 1);
    if (length <= 0)
    {
        return {};
    }

    std::string threadName;
    if (line[length - 1] == '\n')
        threadName = std::string(line, length - 1);
    else
        threadName = std::string(line);

    return shared::ToWSTRING(std::move(threadName));
}
#endif

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
    auto charCount = GetModuleFileNameA((HMODULE)hModule, filename, sizeof(filename) / sizeof(filename[0]));
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

void OpSysTools::MemoryBarrierProcessWide()
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
    const int32_t length = 260;
    char buffer[length] = {'\0'};
    proc_name(getpid(), buffer, length);
    return std::string(buffer);
#else
    std::fstream comm("/proc/self/comm");
    std::string name;
    std::getline(comm, name);
    return name;
#endif
}

bool OpSysTools::IsSafeToStartProfiler(double coresThreshold, double& cpuLimit)
{
#if defined(_WINDOWS) || defined(DD_SANITIZERS)
    // Today we do not have any specific check before starting the profiler on Windows.
    // And also when we compile for sanitizers tests (Address and Undefined behavior sanitizers)
    cpuLimit = 0;
    return true;
#else
    // For linux, we check that the wrapper library is loaded and the default `dl_iterate_phdr` is
    // the one provided by our library.

    const std::string wrapperLibraryName = "Datadog.Linux.ApiWrapper.x64.so";
    const std::string customFnName = "dl_iterate_phdr";
    auto* dlIteratePhdr = reinterpret_cast<void*>(::dl_iterate_phdr);

    Dl_info info;
    auto res = dladdr(dlIteratePhdr, &info);
    if (res == 0 || info.dli_fname == nullptr)
    {
        Log::Warn("Profiling is disabled: Unable to check if the library '", wrapperLibraryName, "'",
                  " is correctly loaded and/or the function '", customFnName, "' is correctly wrapped.",
                  "Please contact the support for help with the following details:\n",
                  "Call to dladdr: ", res, "\n",
                  "info.dli_fname: ", info.dli_fname);
        return false;
    }

    auto sharedObjectPath = fs::path(info.dli_fname);

    if (sharedObjectPath.filename() != wrapperLibraryName)
    {
        // We assume that the profiler library is in the same folder as the wrapper library
        auto currentModulePath = fs::path(shared::GetCurrentModuleFileName());
        auto wrapperLibrary = currentModulePath.parent_path() / wrapperLibraryName;
        auto wrapperLibraryPath = wrapperLibrary.string();

        // Check if process is running is a secure-execution mode
        auto at_secure = getauxval(AT_SECURE);

        // Get LD_PRELOAD env var content
        auto envVarValue = shared::GetEnvironmentValue(WStr("LD_PRELOAD"));

        Log::Warn("Profiling is disabled: It appears the wrapper library '", wrapperLibraryName, "' is not correctly loaded.\n",
                  "Possible reason(s):\n",
                  "* The LD_PRELOAD environment variable might not contain the path '", wrapperLibraryPath, "'. Try adding ",
                  "'", wrapperLibraryPath, "' to LD_PRELOAD environment variable.\n",
                  "* Your application might be running in a secure execution mode (", std::boolalpha, at_secure != 0, "). Try adding ",
                  "the path '", wrapperLibraryPath, "' to the /etc/ld.so.preload file (create the file if needed).\n",
                  "If the issue persists, please contact the support with the following details:\n",
                  "LD_PRELOAD current value: ", (envVarValue.empty() ? WStr("<empty>") : envVarValue), "\n",
                  "The process running in a secure execution mode: ", std::boolalpha, at_secure != 0, "\n",
                  // Reasons for which AT_SECURE is true:
                  //   User ID != Effective User ID
                  "Process User ID differs from Effective User ID: ", std::boolalpha, getuid() != geteuid(), "\n",
                  //   Group ID != Effective Group ID
                  "Process Group ID differs from Effective Group ID: ", std::boolalpha, getgid() != getegid(), "\n");
        // TODO check capabilities (for now checking capabilities requires additional packages/libraries)
        // if at_secure is true, we know that it due to the capabilities

        return false;
    }

    if (CGroup::GetCpuLimit(&cpuLimit))
    {
        Log::Info("CPU limit is ", cpuLimit, " with ", coresThreshold, " threshold");

        if (cpuLimit < coresThreshold)
        {
            Log::Warn("The CPU limit is too low for the profiler to work properly.");
            return false;
        }
    }

    return true;

#endif
}

void OpSysTools::Sleep(std::chrono::nanoseconds duration)
{
#ifdef _WINDOWS
    ::Sleep(static_cast<DWORD>(duration.count() / 1000000));
#else
    usleep(duration.count() / 1000);
#endif
}
