// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Log.h"

#include "shared/src/native-src/string.h"

#include <chrono>
#include <string>
#include <thread>

#ifdef _WINDOWS
#include <atlbase.h>
#include "ScopedHandle.h"
#else
#include "pal.h"
#include "pal_mstypes.h"
#endif

class OpSysTools final
{
public:
    static int32_t GetProcId();
    static int32_t GetThreadId();
    // static std::string UnicodeToAnsi(const WCHAR* str);

    /// <summary>
    /// Gets nanoseconds since some arbitrary origin from a high precision system timer.
    /// This is not meant for time calculations, but for only for durations.
    /// <i>InitHighPrecisionTimer(..)</i> must be called at least once in the process before calling
    ///  this function.
    /// This function will return non-high-precision values or work slowly if a high precision timer
    /// is not available on the system or if <i>InitHighPrecisionTimer()</i> was not called.
    /// </summary>
    static inline std::int64_t GetHighPrecisionNanoseconds();

    static bool InitHighPrecisionTimer();

    static inline bool QueryThreadCycleTime(HANDLE handle, PULONG64 cycleTime);
    static inline HANDLE GetCurrentProcess();

    static bool SetNativeThreadName(const WCHAR* description);
#ifdef _WINDOWS
    static shared::WSTRING GetNativeThreadName(HANDLE threadHandle);
    static ScopedHandle GetThreadHandle(DWORD threadId);
    static bool GetFileVersion(LPCWSTR pszFilename, uint16_t& major, uint16_t& minor, uint16_t& build, uint16_t& reviews);
#else
    static shared::WSTRING GetNativeThreadName(pid_t tid);
#endif

    static bool GetModuleHandleFromInstructionPointer(void* nativeIP, std::uint64_t* pModuleHandle);
    static std::string GetModuleName(void* nativeIP);

    static void* AlignedMAlloc(size_t alignment, size_t size);

    static void MemoryBarrierProcessWide();

    static std::string GetHostname();
    static std::string GetProcessName();

#ifdef LINUX
    static bool ParseThreadInfo(char const* line, char& state, int32_t& userTime, int32_t& kernelTime)
    {
        // based on https://linux.die.net/man/5/proc
        // state  = 3rd position  and 'R' for Running
        // user   = 14th position in clock ticks
        // kernel = 15th position in clock ticks

        // The thread name is in second position and wrapped by ()
        // Since the name can contain SPACE and () characters, skip it before scanning the values
        auto* pos = strrchr(line, ')');

        // paranoia
        if (pos == nullptr)
            return false;

        int currentIdx = 2; // because we are currently at the thread name offset which is 2
        int nbElement = 0;
        while (nbElement != 3)
        {
            pos = strchr(pos, ' ');
            if (pos == nullptr)
                break;

            // skip whitespaces
            pos = pos + strspn(pos, " ");

            if (*pos == '\0')
                break;

            currentIdx++;
            if (currentIdx == 3)
            {
                state = *pos;
                nbElement++;
            }
            else if (currentIdx == 14)
            {
                userTime = atoi(pos);
                nbElement++;
            }
            else if (currentIdx == 15)
            {
                kernelTime = atoi(pos);
                nbElement++;
            }
        }
        return nbElement == 3;
    }

    ///
    /// This function get the current timestamp in a signal-safe manner
    ///
    static inline std::chrono::nanoseconds GetTimestampSafe()
    {
        struct timespec ts;
        // TODO error handling ?
        clock_gettime(CLOCK_REALTIME, &ts);
        return std::chrono::nanoseconds((std::uint64_t)ts.tv_sec * 1000000000 + ts.tv_nsec);
    }

#endif

    static bool IsSafeToStartProfiler(double coresThreshold, double& cpuLimit);
    static std::chrono::nanoseconds GetHighPrecisionTimestamp();
    static std::int64_t ConvertTicks(uint64_t ticks);

    static void Sleep(std::chrono::nanoseconds duration);

private:
    static constexpr std::int64_t NanosecondsPerSecond = 1000000000;

    static std::int64_t s_nanosecondsPerHighPrecisionTimerTick;
    static std::int64_t s_highPrecisionTimerTicksPerNanosecond;
    static uint64_t s_ticksPerSecond;

    static std::int64_t GetHighPrecisionNanosecondsFallback();

#ifdef _WINDOWS
    typedef HRESULT(__stdcall* SetThreadDescriptionDelegate_t)(HANDLE threadHandle, PCWSTR pThreadDescription);
    typedef HRESULT(__stdcall* GetThreadDescriptionDelegate_t)(HANDLE hThread, PWSTR* ppThreadDescription);
    typedef BOOL(__stdcall* GetFileVersionInfoSizeDelegate_t)(LPCWSTR pszFilename, DWORD* pdwHandle);
    typedef BOOL(__stdcall* GetFileVersionInfoDelegate_t)(LPCWSTR pszFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData);
    typedef BOOL(__stdcall* VerQueryValueDelegate_t)(LPCVOID pBlock, LPCWSTR lpSubBlock, LPVOID* lplpBuffer, PUINT puLen);
    static bool s_areWindowsDelegateSet;
    static SetThreadDescriptionDelegate_t s_setThreadDescriptionDelegate;
    static GetThreadDescriptionDelegate_t s_getThreadDescriptionDelegate;
    static GetFileVersionInfoSizeDelegate_t s_getFileVersionInfoSizeW;
    static GetFileVersionInfoDelegate_t s_getFileVersionInfoW;
    static VerQueryValueDelegate_t s_verQueryValueW;

    static void InitWindowsDelegates();
    static SetThreadDescriptionDelegate_t GetDelegate_SetThreadDescription();
    static GetThreadDescriptionDelegate_t GetDelegate_GetThreadDescription();
    static GetFileVersionInfoSizeDelegate_t GetDelegate_GetFileVersionInfoSize();
    static GetFileVersionInfoDelegate_t GetDelegate_GetFileVersionInfo();
    static VerQueryValueDelegate_t GetDelegate_VerQueryValue();
#endif
};

inline std::chrono::nanoseconds OpSysTools::GetHighPrecisionTimestamp()
{
    auto now = std::chrono::system_clock::now();

    return std::chrono::duration_cast<std::chrono::nanoseconds>(now.time_since_epoch());
}

inline std::int64_t OpSysTools::GetHighPrecisionNanoseconds()
{
#ifdef _WINDOWS
    if (0 != s_nanosecondsPerHighPrecisionTimerTick || 0 != s_highPrecisionTimerTicksPerNanosecond)
    {
        LARGE_INTEGER timerTicks;

        bool canQueryTimer = QueryPerformanceCounter(&timerTicks);
        if (canQueryTimer)
        {
            if (0 != s_nanosecondsPerHighPrecisionTimerTick)
            {
                std::uint64_t nanosecs = timerTicks.QuadPart * s_nanosecondsPerHighPrecisionTimerTick;
                return nanosecs;
            }

            if (0 != s_highPrecisionTimerTicksPerNanosecond)
            {
                std::uint64_t nanosecs = timerTicks.QuadPart / s_highPrecisionTimerTicksPerNanosecond;
                return nanosecs;
            }
        }
    }

    return OpSysTools::GetHighPrecisionNanosecondsFallback();
#else
    // Need to implement this for Linux!
    return OpSysTools::GetHighPrecisionNanosecondsFallback();
#endif
}

// TODO: remove if not needed
inline std::int64_t OpSysTools::ConvertTicks(uint64_t ticks)
{
#ifdef _WINDOWS
    if (s_ticksPerSecond != 0)
    {
        uint64_t microsecs = ticks * 1000000 / s_ticksPerSecond; // microseconds
        return microsecs * 1000;                                 // nanoseconds
    }

    return 0;
#else
    // only used for ETW (Windows only)
    return 0;
#endif
}

inline bool OpSysTools::QueryThreadCycleTime(HANDLE handle, PULONG64 cycleTime)
{
#ifdef _WINDOWS
    BOOL result = ::QueryThreadCycleTime(handle, cycleTime);
    return (result != 0);
#else
    return FALSE;
#endif
}

inline HANDLE OpSysTools::GetCurrentProcess()
{
#ifdef _WINDOWS
    return ::GetCurrentProcess();
#else
    return nullptr;
#endif
}

constexpr size_t DefaultPageSize{4096}; // Concerned about hugepages?
inline std::size_t GetPageSize()
{
#ifdef _WINDOWS
    throw std::runtime_error("GetPageSize() is not implemented for Windows.");
#else
    static std::size_t page_size = 0;
    if (page_size == 0)
    {
        page_size = sysconf(_SC_PAGESIZE);
        if (page_size != DefaultPageSize)
        {
            Log::Warn("Page size is ", page_size, " expected ", DefaultPageSize);
        }
    }
    return page_size;
#endif
}