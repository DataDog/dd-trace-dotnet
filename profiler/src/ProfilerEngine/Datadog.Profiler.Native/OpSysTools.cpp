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
#include "cgroup.h"
#include <errno.h>
#include <sys/auxv.h>
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
    on_leave { LocalFree(pThreadDescr); };

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
    on_leave { close(fd); };

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

bool OpSysTools::IsSafeToStartProfiler(double coresThreshold)
{
#if defined(_WINDOWS) || defined(DD_SANITIZERS)
    // Today we do not have any specific check before starting the profiler on Windows.
    // And also when we compile for sanitizers tests (Address and Undefined behavior sanitizers)
    return true;
#else
    // For linux, we check that the wrapper library is loaded and the default `dl_iterate_phdr` is
    // the one provided by our library.

    // We assume that the profiler library is in the same folder as the wrapper library
    auto currentModulePath = fs::path(shared::GetCurrentModuleFileName());
    auto wrapperLibrary = currentModulePath.parent_path() / "Datadog.Linux.ApiWrapper.x64.so";
    auto wrapperLibraryPath = wrapperLibrary.string();

    auto* instance = dlopen(wrapperLibraryPath.c_str(), RTLD_LAZY | RTLD_LOCAL);
    if (instance == nullptr)
    {
        auto errorId = errno;
        Log::Warn("Library '", wrapperLibraryPath, "' cannot be loaded (", strerror(errorId), "). This means that the profiler/tracer is not correctly installed.");
        return false;
    }

    const auto* customFnName = "dl_iterate_phdr";
    auto* customFn = dlsym(instance, customFnName);
    auto* defaultFn = dlsym(RTLD_DEFAULT, customFnName);

    // make sure that the default symbol for the custom function
    // is at the same address as the one found in our lib
    if (customFn != defaultFn)
    {
        Log::Warn("Custom function '", customFnName, "' is not the default one. That indicates that the library ",
                  "'", wrapperLibraryPath, "' is not loaded using the LD_PRELOAD environment variable");

        if (defaultFn != nullptr)
        {
            auto envVarValue = shared::GetEnvironmentValue(WStr("LD_PRELOAD"));

            Log::Info("LD_PRELOAD: ", (envVarValue.empty() ? WStr("<empty>") : envVarValue));

            Dl_info info;
            int rc = dladdr(defaultFn, &info);
            if (rc != 0)
            {
                Log::Info("\nShared library: ", info.dli_fname, "\nShared library base address: ", info.dli_fbase,
                          "\nNearest symbol: ", info.dli_sname, "\nNearest symbol address: ", info.dli_saddr);
            }
            else
            {
                Log::Info("Unable to get information about shared library containing '", customFnName, "'");
            }
        }

        // Check if process is running is a secure-execution mode
        auto at_secure = getauxval(AT_SECURE);
        Log::Info("Is process running in a secure execution mode ? ", std::boolalpha, at_secure);
        // Reasons for which AT_SECURE is true:
        //   User ID != Effective User ID
        Log::Info("Process User ID differs from Effective User ID ? ", std::boolalpha, getuid() != geteuid());
        //   Group ID != Effective Group ID
        Log::Info("Process Group ID differs from Effective Group ID ? ", std::boolalpha, getgid() != getegid());
        // TODO check capabilities (for now checking capabilities requires additional packages/libraries)
        // if at_secure is true, we know that it due to the capabilities

        return false;
    }

    double cpuLimit;

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
